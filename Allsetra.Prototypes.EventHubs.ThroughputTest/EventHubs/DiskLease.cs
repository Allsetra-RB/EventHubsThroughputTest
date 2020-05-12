using Microsoft.Azure.EventHubs.Processor;
using System;
using System.Threading.Tasks;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest.EventHubs
{
	/// <summary>
	/// A lease used in the disk lease manager.
	/// </summary>
	public class DiskLease : Lease
	{
		#region Properties
		/// <summary>
		/// Gets or sets the date at which the lease was acquired. It is used to determine if the lease has expired or not.
		/// </summary>
		public DateTime? AcquiredAt { get; set; }
		/// <summary>
		/// Gets or sets the lease duration. If this duration has passed, relative from AcquiredAt, the lease has expired.
		/// </summary>
		public TimeSpan LeaseDuration { get; set; }

		/// <summary>
		/// Gets a value that indicates if the lease is expired or not.
		/// </summary>
		public override Task<bool> IsExpired()
		{
			return Task.FromResult(!AcquiredAt.HasValue ||
								   DateTime.UtcNow - AcquiredAt.Value > LeaseDuration);
		}
		#endregion

		#region Constructors & Destructors
		/// <summary>
		/// Creates a new DiskLease. This constructor is only for serialization.
		/// </summary>
		public DiskLease()
		{
		}

		/// <summary>
		/// Creates a new DiskLease. This constructor creates a new lease from scratch.
		/// </summary>
		/// <param name="partitionId">The partition id.</param>
		public DiskLease(string partitionId)
			: base(partitionId)
		{
		}

		/// <summary>
		/// Creates a new DiskLease. This constructor wraps a basic lease with additional functionality.
		/// </summary>
		/// <param name="source">The lease to wrap.</param>
		public DiskLease(Lease source)
			: base(source)
		{
		}
		#endregion

		#region Actions
		/// <summary>
		/// Acquires the lease, renewing it just to be sure and assigning the provided owner to it.
		/// owner.
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
			// Refresh the acquire timestamp and lease duration (just in case).
			AcquiredAt = DateTime.UtcNow;
			LeaseDuration = leaseDuration;
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

			// NOTE: Acquire date MUST be reset, because most of the PartitionManager logic is based around
			// expiration. If the lease is released but IsExpired returns false for an hour, the lease will
			// not be held by anyone for an hour, not even through stealing.
			AcquiredAt = null;
		}
		#endregion
	}
}