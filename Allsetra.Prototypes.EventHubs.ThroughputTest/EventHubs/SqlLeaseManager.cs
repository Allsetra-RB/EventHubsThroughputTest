using Allsetra.Utilities.Db;
using Microsoft.Azure.EventHubs.Processor;
using System;
using System.Collections.Generic;
using System.Data.Linq.Mapping;
using System.Linq;
using System.Threading.Tasks;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest.EventHubs
{
	/// <summary>
	/// A Lease Manager for use in Azure Event Hubs that stores its leases in a SQL Server database.
	/// </summary>
	/// <typeparam name="T">The type of the lease to use. The lease type should specify the TableAttribute
	/// to specify which table is used to store lease information.</typeparam>
	public class SqlLeaseManager<T> : ILeaseManager, ICheckpointManager
		where T : SqlLease, new()
	{
		#region Properties
		/// <summary>
		/// Creates a new lease of type T from a partition id.
		/// </summary>
		/// <param name="partitionId">The partition id.</param>
		/// <returns>The new lease.</returns>
		public delegate T CreateLease(string partitionId);
		/// <summary>
		/// Wraps the provided lease in a lease of type T, which adds functionality to the basic lease.
		/// </summary>
		/// <param name="source">The lease to wrap.</param>
		/// <returns>The wrapped lease.</returns>
		public delegate T WrapLease(Lease source);

		/// <summary>
		/// The name of the lease table for leases of type T.
		/// </summary>
		private readonly string _leaseTable;
		/// <summary>
		/// The name of the lease owner when acquired through this lease manager.
		/// </summary>
		private readonly string _owner;
		/// <summary>
		/// The connection string to the SQL Server used as a lease store.
		/// </summary>
		private readonly string _connectionString;
		/// <summary>
		/// The delegate used to create a new T lease from scratch.
		/// </summary>
		private readonly CreateLease _createLease;
		/// <summary>
		/// The delegate used to wrap a basic lease in a T lease.
		/// </summary>
		private readonly WrapLease _wrapLease;

		/// <summary>
		/// The lease duration, filled with a default value in case it is not set by a caller.
		/// </summary>
		private TimeSpan _leaseDuration = TimeSpan.FromSeconds(30);

		/// <summary>
		/// Gets the lease renew interval, which determines how often the acquire loop is executed.
		/// </summary>
		/// <remarks>
		/// This value is also used by the blob storage manager as a timeout for server communication when renewing
		/// its leases.
		/// </remarks>
		public TimeSpan LeaseRenewInterval
		{
			get { return TimeSpan.FromSeconds(10); }
		}

		/// <summary>
		/// Gets or sets the lease duration. This determines how long a lease will last and how long a consumer must
		/// wait to reclaim its leases after a crash.
		/// </summary>
		/// <remarks>
		/// Maybe the blob storage has built-in logic for renewal to use the same lease duration as when the lease
		/// was first acquired, because the blob storage manager only uses this to acquire a new lease. There is
		/// a limit on the blob storage that this value may not be shorter than 15 seconds and may not be longer
		/// than 60 seconds, unless it is an infinite duration. These restrictions do not apply to this lease manager,
		/// but we must be cautious not to let it be set too low or high, or it will have dire consequences.
		/// </remarks>
		public TimeSpan LeaseDuration
		{
			get { return _leaseDuration; }
			set
			{
				if (value == TimeSpan.MinValue ||
					 value == TimeSpan.Zero ||
					 value == TimeSpan.MaxValue ||
					 value.TotalSeconds < 5 ||
					 value.TotalMinutes > 60)
				{
					throw new ArgumentException("The lease duration must be between 5 seconds and 60 minutes.");
				}

				_leaseDuration = value;
			}
		}

		/// <summary>
		/// Gets a unique key for the provided lease which can be used for locking to ensure synchronicity when
		/// modifying vital lease information.
		/// </summary>
		/// <param name="lease">The lease to get a lock key for.</param>
		/// <returns>A lock key that is unique for this lease.</returns>
		private string GetLockKey(Lease lease)
		{
			return $"EventHubs~{_leaseTable}~Lease~{lease.PartitionId}";
		}
		#endregion

		#region Constructors & Destructors
		/// <summary>
		/// Creates a new SqlLeaseManager.
		/// </summary>
		/// <param name="owner">The name of the owner of leases being acquired through this lease manager. This should be
		/// the host name of the EventProcessorHost using this lease manager.</param>
		/// <param name="connectionString">A connection string to a SQL Server database in which leases are stored. The name
		/// of the table is defined by the TableAttributed on the lease type.</param>
		/// <param name="createLease">A delegate that specifies how a new lease can be created from a partition id.</param>
		/// <param name="wrapLease">A delegate that specifies how an existing lease can be wrapped in the lease type.</param>
		public SqlLeaseManager(string owner, string connectionString, CreateLease createLease, WrapLease wrapLease)
		{
			_leaseTable = typeof(T).GetCustomAttributes(typeof(TableAttribute), false).OfType<TableAttribute>().FirstOrDefault()?.Name;

			if (_leaseTable == null)
			{
				throw new TypeLoadException($"The lease type '{typeof(T).Name}' is not correctly annotated for use as a SQL lease.");
			}

			_owner = owner ?? throw new ArgumentNullException(nameof(owner));
			_connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

			_createLease = createLease ?? throw new ArgumentNullException(nameof(createLease));
			_wrapLease = wrapLease ?? throw new ArgumentNullException(nameof(wrapLease));
		}
		#endregion

		#region Leases
		/// <summary>
		/// Checks if the lease store exists.
		/// </summary>
		/// <returns>True if so, false if not.</returns>
		public Task<bool> LeaseStoreExistsAsync()
		{
			using (SqlQuery query = new SqlQuery(_connectionString))
			{
				return Task.FromResult(query.TableExists<T>());
			}
		}

		/// <summary>
		/// Creates a new lease store if it doesn't exist already. In this case a SQL Server table is created.
		/// </summary>
		/// <returns>True if the action succeeded, false if not.</returns>
		public Task<bool> CreateLeaseStoreIfNotExistsAsync()
		{
			using (SqlQuery query = new SqlQuery(_connectionString))
			{
				bool exists = query.TableExists<T>();

				if (!exists)
				{
					query.CreateTable<T>();
				}
			}

			return Task.FromResult(true);
		}

		/// <summary>
		/// Deletes the lease store. This should not be used.
		/// </summary>
		/// <returns>True if the action succeeded, false if not.</returns>
		public Task<bool> DeleteLeaseStoreAsync()
		{
			throw new NotSupportedException("Lease stores will not be deleted! Clean up manually.");
		}

		/// <summary>
		/// Gets all leases from the lease store.
		/// </summary>
		/// <remarks>
		/// This call is used once and only once in every acquire loop in the PartitionManager and should be
		/// optimized as such.
		/// </remarks>
		/// <returns>All leases.</returns>
		public Task<IEnumerable<Lease>> GetAllLeasesAsync()
		{
			using (SqlQuery query = new SqlQuery(_connectionString))
			{
				return Task.FromResult(query.SelectAll<T>().Cast<Lease>());
			}
		}

		/// <summary>
		/// Gets the lease for the provided partition from the lease store.
		/// </summary>
		/// <remarks>
		/// This call is used during the acquire loop of the PartitionManager to either set up a new partition to listen to or
		/// during the stealing of leases. It is used to refresh all lease data and replaces the original lease when used. That
		/// means the returned lease must be complete and up to date to avoid runtime issues.
		/// </remarks>
		/// <param name="partitionId">The partition to get the lease for.</param>
		/// <returns>The lease of the provided partition.</returns>
		public Task<Lease> GetLeaseAsync(string partitionId)
		{
			using (SqlQuery query = new SqlQuery(_connectionString))
			{
				return Task.FromResult((Lease)query.SelectWhere<T, string>(x => x.DbPartitionId, partitionId).FirstOrDefault());
			}
		}

		/// <summary>
		/// Creates a new lease if it doesn't exist in the store yet.
		/// </summary>
		/// <param name="partitionId">The partition id.</param>
		/// <returns>The new lease or the existing one.</returns>
		public async Task<Lease> CreateLeaseIfNotExistsAsync(string partitionId)
		{
			Lease lease = await GetLeaseAsync(partitionId).ConfigureAwait(false);

			if (lease == null)
			{
				T typedLease;
				lease = typedLease = _createLease(partitionId);

				using (SqlQuery query = new SqlQuery(_connectionString))
				{
					query.InsertAll(new List<T> { typedLease });
				}
			}

			return lease;
		}

		/// <summary>
		/// Deletes a lease from the lease store.
		/// </summary>
		/// <param name="lease">The lease to delete.</param>
		public Task DeleteLeaseAsync(Lease lease)
		{
			using (SqlQuery query = new SqlQuery(_connectionString))
			{
				query.DeleteWhere<T, string>(x => x.DbPartitionId, new List<string> { lease.PartitionId });
			}

			return Task.CompletedTask;
		}

		/// <summary>
		/// Acquires the provided lease, the easy way or the hard way.
		/// </summary>
		/// <remarks>
		/// Acquiring is only used to steal leases from other owners. This applies exclusively to leases that haven't expired
		/// yet. Please be aware that "" counts as another owner, so released leases need to be stolen. If the acquire fails,
		/// the lease will not be available to anyone during the current acquire loop in the PartitionManager.
		/// </remarks>
		/// <param name="lease">The lease to acquire.</param>
		/// <returns>True if the lease was acquired, false if not.</returns>
		public async Task<bool> AcquireLeaseAsync(Lease lease)
		{
			T staleLease = lease is T x ? x : _wrapLease(lease);
			string key = GetLockKey(staleLease);

			using (SqlQuery query = new SqlQuery(_connectionString, true))
			{
				using (new ApplicationLockScope(query, key))
				{
					// NOTE: We can't check the token or expiry, because acquiring is used to steal leases.

					staleLease.Acquire(_owner, LeaseDuration);
					await UpdateLeaseInternalAsync(query, staleLease).ConfigureAwait(false);

					return true;
				}
			}
		}

		/// <summary>
		/// Renews the provided lease, refreshing its expiration date so the lease may be used for a while longer.
		/// </summary>
		/// <remarks>
		/// Only leases already owned by the current PartitionManager can be renewed (judged by Lease.Owner). This includes
		/// leases that have already expired, but belonged to the current owner before they expired. The result of this
		/// method is not actually used by the PartitionManager. Failure by exception to renew a lease will result in a
		/// restart of the acquire loop in the PartitionManager.
		/// </remarks>
		/// <param name="lease">The lease to renew.</param>
		/// <returns>True if the lease was renewed, false if not.</returns>
		public async Task<bool> RenewLeaseAsync(Lease lease)
		{
			T staleLease = lease is T x ? x : _wrapLease(lease);
			T freshLease = (T)await GetLeaseAsync(lease.PartitionId).ConfigureAwait(false);
			string key = GetLockKey(staleLease);

			using (SqlQuery query = new SqlQuery(_connectionString, true))
			{
				using (new ApplicationLockScope(query, key))
				{
					// We can't renew the lease, it's no longer ours to use. Expiration doesn't matter. If the lease has been
					// stolen by someone else, but it has already expired, it's still not ours to renew without first acquiring.
					if (freshLease.Token != staleLease.Token)
					{
						return false;
					}

					// Normally we shouldn't renew a lease that has expired, but since we already confirmed we were the last owners,
					// and the caller doesn't use the return value, we have no choice but to renew it anyway.

					// Synchronous Task execution because we need to remain in the same thread for the lock we're holding.
					staleLease.Renew(LeaseDuration);
					await UpdateLeaseInternalAsync(query, staleLease).ConfigureAwait(false);

					return true;
				}
			}
		}

		/// <summary>
		/// Releases the provided lease, resetting ownership information so that another owner may acquire the lease.
		/// </summary>
		/// <remarks>
		/// Leases are only ever released if a PartitionPump closes gracefully by shutdown. Stealing of leases or crashes
		/// of a consumer result in 'dirty' leases of which must be stolen before they can be used by another owner. The
		/// return value is not used, so failure to release a lease in any way will cause the application to continue anyway.
		/// However, exceptions during releasing will be propagated to the unhandled exception handler in EventProcessorOptions.
		/// </remarks>
		/// <param name="lease">The lease to release.</param>
		/// <returns>True if the lease was released, false if not.</returns>
		public async Task<bool> ReleaseLeaseAsync(Lease lease)
		{
			T staleLease = lease is T x ? x : _wrapLease(lease);
			T freshLease = (T)await GetLeaseAsync(lease.PartitionId).ConfigureAwait(false);
			string key = GetLockKey(staleLease);

			using (SqlQuery query = new SqlQuery(_connectionString, true))
			{
				using (new ApplicationLockScope(query, key))
				{
					// We can't release the lease, it's no longer ours to use. Expiration doesn't matter. If the lease has been
					// stolen by someone else, but it has already expired, it's still not ours to release without first acquiring.
					if (freshLease.Token != staleLease.Token)
					{
						return false;
					}

					// Normally we shouldn't release a lease that has expired, but since we already confirmed we were the last owners,
					// and the caller doesn't use the return value, we have no choice but to release it anyway.

					// Synchronous Task execution because we need to remain in the same thread for the lock we're holding.
					staleLease.Release();
					await UpdateLeaseInternalAsync(query, staleLease).ConfigureAwait(false);

					return true;
				}
			}
		}

		/// <summary>
		/// Updates the provided lease, persisting its data to the lease store.
		/// </summary>
		/// <remarks>
		/// This call is not used by the API and is only useful for internal calls like UpdateCheckpointAsync.
		/// </remarks>
		/// <param name="lease">The lease to update.</param>
		/// <returns>True if the lease was updated, false if not.</returns>
		public async Task<bool> UpdateLeaseAsync(Lease lease)
		{
			T staleLease = lease is T x ? x : _wrapLease(lease);
			T freshLease = (T)await GetLeaseAsync(lease.PartitionId).ConfigureAwait(false);
			string key = GetLockKey(staleLease);

			using (SqlQuery query = new SqlQuery(_connectionString, true))
			{
				using (new ApplicationLockScope(query, key))
				{
					// We can't modify the lease, it's no longer ours to use. Expiration doesn't matter. If the lease has been
					// stolen by someone else, but it has already expired, it's still not ours to modify without first acquiring.
					if (freshLease.Token != staleLease.Token)
					{
						return false;
					}

					// We can't modify a lease that has already expired. It must first be acquired again.
					// Synchronous Task execution because we need to remain in the same thread for the lock we're holding.
					if (freshLease.IsExpired().Result)
					{
						return false;
					}

					// Synchronous Task execution because we need to remain in the same thread for the lock we're holding.
					await UpdateLeaseInternalAsync(query, staleLease).ConfigureAwait(false);

					return true;
				}
			}
		}

		/// <summary>
		/// Performs the basic update logic for a lease. This call should always be wrapped in a call that
		/// performs validation.
		/// </summary>
		/// <param name="query">The query to perform the update one.</param>
		/// <param name="lease">The lease to update.</param>
		private Task UpdateLeaseInternalAsync(SqlQuery query, Lease lease)
		{
			T realLease = lease is T x ? x : _wrapLease(lease);

			query.Update(new List<T> { realLease },
						 y => y.DbOwner,
						 y => y.DbToken,
						 y => y.DbEpoch,
						 y => y.DbOffset,
						 y => y.DbSequenceNumber,
						 y => y.AcquiredAt,
						 y => y.ExpiresAt);

			return Task.CompletedTask;
		}
		#endregion

		#region Checkpointing
		/// <summary>
		/// Checks if the checkpoint store exists.
		/// </summary>
		/// <returns>True if so, false if not.</returns>
		public Task<bool> CheckpointStoreExistsAsync()
		{
			return LeaseStoreExistsAsync();
		}

		/// <summary>
		/// Creates a new checkpoint store if it doesn't exist already.
		/// </summary>
		/// <returns>True if the action succeeded, false if not.</returns>
		public Task<bool> CreateCheckpointStoreIfNotExistsAsync()
		{
			return CreateLeaseStoreIfNotExistsAsync();
		}

		/// <summary>
		/// Gets the current checkpoint of the partition, if any.
		/// </summary>
		/// <remarks>
		/// This method is called to check if there is a checkpoint to continue from or if the consumer must start from the
		/// beginning. The caller expects null if there is no checkpoint yet (even though the lease exists and the checkpoint
		/// data is weaved into the lease data). If at any point a checkpoint with offset "" is returned, the PartitionPump
		/// will crash on an ArgumentNullException and retry. Retrying results in the same offset, and because the retry is
		/// executed so often, the machine's CPU is exhausted, crippling the consumer and possibly other applications on the
		/// same machine.
		/// is basically crippled.
		/// </remarks>
		/// <param name="partitionId">The partition to get the lease for.</param>
		/// <returns>The checkpoint of the provided partition, or null if there is no checkpoint yet.</returns>
		public async Task<Checkpoint> GetCheckpointAsync(string partitionId)
		{
			Lease lease = await GetLeaseAsync(partitionId).ConfigureAwait(false);

			if (lease.Offset == null)
			{
				return null;
			}

			return new Checkpoint(lease.PartitionId, lease.Offset, lease.SequenceNumber);
		}

		/// <summary>
		/// Creates a new checkpoint if it doesn't exit in the store already.
		/// </summary>
		/// <param name="partitionId">The partition id.</param>
		/// <returns>The new checkpoint or an existing one.</returns>
		public async Task<Checkpoint> CreateCheckpointIfNotExistsAsync(string partitionId)
		{
			Lease lease = await CreateLeaseIfNotExistsAsync(partitionId).ConfigureAwait(false);
			return new Checkpoint(lease.PartitionId, lease.Offset, lease.SequenceNumber);
		}

		/// <summary>
		/// Updates the provided checkpoint for the provided lease, persisting it to the checkpoint store.
		/// </summary>
		/// <remarks>
		/// Checkpoints are protected against out of sync and old data by the PartitionContext, so there is no need to
		/// perform checks on whether or not it's going backwards.
		/// </remarks>
		/// <param name="lease">The lease to associate the checkpoint with.</param>
		/// <param name="checkpoint">The checkpoint to persist.</param>
		public async Task UpdateCheckpointAsync(Lease lease, Checkpoint checkpoint)
		{
			T realLease = lease is T x ? x : _wrapLease(lease);

			realLease.Renew(LeaseDuration);
			realLease.Offset = checkpoint.Offset;
			realLease.SequenceNumber = checkpoint.SequenceNumber;

			bool result = await UpdateLeaseAsync(realLease).ConfigureAwait(false);

			// Because we don't have a return value, we have no other choice than to throw an exception if the lease failed
			// to update. If we don't, we would have a checkpoint that was never persisted.
			if (!result)
			{
				throw new InvalidOperationException($"The checkpoint for partition '{checkpoint.PartitionId}' could not be updated, because the lease is not owned.");
			}
		}

		/// <summary>
		/// Deletes a checkpoint from the store.
		/// </summary>
		/// <param name="partitionId">The partition id.</param>
		public async Task DeleteCheckpointAsync(string partitionId)
		{
			Lease lease = await GetLeaseAsync(partitionId).ConfigureAwait(false);
			await UpdateCheckpointAsync(lease, new Checkpoint(lease.PartitionId)).ConfigureAwait(false);
		}
		#endregion
	}
}
