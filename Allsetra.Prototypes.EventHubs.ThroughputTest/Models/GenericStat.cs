using System;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest.Models
{
	public class GenericStat
	{
		private DateTime _timestamp;

		public DateTime Timestamp
		{
			get { return _timestamp; }
			private set
			{
				_timestamp = value;
				Second = (int)( Timestamp - App.ApplicationStart ).TotalSeconds;
			}
		}
		public int Second { get; private set; }
		public string Name { get; set; }
		public int Value { get; set; }

		public GenericStat()
		{
			Timestamp = DateTime.Now;
		}
	}
}