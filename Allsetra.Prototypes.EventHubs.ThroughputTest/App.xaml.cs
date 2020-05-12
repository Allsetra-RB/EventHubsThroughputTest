using Allsetra.Prototypes.EventHubs.ThroughputTest.General;
using Allsetra.Utilities.Threading;
using log4net.Config;
using System;
using System.Windows;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest
{
	public partial class App : Application
	{
		public static readonly DateTime ApplicationStart = DateTime.Now;
		public static readonly InstanceThreadPool ThreadPool = new InstanceThreadPool();

		public App()
		{
			AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
			AppDomain.CurrentDomain.ProcessExit += ProcessExitHandler;

			// Configure and initiate Log4Net. This sets it up from the app.config.
			XmlConfigurator.Configure();
			Data.Info(nameof(App), "Application started");
		}

		private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
		{
			Data.Fatal( nameof(App), "Unhandled exception.", e.ExceptionObject as Exception );
		}

		private void ProcessExitHandler(object sender, EventArgs e)
		{
			Data.Info(nameof(App), "Application exitted.");
		}
	}
}