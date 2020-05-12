using Allsetra.Prototypes.EventHubs.ThroughputTest.General;
using Allsetra.Prototypes.EventHubs.ThroughputTest.Models;
using Microsoft.Azure.EventHubs;
using Microsoft.ServiceBus;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest.EventHubs
{
	public class EventHubsWriter : IRunnable
	{
		#region Properties
		private static int NextId = 1000;

		private bool _isRunning;

		public int Id { get; } = ++NextId;

		public string Name
		{
			get { return $"{nameof(EventHubsWriter)}_{Id}"; }
		}
		#endregion

		#region Running
		public Task Start()
		{
			_isRunning = true;
			Task.Run( () => Run() );
			return Task.CompletedTask;
		}

		public Task Stop()
		{
			_isRunning = false;
			return Task.CompletedTask;
		}
		#endregion

		#region Writing
		private void Run()
		{
			var connectionStringBuilder = new ServiceBusConnectionStringBuilder( Settings.EventHubConnectionString )
				{ EntityPath = Settings.EventHubName, };
			var eventHubClient = EventHubClient.CreateFromConnectionString( connectionStringBuilder.ToString() );

			while ( _isRunning )
			{
				Stat stat = new Stat
				{
					Id = Id,
					Owner = Name,
					Name = Name,
					IsReader = false,
					ProcessedMessages = Settings.WriteBatchSize,
				};

				try
				{
					for ( int i = 0; i < Settings.WriteBatchSize; i++ )
					{
						eventHubClient.SendAsync(GetEvent());
						Thread.Sleep(Settings.WriterSleep);
					}
				}
				catch (Exception e)
				{
					Data.Error(Name, "Could not process messages.", e);
				}
				finally
				{
					Data.Record(stat);
				}
			}
		}

		private EventData GetEvent()
		{
			return new EventData( Encoding.UTF8.GetBytes( JsonConvert.SerializeObject( Message.GetSimpleMessage() ) ) );
		}
		#endregion
	}
}
