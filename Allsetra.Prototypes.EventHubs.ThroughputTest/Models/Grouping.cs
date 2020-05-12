namespace Allsetra.Prototypes.EventHubs.ThroughputTest.Models
{
	public class Grouping<TKey, TValue>
	{
		public TKey Key { get; set; }
		public TValue Value { get; set; }
	}
}