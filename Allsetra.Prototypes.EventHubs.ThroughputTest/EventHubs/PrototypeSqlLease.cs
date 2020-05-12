using Microsoft.Azure.EventHubs.Processor;
using System.Data.Linq.Mapping;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest.EventHubs
{
	[Table(Name = "[azure].[PrototypeLeases]")]
	public class PrototypeSqlLease : SqlLease
	{
		public PrototypeSqlLease()
		{
		}

		public PrototypeSqlLease( string partitionId )
			: base( partitionId )
		{
		}

		public PrototypeSqlLease( Lease source )
			: base( source )
		{
		}
	}
}