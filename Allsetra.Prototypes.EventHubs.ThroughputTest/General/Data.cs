using Allsetra.Prototypes.EventHubs.ThroughputTest.Models;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Timers;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest.General
{
	public static class Data
	{
		#region Properties
		private static readonly ILog DiskLog = LogManager.GetLogger(nameof(App));
		private static readonly ILog DiskStatsLog = LogManager.GetLogger(nameof(Data));
		private static readonly Timer LargeObjectHeapGarbageCollectionTimer = new Timer(3000);
		private static readonly Timer DiskLogStatsTimer = new Timer(10000);

		private static readonly GlobalStats GlobalStats = new GlobalStats();

		public static ConcurrentFixedQueue<LogEntry> Log { get; } = new ConcurrentFixedQueue<LogEntry>(100);
		public static ConcurrentFixedQueue<Stat> Stats { get; } = new ConcurrentFixedQueue<Stat>(100000);
		public static ConcurrentBag<PartitionOwner> PartitionOwnerStats { get; } = new ConcurrentBag<PartitionOwner>();
		public static ConcurrentFixedQueue<GenericStat> UserChangeStats { get; } = new ConcurrentFixedQueue<GenericStat>(20000);
		public static ConcurrentFixedQueue<GenericStat> SystemStateStats { get; } = new ConcurrentFixedQueue<GenericStat>(20000);

		public static IEnumerable<IGrouping<int, Stat>> StatsBySecond
		{
			get
			{
				int min = Stats.Min( x => (int?)x.Second ) ?? 0;
				int max = Stats.Max( x => (int?)x.Second ) ?? int.MaxValue;

				// Group all data by 'second since application start' to get a nice and reliable x axis, and filter out
				// the start and end second of data, because they will contain partial data that will make the graph 'jump'
				// when auto-adjusting and/or 'wiggle' at the start and end points.
				return Stats.ToList().
					Where( x => x.Second > min && x.Second < max ).
					GroupBy( x => x.Second );
			}
		}

		public static IEnumerable<IGrouping<int, Stat>> ReaderStatsBySecond
		{
			get
			{
				int min = Stats.Min( x => (int?)x.Second ) ?? 0;
				int max = Stats.Max( x => (int?)x.Second ) ?? int.MaxValue;

				// Group all data by 'second since application start' to get a nice and reliable x axis, and filter out
				// the start and end second of data, because they will contain partial data that will make the graph 'jump'
				// when auto-adjusting and/or 'wiggle' at the start and end points.
				return Stats.ToList().
					Where( x => x.IsReader && x.Second > min && x.Second < max ).
					GroupBy( x => x.Second );
			}
		}

		public static IEnumerable<Grouping<bool, List<IGrouping<int, Stat>>>> StatsByTypeAndSecond
		{
			get
			{
				int min = Stats.Min(x => (int?)x.Second) ?? 0;
				int max = Stats.Max(x => (int?)x.Second) ?? int.MaxValue;

				// Filter out the start and end second of data, because they will contain partial data that will make the
				// graph 'jump' when auto-adjusting and/or 'wiggle' at the start and end points.
				return Stats.ToList().
					Where( x => x.Second > min && x.Second < max ).
					GroupBy( x => x.IsReader ).
					Select( x => new Grouping<bool, List<IGrouping<int, Stat>>> { Key = x.Key, Value = x.GroupBy( y => y.Second ).ToList(), } );
			}
		}

		public static IEnumerable<Grouping<int, List<IGrouping<int, Stat>>>> StatsByPartitionAndSecond
		{
			get
			{
				int min = Stats.Min(x => (int?)x.Second) ?? 0;
				int max = Stats.Max(x => (int?)x.Second) ?? int.MaxValue;

				// Filter out the start and end second of data, because they will contain partial data that will make the
				// graph 'jump' when auto-adjusting and/or 'wiggle' at the start and end points.
				return Stats.ToList().
					Where( x => x.IsReader && x.Second > min && x.Second < max ).
					GroupBy( x => x.PartitionId ).
					OrderBy( x => x.Key ).
					Select( x => new Grouping<int, List<IGrouping<int, Stat>>> { Key = x.Key, Value = x.GroupBy( y => y.Second ).ToList(), } );
			}
		}

		// NOTE: Mostly for debugging.
		public static IEnumerable<IGrouping<int, Stat>> StatsByPartition
		{
			get
			{
				return Stats.ToList().
					Where( x => x.IsReader ).
					GroupBy( x => x.PartitionId ).
					OrderBy(x => x.Key);
			}
		}
		#endregion

		#region Constructors & Destructors
		static Data()
		{
			LargeObjectHeapGarbageCollectionTimer.Elapsed += LargeObjectHeapGarbageCollectionTimer_ElapsedHandler;
			LargeObjectHeapGarbageCollectionTimer.Start();
			DiskLogStatsTimer.Elapsed += DiskLogStatsTimer_ElapsedHandler;
			DiskLogStatsTimer.Start();
		}
		#endregion

		#region Stats Recording
		public static void Record(Stat stat)
		{
			Stats.Enqueue(stat);

			if ( stat.IsReader )
			{
				GlobalStats.LifetimeMessagesRead += stat.ProcessedMessages;

				RegisterPartitionOwner( stat.Owner, stat.PartitionId );
			}
			else
			{
				GlobalStats.LifetimeMessagesWritten += stat.ProcessedMessages;
			}
		}

		public static void RecordSetting(string name, int value)
		{
			UserChangeStats.Enqueue(new GenericStat { Name = name, Value = value, });
		}

		public static void RecordSettingChange(string name, int previousValue, int newValue)
		{
			RecordSetting( name, newValue );

			if ( previousValue != newValue )
			{
				Warn( nameof(Data), $"Changed {name} from {previousValue} to {newValue}." );
			}
		}

		public static void RecordSystem(string name, int value)
		{
			SystemStateStats.Enqueue(new GenericStat { Name = name, Value = value, });
		}

		public static void RegisterPartitionOwner( string owner, int partitionId )
		{
			PartitionOwner partitionOwner = PartitionOwnerStats.FirstOrDefault(x => x.Name == owner );

			// Double checked locking for efficiency and to prevent duplicate owners, since this call is executed in a
			// highly multithreaded environment.
			if ( partitionOwner == null )
			{
				lock (PartitionOwnerStats)
				{
					partitionOwner = PartitionOwnerStats.FirstOrDefault(x => x.Name == owner );

					if (partitionOwner == null)
					{
						partitionOwner = new PartitionOwner { Name = owner, };
						PartitionOwnerStats.Add(partitionOwner);
					}
				}
			}

			switch (partitionId)
			{
				case 00: partitionOwner.HasPartition00Since = DateTime.Now; break;
				case 01: partitionOwner.HasPartition01Since = DateTime.Now; break;
				case 02: partitionOwner.HasPartition02Since = DateTime.Now; break;
				case 03: partitionOwner.HasPartition03Since = DateTime.Now; break;
				case 04: partitionOwner.HasPartition04Since = DateTime.Now; break;
				case 05: partitionOwner.HasPartition05Since = DateTime.Now; break;
				case 06: partitionOwner.HasPartition06Since = DateTime.Now; break;
				case 07: partitionOwner.HasPartition07Since = DateTime.Now; break;
				case 08: partitionOwner.HasPartition08Since = DateTime.Now; break;
				case 09: partitionOwner.HasPartition09Since = DateTime.Now; break;
				case 10: partitionOwner.HasPartition10Since = DateTime.Now; break;
				case 11: partitionOwner.HasPartition11Since = DateTime.Now; break;
				case 12: partitionOwner.HasPartition12Since = DateTime.Now; break;
				case 13: partitionOwner.HasPartition13Since = DateTime.Now; break;
				case 14: partitionOwner.HasPartition14Since = DateTime.Now; break;
				case 15: partitionOwner.HasPartition15Since = DateTime.Now; break;
				case 16: partitionOwner.HasPartition16Since = DateTime.Now; break;
				case 17: partitionOwner.HasPartition17Since = DateTime.Now; break;
				case 18: partitionOwner.HasPartition18Since = DateTime.Now; break;
				case 19: partitionOwner.HasPartition19Since = DateTime.Now; break;
				case 20: partitionOwner.HasPartition20Since = DateTime.Now; break;
				case 21: partitionOwner.HasPartition21Since = DateTime.Now; break;
				case 22: partitionOwner.HasPartition22Since = DateTime.Now; break;
				case 23: partitionOwner.HasPartition23Since = DateTime.Now; break;
				case 24: partitionOwner.HasPartition24Since = DateTime.Now; break;
				case 25: partitionOwner.HasPartition25Since = DateTime.Now; break;
				case 26: partitionOwner.HasPartition26Since = DateTime.Now; break;
				case 27: partitionOwner.HasPartition27Since = DateTime.Now; break;
				case 28: partitionOwner.HasPartition28Since = DateTime.Now; break;
				case 29: partitionOwner.HasPartition29Since = DateTime.Now; break;
				case 30: partitionOwner.HasPartition30Since = DateTime.Now; break;
				case 31: partitionOwner.HasPartition31Since = DateTime.Now; break;
			}
		}

		public static void UnregisterPartitionOwner( string owner, int partitionId )
		{
			PartitionOwner partitionOwner = PartitionOwnerStats.FirstOrDefault(x => x.Name == owner);

			if (partitionOwner == null)
			{
				partitionOwner = new PartitionOwner { Name = owner, };
				PartitionOwnerStats.Add(partitionOwner);
			}

			switch (partitionId)
			{
				case 00: partitionOwner.HasPartition00Since = null; break;
				case 01: partitionOwner.HasPartition01Since = null; break;
				case 02: partitionOwner.HasPartition02Since = null; break;
				case 03: partitionOwner.HasPartition03Since = null; break;
				case 04: partitionOwner.HasPartition04Since = null; break;
				case 05: partitionOwner.HasPartition05Since = null; break;
				case 06: partitionOwner.HasPartition06Since = null; break;
				case 07: partitionOwner.HasPartition07Since = null; break;
				case 08: partitionOwner.HasPartition08Since = null; break;
				case 09: partitionOwner.HasPartition09Since = null; break;
				case 10: partitionOwner.HasPartition10Since = null; break;
				case 11: partitionOwner.HasPartition11Since = null; break;
				case 12: partitionOwner.HasPartition12Since = null; break;
				case 13: partitionOwner.HasPartition13Since = null; break;
				case 14: partitionOwner.HasPartition14Since = null; break;
				case 15: partitionOwner.HasPartition15Since = null; break;
				case 16: partitionOwner.HasPartition16Since = null; break;
				case 17: partitionOwner.HasPartition17Since = null; break;
				case 18: partitionOwner.HasPartition18Since = null; break;
				case 19: partitionOwner.HasPartition19Since = null; break;
				case 20: partitionOwner.HasPartition20Since = null; break;
				case 21: partitionOwner.HasPartition21Since = null; break;
				case 22: partitionOwner.HasPartition22Since = null; break;
				case 23: partitionOwner.HasPartition23Since = null; break;
				case 24: partitionOwner.HasPartition24Since = null; break;
				case 25: partitionOwner.HasPartition25Since = null; break;
				case 26: partitionOwner.HasPartition26Since = null; break;
				case 27: partitionOwner.HasPartition27Since = null; break;
				case 28: partitionOwner.HasPartition28Since = null; break;
				case 29: partitionOwner.HasPartition29Since = null; break;
				case 30: partitionOwner.HasPartition30Since = null; break;
				case 31: partitionOwner.HasPartition31Since = null; break;
			}
		}
		#endregion

		#region Logging
		private static void AddLog( Severity severity, string originator, string message )
		{
			Log.Enqueue( new LogEntry { Severity = severity, Originator = originator, Message = message, } );
		}

		public static void Debug( string originator, string message )
		{
			AddLog( Severity.Debug, originator, message );
			DiskLog.Debug( $"{originator}: {message}" );
		}

		public static void Info( string originator, string message )
		{
			AddLog( Severity.Info, originator, message );
			DiskLog.Info( $"{originator}: {message}" );
		}

		public static void Warn( string originator, string message )
		{
			AddLog( Severity.Warn, originator, message );
			DiskLog.Warn( $"{originator}: {message}" );
		}

		public static void Error( string originator, string message )
		{
			AddLog( Severity.Error, originator, message );
			DiskLog.Error( $"{originator}: {message}" );
		}

		public static void Error( string originator, string message, Exception exception )
		{
			AddLog( Severity.Error, originator, message );
			DiskLog.Error( $"{originator}: {message}", exception );
		}

		public static void Fatal( string originator, string message )
		{
			AddLog( Severity.Fatal, originator, message );
			DiskLog.Fatal( $"{originator}: {message}" );
		}

		public static void Fatal(string originator, string message, Exception exception)
		{
			AddLog(Severity.Error, originator, message);
			DiskLog.Fatal($"{originator}: {message}", exception);
		}
		#endregion

		#region Events
		private static void LargeObjectHeapGarbageCollectionTimer_ElapsedHandler(object sender, ElapsedEventArgs e)
		{
			try
			{
				LargeObjectHeapGarbageCollectionTimer.Stop();

				GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
				GC.Collect();
			}
			catch (Exception ex)
			{
				Error(nameof(Data), "Could not compact heap memory.", ex);
			}
			finally
			{
				LargeObjectHeapGarbageCollectionTimer.Start();
			}
		}

		private static void DiskLogStatsTimer_ElapsedHandler( object sender, ElapsedEventArgs e )
		{
			try
			{
				DiskLogStatsTimer.Stop();

				List<Stat> stats = Stats.ToList();

				double minMessagesWritten = stats.Where( x => !x.IsReader ).GroupBy( x => x.Second ).Min( x => x.Sum( y => (int?)y.ProcessedMessages ) ) ?? 0;
				double avgMessagesWritten = stats.Where( x => !x.IsReader ).GroupBy( x => x.Second ).Average( x => x.Sum( y => (int?)y.ProcessedMessages ) ) ?? 0;
				double maxMessagesWritten = stats.Where( x => !x.IsReader ).GroupBy( x => x.Second ).Max( x => x.Sum( y => (int?)y.ProcessedMessages ) ) ?? 0;

				double minMessagesRead = stats.Where( x => x.IsReader ).GroupBy( x => x.Second ).Min( x => x.Sum( y => (int?)y.ProcessedMessages ) ) ?? 0;
				double avgMessagesRead = stats.Where( x => x.IsReader ).GroupBy( x => x.Second ).Average( x => x.Sum( y => (int?)y.ProcessedMessages ) ) ?? 0;
				double maxMessagesRead = stats.Where( x => x.IsReader ).GroupBy( x => x.Second ).Max( x => x.Sum( y => (int?)y.ProcessedMessages ) ) ?? 0;

				double minCheckpointLatency = stats.Where( x => x.IsReader ).Min( x => (int?)x.CheckpointDuration ) ?? 0;
				double avgCheckpointLatency = stats.Where( x => x.IsReader ).Average( x => (int?)x.CheckpointDuration ) ?? 0;
				double maxCheckpointLatency = stats.Where( x => x.IsReader ).Max( x => (int?)x.CheckpointDuration ) ?? 0;

				GlobalStats.MaxMessagesWrittenPerSecond = Math.Max( GlobalStats.MaxMessagesWrittenPerSecond, maxMessagesWritten );
				GlobalStats.MaxMessagesReadPerSecond = Math.Max( GlobalStats.MaxMessagesReadPerSecond, maxMessagesRead );
				GlobalStats.MaxCheckpointLatency = Math.Max( GlobalStats.MaxCheckpointLatency, maxCheckpointLatency );

				DiskStatsLog.Info( $"Data s# {stats.Count}, ucs# {UserChangeStats.Count()}, l# {Log.Count()} | " +
								   $"Global m- {GlobalStats.LifetimeMessagesWritten}, m- {GlobalStats.LifetimeMessagesRead}, m/s+ {GlobalStats.MaxMessagesWrittenPerSecond}, m/s- {GlobalStats.MaxMessagesReadPerSecond}, chck. {GlobalStats.MaxCheckpointLatency} | " +
								   $"m/s+ min {minMessagesWritten}, avg {avgMessagesWritten}, max {maxMessagesWritten} | " +
								   $"m/s- min {minMessagesRead}, avg {avgMessagesRead}, max {maxMessagesRead} | " +
								   $"chck. min {minCheckpointLatency}, avg {avgCheckpointLatency}, max {maxCheckpointLatency}" );
			}
			catch ( Exception ex )
			{
				Error( nameof(Data), "Could not log stats to file.", ex );
			}
			finally
			{
				DiskLogStatsTimer.Start();
			}
		}
		#endregion
	}
}
