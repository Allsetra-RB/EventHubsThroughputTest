using Allsetra.Prototypes.EventHubs.ThroughputTest.General;
using Allsetra.Prototypes.EventHubs.ThroughputTest.Models;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest.ServiceBus
{
	public class ServiceBusReader : IRunnable, IEventProcessor
	{
		#region Properties
		private static int NextId = 1000;

		private EventProcessorHost _eventProcessorHost;
		private Stopwatch _checkpointStopWatch;
		private Stopwatch _lastRunStopWatch;
		private bool _isRunning;

		public int Id { get; } = ++NextId;

		public string Name
		{
			get { return $"{nameof(ServiceBusReader)}_{Process.GetCurrentProcess().Id}_{Id}"; }
		}
		#endregion

		#region Running
		public async Task Start()
		{
			_isRunning = true;
			_eventProcessorHost = new EventProcessorHost( Name,
														 Settings.EventHubName,
														 Settings.EventHubConsumerGroup,
														 Settings.EventHubConnectionString,
														 Settings.LeaseConnectionString )
			{
				PartitionManagerOptions = new PartitionManagerOptions
				{
					AcquireInterval = Settings.AcquireLeaseEvery,
					RenewInterval = Settings.RenewLeaseEvery,
					LeaseInterval = Settings.ExpireLeaseEvery,
				},
			};
			EventProcessorOptions options = new EventProcessorOptions
			{
				MaxBatchSize = Settings.ReadBatchSize,
				PrefetchCount = Settings.ReadPrefetchCount,
				ReceiveTimeOut = Settings.ReadReceiveTimeout,
				EnableReceiverRuntimeMetric = Settings.ReadEnableReceiverRuntimeMetric,
				InvokeProcessorAfterReceiveTimeout = false,
			};
			options.ExceptionReceived += EventProcessorOptions_ExceptionReceivedHandler;

			await _eventProcessorHost.RegisterEventProcessorAsync<ServiceBusReader>( options ).ConfigureAwait( false );
		}

		public async Task Stop()
		{
			if ( _eventProcessorHost == null )
			{
				return;
			}

			try
			{
				await _eventProcessorHost.UnregisterEventProcessorAsync().ConfigureAwait( false );
			}
			catch ( Exception e )
			{
				Data.Error( Name, "Could not unregister processor.", e );
			}
			finally
			{
				_eventProcessorHost.Dispose();
				_eventProcessorHost = null;
			}

			_isRunning = false;
		}
		#endregion

		#region EventProcessorHost
		public Task OpenAsync( PartitionContext context )
		{
			Data.RegisterPartitionOwner( context.Lease.Owner, int.Parse( context.Lease.PartitionId ) );

			_checkpointStopWatch = new Stopwatch();
			_checkpointStopWatch.Start();
			_lastRunStopWatch = new Stopwatch();
			_lastRunStopWatch.Start();

			return Task.CompletedTask;
		}

		public async Task CloseAsync( PartitionContext context, CloseReason reason )
		{
			Data.UnregisterPartitionOwner( context.Lease.Owner, int.Parse( context.Lease.PartitionId ) );

			try
			{
				// An exception may be caused here if the reader shuts down gracefully, but another (greedy) reader
				// steals the partition. We can then no longer create the checkpoint, but we should continue the rest
				// of our closing logic, such as unsubscribing from the partition.
				switch ( reason )
				{
					case CloseReason.Shutdown:
						await context.CheckpointAsync().ConfigureAwait( false );
						break;
				}
			}
			catch ( Exception e )
			{
				Data.Error( Name, "Failed to create checkpoint on shutdown.", e );
			}
		}

		public async Task ProcessEventsAsync( PartitionContext context, IEnumerable<EventData> events )
		{
			_lastRunStopWatch.Stop();

			List<EventData> messages = events.ToList();
			Stat stat = new Stat
			{
				Id = Id,
				Owner = context.Lease.Owner,
				Name = Name,
				IsReader = true,
				// NOTE: PartitionContext.RuntimeInfo.PartitionId can be null (after restart?)! RuntimeInfo is affected by EnableRuntimeMetrics.
				PartitionId = int.Parse( context.Lease.PartitionId ),
				ProcessedMessages = messages.Count,
				RerunLatency = (int)_lastRunStopWatch.ElapsedMilliseconds,
			};

			try
			{
				stat.StartBatch();

				int grouper = (int)Math.Max( 1, Math.Ceiling( messages.Count / (double)Settings.ReaderMessageBatchSize ) );
				List<Func<Task>> taskFuncs =
					messages.
						Select( ( x, i ) => new { Index = i, Item = x, } ).
						GroupBy( x => x.Index / grouper ).
						Select( x => Get( async () =>
							   {
								   foreach ( var y in x )
								   {
									   await ProcessMessage( context, y.Item, ParseMessage( y.Item ) ).ConfigureAwait( false );
								   }
							   } )
							  ).
						ToList();
				List<Task> tasks = Settings.UseInstanceThreadPool ?
					taskFuncs.Select( x => App.ThreadPool.QueueWorkItem( x ) ).ToList() :
					taskFuncs.Select( x => x() ).ToList();
				await Task.WhenAll( tasks ).ConfigureAwait( false );

				if ( _checkpointStopWatch.Elapsed > TimeSpan.FromSeconds( Settings.ReaderCheckpointEvery ) )
				{
					try
					{
						stat.StartCheckpoint();
						await context.CheckpointAsync().ConfigureAwait( false );
					}
					catch ( Exception e )
					{
						Data.Error( Name, "Could not checkpoint.", e );
					}
					finally
					{
						stat.StopCheckpoint();
						_checkpointStopWatch.Restart();
					}
				}
			}
			catch ( Exception e )
			{
				Data.Error( Name, "Could not process messages.", e );
			}
			finally
			{
				stat.StopBatch();
				Data.Record( stat );
			}

			await Task.Delay( Settings.ReaderSleep ).ConfigureAwait( false );

			_lastRunStopWatch.Restart();
		}

		private void EventProcessorOptions_ExceptionReceivedHandler( object sender, ExceptionReceivedEventArgs e )
		{
			Data.Error( Name, $"Unhandled receive error while '{e.Action}'.", e.Exception );
		}

		private Message ParseMessage( EventData @event )
		{
			string json = Encoding.UTF8.GetString( @event.GetBytes() );
			return JsonConvert.DeserializeObject<Message>( json );
		}

		private async Task ProcessMessage( PartitionContext partitionContext, EventData rawMessage, Message message )
		{
			if ( Settings.UseBlockingWork )
			{
				Thread.Sleep( Settings.ReaderMessageDuration );
			}
			else
			{
				await Task.Delay( Settings.ReaderMessageDuration ).ConfigureAwait( false );
			}
		}

		private Func<Task> Get( Func<Task> x )
		{
			return x;
		}
		#endregion
	}
}