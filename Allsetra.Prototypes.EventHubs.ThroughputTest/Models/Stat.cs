using System;
using System.Diagnostics;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest.Models
{
	public class Stat
	{
		private DateTime _timestamp;

		private Stopwatch _batchStopwatch;
		private Stopwatch _processingStopwatch;
		private Stopwatch _checkpointStopwatch;

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

		public int Id { get; set; } 
		public string Owner { get; set; }
		public string Name { get; set; } 
		public bool IsReader { get; set; } 
		public int PartitionId { get; set; }

		public int ProcessedMessages { get; set; }

		public int BatchDuration { get; set; }
		public int ProcessingDuration { get; set; }
		public int CheckpointDuration { get; set; }
		public int RerunLatency { get; set; }

		public Stat()
		{
			Timestamp = DateTime.Now;
		}

		public void StartBatch()
		{
			_batchStopwatch = new Stopwatch();
			_batchStopwatch.Start();
		}

		public void StopBatch()
		{
			if ( _batchStopwatch == null )
			{
				return;
			}

			_batchStopwatch.Stop();
			BatchDuration = (int)_batchStopwatch.ElapsedMilliseconds;
		}

		public void StartProcessing()
		{
			_processingStopwatch = new Stopwatch();
			_processingStopwatch.Start();
		}

		public void StopProcessing()
		{
			if (_processingStopwatch == null)
			{
				return;
			}

			_processingStopwatch.Stop();
			ProcessingDuration = (int)_processingStopwatch.ElapsedMilliseconds;
		}

		public void StartCheckpoint()
		{
			_checkpointStopwatch = new Stopwatch();
			_checkpointStopwatch.Start();
		}

		public void StopCheckpoint()
		{
			if (_checkpointStopwatch == null)
			{
				return;
			}

			_checkpointStopwatch.Stop();
			CheckpointDuration = (int)_checkpointStopwatch.ElapsedMilliseconds;
		}
	}
}