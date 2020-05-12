using Allsetra.Utilities.Helpers;
using System;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest.General
{
	public static class Settings
	{
		private static int _writerSleep = 1;
		private static int _writeBatchSize = 100;

		private static int _readerSleep = 0;
		private static int _readerMessageBatchSize = 1000;
		private static int _readerMessageDuration = 0;
		private static int _readBatchSize = 100;
		private static int _readerCheckpointEvery = 10;

		private static TimeSpan _acquireLeaseEvery = TimeSpan.FromSeconds( 1 );
		private static TimeSpan _renewLeaseEvery = TimeSpan.FromSeconds( 10 );
		private static TimeSpan _expireLeaseEvery = TimeSpan.FromSeconds( 30 );

		/// <summary>
		/// Indicates if the front-end should host Readers and Writers without running them. It is useful
		/// for debugging the front-end without accidentally starting all kinds of processing logic.
		/// </summary>
		public static readonly bool DisableRunning = false;

		public static readonly string EventHubConnectionString = Config.Get<string>( "EventHubConnectionString" );
		public static readonly string EventHubNamespace = Config.Get<string>( "EventHubNamespace" );
		public static readonly string EventHubName = Config.Get<string>( "EventHubName" );
		public static readonly string EventHubConsumerGroup = Config.Get<string>( "EventHubConsumerGroup" );
		public static readonly string LeaseConnectionString = Config.Get<string>( "LeaseConnectionString" );
		public static readonly string LeaseSqlConnectionString = Config.Get<string>("LeaseSqlConnectionString");

		public static int WriterSleep
		{
			get { return _writerSleep; }
			set { _writerSleep = Math.Max( 0, value ); }
		}

		public static int WriteBatchSize
		{
			get { return _writeBatchSize; }
			set { _writeBatchSize = Math.Max( 1, value ); }
		}

		public static int ReaderSleep
		{
			get { return _readerSleep; }
			set { _readerSleep = Math.Max( 0, value ); }
		}

		public static int ReaderMessageBatchSize
		{
			get { return _readerMessageBatchSize; }
			set { _readerMessageBatchSize = Math.Max( 1, value ); }
		}

		public static int ReaderMessageDuration
		{
			get { return _readerMessageDuration; }
			set { _readerMessageDuration = Math.Max( 0, value ); }
		}

		public static int ReadBatchSize
		{
			get { return _readBatchSize; }
			set { _readBatchSize = Math.Max( 1, value ); }
		}

		public static int ReadPrefetchCount { get; set; } = 1000;
		public static TimeSpan ReadReceiveTimeout { get; set; } = TimeSpan.FromSeconds( 30 );
		public static bool ReadEnableReceiverRuntimeMetric { get; set; } = true;

		public static TimeSpan AcquireLeaseEvery
		{
			get { return _acquireLeaseEvery; }
			set { _acquireLeaseEvery = TimeSpan.FromSeconds( Math.Max( 1, Math.Min( value.TotalSeconds, 600 ) ) ); }
		}

		public static TimeSpan RenewLeaseEvery
		{
			get { return _renewLeaseEvery; }
			set { _renewLeaseEvery = TimeSpan.FromSeconds( Math.Max( 1, Math.Min( value.TotalSeconds, 60 ) ) ); }
		}

		public static TimeSpan ExpireLeaseEvery
		{
			get { return _expireLeaseEvery; }
			set { _expireLeaseEvery = TimeSpan.FromSeconds( Math.Max( 15, Math.Min( value.TotalSeconds, 60 ) ) ); }
		}

		public static int ReaderCheckpointEvery
		{
			get { return _readerCheckpointEvery; }
			set { _readerCheckpointEvery = Math.Max(0, value); }
		}

		public static LeaseType LeaseType { get; set; } = LeaseType.AzureStorageAccount;
		public static bool UseServiceBus { get; set; } = true;

		public static bool UseBlockingWork { get; set; } = true;
		public static bool UseInstanceThreadPool { get; set; } = false;
	}
}
