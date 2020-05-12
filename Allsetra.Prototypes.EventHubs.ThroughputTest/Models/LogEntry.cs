using System;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest.Models
{
	public class LogEntry
	{
		public DateTime Timestamp { get; } = DateTime.Now;
		public Severity Severity { get; set; }
		public string Originator { get; set; }
		public string Message { get; set; }
	}
}