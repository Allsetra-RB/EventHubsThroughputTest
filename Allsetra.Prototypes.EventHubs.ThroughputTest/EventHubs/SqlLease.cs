using Microsoft.Azure.EventHubs.Processor;
using System;
using System.ComponentModel;
using System.Data.Linq.Mapping;
using System.Threading.Tasks;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest.EventHubs
{
	/// <summary>
	/// Defines what a lease compatible with SQL looks like. It must be derived and attributed with the TableAttribute
	/// to be fully compatible.
	/// </summary>
	public abstract class SqlLease : Lease
	{
		#region Properties
		/// <summary>
		/// Gets the ID of the partition to which this lease belongs.
		/// </summary>
		/// <remarks>
		/// Required to place the Column attribute for SQL compatibility.
		/// </remarks>
		[Column(Name = nameof(PartitionId), IsPrimaryKey = true, DbType = "varchar(10) NOT NULL")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public string DbPartitionId
		{
			get { return PartitionId; }
			set { PartitionId = value; }
		}

		/// <summary>
		/// Gets or sets the host owner for the partition.
		/// </summary>
		/// <remarks>
		/// Required to place the Column attribute for SQL compatibility.
		/// </remarks>
		[Column(Name = nameof(Owner), DbType = "varchar(100) NULL")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public string DbOwner
		{
			get { return Owner; }
			set { Owner = value; }
		}

		/// <summary>
		/// Gets or sets the lease token that manages concurrency between hosts. You can use this token to guarantee
		/// single access to any resource needed by the <see cref="T:Microsoft.Azure.EventHubs.Processor.IEventProcessor" />
		/// object.
		/// </summary>
		/// <remarks>
		/// Required to place the Column attribute for SQL compatibility.
		/// </remarks>
		[Column(Name = nameof(Token), DbType = "varchar(50) NULL")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public string DbToken
		{
			get { return Token; }
			set { Token = value; }
		}

		/// <summary>
		/// Gets or sets the epoch year of the lease, which is a value you can use to determine the most recent owner
		/// of a partition between competing nodes.
		/// </summary>
		/// <remarks>
		/// Required to place the Column attribute for SQL compatibility.
		/// </remarks>
		[Column(Name = nameof(Epoch), DbType = "bigint NOT NULL")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public long DbEpoch
		{
			get { return Epoch; }
			set { Epoch = value; }
		}

		/// <summary>
		/// Gets or sets the current value for the offset in the stream.
		/// </summary>
		/// <remarks>
		/// Required to place the Column attribute for SQL compatibility.
		/// </remarks>
		[Column(Name = nameof(Offset), DbType = "varchar(50) NULL")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public string DbOffset
		{
			get { return Offset; }
			set { Offset = value; }
		}

		/// <summary>
		/// Gets or sets the last checkpointed sequence number in the stream.
		/// </summary>
		/// <remarks>
		/// Required to place the Column attribute for SQL compatibility.
		/// </remarks>
		[Column(Name = nameof(SequenceNumber), DbType = "bigint NOT NULL")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public long DbSequenceNumber
		{
			get { return SequenceNumber; }
			set { SequenceNumber = value; }
		}

		/// <summary>
		/// Gets or sets the time at which this lease was acquired. It is cosmetic only, since ExpiresAt is leading.
		/// </summary>
		[Column(Name = nameof(AcquiredAt), DbType = "datetimeoffset(7) NULL")]
		public DateTimeOffset? AcquiredAt { get; set; }

		/// <summary>
		/// Gets or sets the time at which this lease will expire.
		/// </summary>
		[Column(Name = nameof(ExpiresAt), DbType = "datetimeoffset(7) NULL")]
		public DateTimeOffset? ExpiresAt { get; set; }

		/// <summary>
		/// Gets a value that indicates if this lease has expired.
		/// </summary>
		/// <returns>True if it has expired, false if not.</returns>
		public override Task<bool> IsExpired()
		{
			return Task.FromResult(!ExpiresAt.HasValue || DateTimeOffset.Now > ExpiresAt.Value);
		}
		#endregion

		#region Constructors & Destructors
		/// <summary>
		/// Creates a new SqlLease. This constructor creates a new lease for use in serialization.
		/// </summary>
		protected SqlLease()
		{
		}

		/// <summary>
		/// Creates a new SqlLease. This constructor creates a new lease from scratch.
		/// </summary>
		/// <param name="partitionId">The id of the partition.</param>
		protected SqlLease(string partitionId)
			: base(partitionId)
		{
		}

		/// <summary>
		/// Creates a new SqlLease. This constructor wraps the provided lease with additional functionality.
		/// </summary>
		/// <param name="source">The original Lease.</param>
		protected SqlLease(Lease source)
			: base(source)
		{
		}
		#endregion

		#region Actions
		/// <summary>
		/// Acquires the lease, renewing it just to be sure and assigning the provided owner to it.
		/// </summary>
		/// <param name="owner">The name of the owner of the lease.</param>
		/// <param name="leaseDuration">The time after which the lease ownership expires.</param>
		public void Acquire(string owner, TimeSpan leaseDuration)
		{
			// Renew the lease so it's valid for a while.
			Renew(leaseDuration);

			Token = Guid.NewGuid().ToString("N");
			Owner = owner;
			IncrementEpoch();
		}

		/// <summary>
		/// Renews the lease, shifting its expiry date forward so that it is marked as in use for a while longer.
		/// </summary>
		/// <param name="leaseDuration">The time after which the lease ownership expires.</param>
		public void Renew(TimeSpan leaseDuration)
		{
			// Refresh the acquire timestamp and expire timestamp.
			AcquiredAt = DateTimeOffset.Now;
			ExpiresAt = AcquiredAt + leaseDuration;
		}

		/// <summary>
		/// Resets all ownership properties so the lease will indicate it is not in use by anyone.
		/// </summary>
		public void Release()
		{
			Token = null;

			// NOTE: Owner may not be null or the PartitionManager from the API crashes, because it tries to
			// create a dictionary of owners and partition counts.
			Owner = string.Empty;

			// NOTE: Expiration date MUST be reset, because most of the PartitionManager logic is based around
			// expiration. If the lease is released but IsExpired returns false for an hour, the lease will
			// not be held by anyone for an hour, not even through stealing.
			AcquiredAt = null;
			ExpiresAt = null;
		}
		#endregion
	}
}