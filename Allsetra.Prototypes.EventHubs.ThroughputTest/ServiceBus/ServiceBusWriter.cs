using Allsetra.Prototypes.EventHubs.ThroughputTest.General;
using Allsetra.Prototypes.EventHubs.ThroughputTest.Models;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest.ServiceBus
{
	public class ServiceBusWriter : IRunnable
	{
		#region Properties
		private static int NextId = 1000;

		private bool _isRunning;

		public int Id { get; } = ++NextId;

		public string Name
		{
			get { return $"{nameof(ServiceBusWriter)}_{Id}"; }
		}
		#endregion

		#region Running
		public Task Start()
		{
			_isRunning = true;
			ThreadPool.QueueUserWorkItem( x => Run() );
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
			var eventHubClient = EventHubClient.CreateFromConnectionString(Settings.EventHubConnectionString, Settings.EventHubName);

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
						eventHubClient.SendAsync( GetEvent() );
						Thread.Sleep( Settings.WriterSleep );
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
			return new EventData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Message.GetSimpleMessage())));
		}
		#endregion
	}
}