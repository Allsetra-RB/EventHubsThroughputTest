using log4net;
using Microsoft.Azure.EventHubs.Processor;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest.EventHubs
{
	/// <summary>
	/// A lease manager for use in Azure Event Hubs that stores lease information on disk. Please note that this
	/// lease manager doesn't allow for consumers to be on different computers unless the lease store is on a
	/// network share they can all reach.
	/// </summary>
	public class DiskLeaseManager : ILeaseManager, ICheckpointManager
	{
		#region Properties
		/// <summary>
		/// The error code of an IOException that specifies a sharing violation.
		/// </summary>
		private const int ERROR_SHARING_VIOLATION = 32;
		/// <summary>
		/// The error code of an IOException that specifies lock violation.
		/// </summary>
		private const int ERROR_LOCK_VIOLATION = 33;

		/// <summary>
		/// The duration in milliseconds before an operation on all lease files is considered slow and results in
		/// a warning in the log.
		/// </summary>
		private const int SLOW_FILES_OPERATION_WARNING = 500;
		/// <summary>
		/// The duration in milliseconds before an operation on a lease file is considered slow and results in a
		/// warning in the log.
		/// </summary>
		private const int SLOW_FILE_OPERATION_WARNING = 150;
		/// <summary>
		/// The wait duration in milliseconds between attempts to access a locked file.
		/// </summary>
		private const int FILE_OPERATION_LOCK_WAIT = 25;

		/// <summary>
		/// A logger used to log important events and information.
		/// </summary>
		private static readonly ILog Log = LogManager.GetLogger(typeof(DiskLeaseManager));

		/// <summary>
		/// The name of the event hub for use in the lease store path.
		/// </summary>
		private readonly string _eventHub;
		/// <summary>
		/// The name of the consumer group for use in the lease store path.
		/// </summary>
		private readonly string _consumerGroup;
		/// <summary>
		/// The name of the owner of leases when acquired through this lease manager.
		/// </summary>
		private readonly string _owner;
		/// <summary>
		/// The base path to the lease store.
		/// </summary>
		private readonly string _path = "../Leases/";

		/// <summary>
		/// The lease duration. After this duration, the lease is expired. It is filled with a default value in case a
		/// caller doesn't set it.
		/// </summary>
		private TimeSpan _leaseDuration = TimeSpan.FromSeconds(30);

		/// <summary>
		/// Gets the path to the lease store.
		/// </summary>
		private string LeaseStore
		{
			get { return Path.Combine(_path, _eventHub, _consumerGroup); }
		}

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
					 value.TotalMinutes > 30)
				{
					throw new ArgumentException("The lease duration must be between 5 seconds and 30 minutes.");
				}

				_leaseDuration = value;
			}
		}

		/// <summary>
		/// Gets the path of the lease file for the provided partition.
		/// </summary>
		/// <param name="partitionId">The partition id.</param>
		/// <returns>The path to the lease file.</returns>
		private string GetPartitionFile(string partitionId)
		{
			return Path.Combine(LeaseStore, partitionId);
		}

		/// <summary>
		/// Gets the path of the lease file for the provided lease.
		/// </summary>
		/// <param name="lease">The lease.</param>
		/// <returns>The path to the lease file.</returns>
		private string GetPartitionFile(Lease lease)
		{
			return GetPartitionFile(lease.PartitionId);
		}

		/// <summary>
		/// Gets a key that can be used to lock access to a lease file to prevent concurrent access.
		/// </summary>
		/// <param name="lease">The lease.</param>
		/// <returns>A unique key for this lease that can be used to lock on.</returns>
		private string GetLockKey(Lease lease)
		{
			return $"Lock~EventHubs~Lease~{lease.PartitionId}";
		}
		#endregion

		#region Constructors & Destructors
		/// <summary>
		/// Creates a new DiskLeaseManager.
		/// </summary>
		/// <param name="eventHub">The name of the event hub.</param>
		/// <param name="consumerGroup">The name of the consumer group.</param>
		/// <param name="owner">The name of the owner of the leases acquired by this lease manager. This should be
		/// the name of the EventProcessorHost.</param>
		/// <param name="leaseStorePath">The optional path to a directory where the lease store will be located. If
		/// left empty, leases will be located at ../Leases/.</param>
		public DiskLeaseManager(string eventHub, string consumerGroup, string owner, string leaseStorePath = null)
		{
			_eventHub = eventHub;
			_consumerGroup = consumerGroup;
			_owner = owner;
			_path = leaseStorePath ?? _path;
		}
		#endregion

		#region Leases
		/// <summary>
		/// Checks if the lease store exists.
		/// </summary>
		/// <returns>True if so, false if not.</returns>
		public Task<bool> LeaseStoreExistsAsync()
		{
			bool result = Directory.Exists(LeaseStore);

			return Task.FromResult(result);
		}

		/// <summary>
		/// Creates a new lease store if it doesn't exist already. In this case a SQL Server table is created.
		/// </summary>
		/// <returns>True if the store was created, false if it already existed.</returns>
		public Task<bool> CreateLeaseStoreIfNotExistsAsync()
		{
			bool result = Directory.CreateDirectory(LeaseStore).Exists;

			return Task.FromResult(result);
		}

		/// <summary>
		/// Deletes the lease store. This should not be used.
		/// </summary>
		/// <returns>True if the action succeeded, false if not.</returns>
		public Task<bool> DeleteLeaseStoreAsync()
		{
			Directory.Delete(LeaseStore, true);

			return Task.FromResult(true);
		}

		/// <summary>
		/// Gets all leases from the lease store.
		/// </summary>
		/// <remarks>
		/// This call is used once and only once in every acquire loop in the PartitionManager and should be
		/// optimized as such.
		/// </remarks>
		/// <returns>All leases.</returns>
		public async Task<IEnumerable<Lease>> GetAllLeasesAsync()
		{
			await Task.Delay(1).ConfigureAwait(false);

			string[] files = Directory.GetFiles(LeaseStore);
			List<DiskLease> result = (await WaitForLockReadAll(files).ConfigureAwait(false)).Select(JsonConvert.DeserializeObject<DiskLease>).
				ToList();

			return result;
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
		public async Task<Lease> GetLeaseAsync(string partitionId)
		{
			await Task.Delay(1).ConfigureAwait(false);

			string partitionFile = GetPartitionFile(partitionId);
			string data = await WaitForLockRead(partitionFile).ConfigureAwait(false);
			DiskLease result = JsonConvert.DeserializeObject<DiskLease>(data);

			return result;
		}

		/// <summary>
		/// Creates a new lease if it doesn't exist in the store yet.
		/// </summary>
		/// <param name="partitionId">The partition id.</param>
		/// <returns>The new lease or the existing one.</returns>
		public async Task<Lease> CreateLeaseIfNotExistsAsync(string partitionId)
		{
			string partitionFile = GetPartitionFile(partitionId);

			// NOTE: Not particularly threadsafe, but this call is only executed on first startup of a consumer ever.
			if (File.Exists(partitionFile))
			{
				return await GetLeaseAsync(partitionId).ConfigureAwait(false);
			}

			DiskLease result = new DiskLease(partitionId) { LeaseDuration = LeaseDuration, };

			string data = JsonConvert.SerializeObject(result);
			await WaitForLockWrite(partitionFile, data).ConfigureAwait(false);

			return result;
		}

		/// <summary>
		/// Deletes a lease from the lease store.
		/// </summary>
		/// <param name="lease">The lease to delete.</param>
		public Task DeleteLeaseAsync(Lease lease)
		{
			string partitionFile = GetPartitionFile(lease);
			File.Delete(partitionFile);

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
			// BUG: Not safe when used across multiple processes or machines.

			await Task.Delay(1).ConfigureAwait(false);

			DiskLease staleLease = lease is DiskLease x ? x : new DiskLease(lease) { LeaseDuration = LeaseDuration, };
			string key = GetLockKey(staleLease);
			bool hasLock = false;

			try
			{
				Monitor.Enter(key, ref hasLock);

				// NOTE: We can't check the token or expiry, because acquiring is used to steal leases.

				// Synchronous Task execution because we need to remain in the same thread for the lock we're holding.
				staleLease.Acquire(_owner, LeaseDuration);
				UpdateLeaseInternalAsync(staleLease).Wait();

				return true;
			}
			finally
			{
				if (hasLock)
				{
					Monitor.Exit(key);
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
			// BUG: Not safe when used across multiple processes or machines.

			await Task.Delay(1).ConfigureAwait(false);

			DiskLease staleLease = lease is DiskLease x ? x : new DiskLease(lease) { LeaseDuration = LeaseDuration, };
			DiskLease freshLease = (DiskLease)await GetLeaseAsync(lease.PartitionId).ConfigureAwait(false);
			string key = GetLockKey(staleLease);
			bool hasLock = false;

			try
			{
				Monitor.Enter(key, ref hasLock);

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
				UpdateLeaseInternalAsync(staleLease).Wait();

				return true;
			}
			finally
			{
				if (hasLock)
				{
					Monitor.Exit(key);
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
			// BUG: Not safe when used across multiple processes or machines.

			await Task.Delay(1).ConfigureAwait(false);

			DiskLease staleLease = lease is DiskLease x ? x : new DiskLease(lease) { LeaseDuration = LeaseDuration, };
			DiskLease freshLease = (DiskLease)await GetLeaseAsync(lease.PartitionId).ConfigureAwait(false);
			string key = GetLockKey(staleLease);
			bool hasLock = false;

			try
			{
				Monitor.Enter(key, ref hasLock);

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
				UpdateLeaseInternalAsync(staleLease).Wait();

				return true;
			}
			finally
			{
				if (hasLock)
				{
					Monitor.Exit(key);
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
			// BUG: Not safe when used across multiple processes or machines.

			await Task.Delay(1).ConfigureAwait(false);

			DiskLease staleLease = lease is DiskLease x ? x : new DiskLease(lease) { LeaseDuration = LeaseDuration, };
			DiskLease freshLease = (DiskLease)await GetLeaseAsync(lease.PartitionId).ConfigureAwait(false);
			string key = GetLockKey(staleLease);
			bool hasLock = false;

			try
			{
				Monitor.Enter(key, ref hasLock);

				// We can't modify the lease, it's no longer ours to use. Expiration doesn't matter. If the lease has been
				// stolen by someone else, but it has already expired, it's still not ours to modify without first acquiring.
				if (freshLease.Token != staleLease.Token)
				{
					return false;
				}

				// NOTE: Disabled because there's temporarily only 1 running.

				//// We can't modify a lease that has already expired. It must first be acquired again.
				//// Synchronous Task execution because we need to remain in the same thread for the lock we're holding.
				//if (freshLease.IsExpired().Result)
				//{
				//	return false;
				//}

				// Synchronous Task execution because we need to remain in the same thread for the lock we're holding.
				UpdateLeaseInternalAsync(staleLease).Wait();

				return true;
			}
			finally
			{
				if (hasLock)
				{
					Monitor.Exit(key);
				}
			}
		}

		/// <summary>
		/// Performs the basic update logic for a lease. This call should always be wrapped in a call that
		/// performs validation.
		/// </summary>
		/// <param name="lease">The lease to update.</param>
		private async Task UpdateLeaseInternalAsync(Lease lease)
		{
			DiskLease realLease = lease is DiskLease x ? x : new DiskLease(lease) { LeaseDuration = LeaseDuration, };
			string data = JsonConvert.SerializeObject(realLease);
			string partitionFile = GetPartitionFile(realLease);
			await WaitForLockWrite(partitionFile, data).ConfigureAwait(false);
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
		/// <returns>True if the store exists, false if it was created.</returns>
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
			await Task.Delay(1).ConfigureAwait(false);

			DiskLease realLease = lease is DiskLease x ? x : new DiskLease(lease) { LeaseDuration = LeaseDuration, };

			// TODO: Also renew the lease to ensure it doesn't expire right after checkpointing. Basically anything touching the lease should renew it, creating a sliding expiration.
			realLease.Offset = checkpoint.Offset;
			realLease.SequenceNumber = checkpoint.SequenceNumber;

			bool result = await UpdateLeaseAsync(realLease).ConfigureAwait(false);

			// Because we don't have a return value, we have no other choice than to throw an exception if the lease failed
			// to update. If we don't, we would have a checkpoint that was never persisted.
			if (!result)
			{
				throw new
					InvalidOperationException($"The checkpoint for partition '{checkpoint.PartitionId}' could not be updated, because the lease is not owned.");
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

		#region Helpers
		/// <summary>
		/// Reads all of the provided files, but waits (efficiently) if any file locks are encountered until the
		/// lock is released. There is no protection against infinite locks!
		/// </summary>
		/// <param name="paths">The paths of the files to read.</param>
		/// <returns>The data in all of the files as a string.</returns>
		private async Task<List<string>> WaitForLockReadAll(string[] paths)
		{
			bool alreadyWarned = false;
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();

			Queue<string> queuedPaths = new Queue<string>(paths);
			List<string> files = new List<string>();

			while (queuedPaths.Count > 0)
			{
				string path = queuedPaths.Dequeue();

				try
				{
					files.Add(File.ReadAllText(path));
				}
				catch (IOException e)
				{
					// Every path that can't be read is put at the back of the queue. This logic makes the read operation
					// skip over any locked files instead of explicitly waiting for them. Since we have a bunch of files
					// to read, this style increase efficiency.
					queuedPaths.Enqueue(path);

					if (IsFileLocked(e))
					{
						if (!alreadyWarned && stopwatch.ElapsedMilliseconds > SLOW_FILES_OPERATION_WARNING)
						{
							Log.Warn("Reading all files is taking a while...");
							alreadyWarned = true;
						}

						// Only waste time if we only have 1 more file to read that's currently locked.
						if (queuedPaths.Count == 1)
						{
							await Task.Delay(FILE_OPERATION_LOCK_WAIT).ConfigureAwait(false);
						}
						continue;
					}

					throw;
				}
			}

			stopwatch.Stop();

			if (stopwatch.ElapsedMilliseconds > SLOW_FILES_OPERATION_WARNING)
			{
				Log.Warn($"Reading all files took '{stopwatch.ElapsedMilliseconds}' milliseconds!");
			}

			return files;
		}

		/// <summary>
		/// Reads the provided file, but waits if a file lock is encountered until the lock is released. There is
		/// no protection against infinite locks!
		/// </summary>
		/// <param name="path">The path of the file to read.</param>
		/// <returns>The data in the file as a string.</returns>
		private async Task<string> WaitForLockRead(string path)
		{
			bool alreadyWarned = false;
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();

			while (true)
			{
				try
				{
					string data = File.ReadAllText(path);
					stopwatch.Stop();

					if (stopwatch.ElapsedMilliseconds > SLOW_FILE_OPERATION_WARNING)
					{
						Log.Warn($"Reading file '{path}' took '{stopwatch.ElapsedMilliseconds}' milliseconds!");
					}

					return data;
				}
				catch (IOException e)
				{
					if (IsFileLocked(e))
					{
						if (!alreadyWarned && stopwatch.ElapsedMilliseconds > SLOW_FILE_OPERATION_WARNING)
						{
							Log.Warn($"Reading file '{path}' is taking a while...");
							alreadyWarned = true;
						}

						await Task.Delay(FILE_OPERATION_LOCK_WAIT).ConfigureAwait(false);
						continue;
					}

					throw;
				}
			}
		}

		/// <summary>
		/// Writes to the provided file, but waits if a file lock is encountered until the lock is released. There
		/// is no protection against infinite locks!
		/// </summary>
		/// <param name="path">The path of the file to write to.</param>
		/// <param name="data">The data for the file as a string.</param>
		private async Task WaitForLockWrite(string path, string data)
		{
			bool alreadyWarned = false;
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();

			while (true)
			{
				try
				{
					File.WriteAllText(path, data);
					stopwatch.Stop();

					if (stopwatch.ElapsedMilliseconds > SLOW_FILE_OPERATION_WARNING)
					{
						Log.Warn($"Writing file '{path}' took '{stopwatch.ElapsedMilliseconds}' milliseconds!");
					}

					return;
				}
				catch (IOException e)
				{
					if (IsFileLocked(e))
					{
						if (!alreadyWarned && stopwatch.ElapsedMilliseconds > SLOW_FILE_OPERATION_WARNING)
						{
							Log.Warn($"Writing file '{path}' is taking a while...");
							alreadyWarned = true;
						}

						await Task.Delay(FILE_OPERATION_LOCK_WAIT).ConfigureAwait(false);
						continue;
					}

					throw;
				}
			}
		}

		/// <summary>
		/// Gets a value that indicates if the IOException occurred because the file was locked.
		/// </summary>
		/// <param name="exception">The exception to check.</param>
		/// <returns>True if the file was locked, false if not.</returns>
		private bool IsFileLocked(IOException exception)
		{
			int errorCode = exception.HResult & ((1 << 16) - 1);
			return errorCode == ERROR_SHARING_VIOLATION || errorCode == ERROR_LOCK_VIOLATION;
		}
		#endregion
	}
}
