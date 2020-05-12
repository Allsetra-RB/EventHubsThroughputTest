namespace Allsetra.Prototypes.EventHubs.ThroughputTest.Models
{
	public class GlobalStats
	{
		public int LifetimeMessagesWritten { get; set; }
		public int LifetimeMessagesRead { get; set; }

		public double MaxMessagesWrittenPerSecond { get; set; }
		public double MaxMessagesReadPerSecond { get; set; }
		public double MaxCheckpointLatency { get; set; }
	}
}