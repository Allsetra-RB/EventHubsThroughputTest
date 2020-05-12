using Allsetra.Prototypes.EventHubs.ThroughputTest.EventHubs;
using Allsetra.Prototypes.EventHubs.ThroughputTest.General;
using Allsetra.Prototypes.EventHubs.ThroughputTest.Models;
using Allsetra.Prototypes.EventHubs.ThroughputTest.ServiceBus;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using Timer = System.Timers.Timer;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest
{
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		#region Properties
		public static readonly List<Brush> OwnerBrushes = new List<Brush>
		{
			new SolidColorBrush( Color.FromRgb( 1, 181, 170 ) ),
			new SolidColorBrush( Color.FromRgb( 55, 70, 73 ) ),
			new SolidColorBrush( Color.FromRgb( 253, 98, 94 ) ),
			new SolidColorBrush( Color.FromRgb( 242, 200, 15 ) ),
			new SolidColorBrush( Color.FromRgb( 95, 107, 109 ) ),
			new SolidColorBrush( Color.FromRgb( 138, 212, 235 ) ),
			new SolidColorBrush( Color.FromRgb( 254, 150, 102 ) ),
			new SolidColorBrush( Color.FromRgb( 166, 105, 153 ) ),
			new SolidColorBrush( Color.FromRgb( 53, 153, 184 ) ),
			new SolidColorBrush( Color.FromRgb( 223, 191, 191 ) ),
		};

		private bool _isBusy;

		public bool IsBusy
		{
			get { return _isBusy; }
			set
			{
				_isBusy = value;
				NotifyPropertyChanged();
			}
		}

		#region Reader/Writer Control
		public ObservableCollection<IRunnable> Writers { get; } = new ObservableCollection<IRunnable>();
		public ObservableCollection<IRunnable> Readers { get; } = new ObservableCollection<IRunnable>();

		public List<PartitionOwner> PartitionOwners
		{
			get { return _partitionOwners; }
			set
			{
				_partitionOwners = value;
				NotifyPropertyChanged();
			}
		}

		public int WriterSleep
		{
			get { return Settings.WriterSleep; }
			set
			{
				int previous = Settings.WriterSleep;
				Settings.WriterSleep = value;
				Data.RecordSettingChange( "Sleep (W)", previous, value );
			}
		}

		public int ReaderSleep
		{
			get { return Settings.ReaderSleep; }
			set
			{
				int previous = Settings.ReaderSleep;
				Settings.ReaderSleep = value;
				Data.RecordSettingChange( "Sleep (R)", previous, value );
			}
		}

		public int WriterBatchSize
		{
			get { return Settings.WriteBatchSize; }
			set
			{
				int previous = Settings.WriteBatchSize;
				Settings.WriteBatchSize = value;
				Data.RecordSettingChange( "Batch Size (W)", previous, value );
			}
		}

		public int ReaderBatchSize
		{
			get { return Settings.ReadBatchSize; }
			set
			{
				int previous = Settings.ReadBatchSize;
				Settings.ReadBatchSize = value;
				Data.RecordSettingChange( "Batch Size (R)", previous, value );

				if ( value != previous )
				{
					RestartReaders();
				}
			}
		}

		public int ReaderMessageBatchSize
		{
			get { return Settings.ReaderMessageBatchSize; }
			set
			{
				int previous = Settings.ReaderMessageBatchSize;
				Settings.ReaderMessageBatchSize = value;
				Data.RecordSettingChange( "Message Batch Size", previous, value );
			}
		}

		public int ReaderMessageDuration
		{
			get { return Settings.ReaderMessageDuration; }
			set
			{
				int previous = Settings.ReaderMessageDuration;
				Settings.ReaderMessageDuration = value;
				Data.RecordSettingChange( "Message Duration", previous, value );
			}
		}

		public int ReaderCheckpointEvery
		{
			get { return Settings.ReaderCheckpointEvery; }
			set
			{
				int previous = Settings.ReaderCheckpointEvery;
				Settings.ReaderCheckpointEvery = value;
				Data.RecordSettingChange( "Checkpoint Gap", previous, value );
			}
		}

		public int ReaderRenewLeaseDuration
		{
			get { return (int)Settings.RenewLeaseEvery.TotalSeconds; }
			set
			{
				int previous = (int)Settings.RenewLeaseEvery.TotalSeconds;
				Settings.RenewLeaseEvery = TimeSpan.FromSeconds( value );
				Data.RecordSettingChange( "Renew Lease", previous, value );

				if ( value != previous )
				{
					RestartReaders();
				}
			}
		}

		public int ReaderExpireLeaseDuration
		{
			get { return (int)Settings.ExpireLeaseEvery.TotalSeconds; }
			set
			{
				int previous = (int)Settings.ExpireLeaseEvery.TotalSeconds;
				Settings.ExpireLeaseEvery = TimeSpan.FromSeconds( value );
				Data.RecordSettingChange( "Expire Lease", previous, value );

				if ( value != previous )
				{
					RestartReaders();
				}
			}
		}

		public bool ReaderEnableMetrics
		{
			get { return Settings.ReadEnableReceiverRuntimeMetric; }
			set
			{
				bool previous = Settings.ReadEnableReceiverRuntimeMetric;
				Settings.ReadEnableReceiverRuntimeMetric = value;
				Data.RecordSettingChange( "Enable Metrics", previous ? 1 : 0, value ? 1 : 0 );

				if ( value != previous )
				{
					RestartReaders();
				}
			}
		}

		public bool UseServiceBus
		{
			get { return Settings.UseServiceBus; }
			set
			{
				bool previous = Settings.UseServiceBus;
				Settings.UseServiceBus = value;
				Data.RecordSettingChange( "Use ServiceBus", previous ? 1 : 0, value ? 1 : 0 );

				if ( value != previous )
				{
					RestartReaders();
				}
			}
		}
		#endregion

		#region System Control
		public bool UseBlockingWork
		{
			get { return Settings.UseBlockingWork; }
			set
			{
				bool previous = Settings.UseBlockingWork;
				Settings.UseBlockingWork = value;
				Data.RecordSettingChange("Use Blocking Work", previous ? 1 : 0, value ? 1 : 0 );
			}
		}

		public bool UseInstanceThreadPool
		{
			get { return Settings.UseInstanceThreadPool; }
			set
			{
				bool previous = Settings.UseInstanceThreadPool;
				Settings.UseInstanceThreadPool = value;
				Data.RecordSettingChange("Use Instance ThreadPool", previous ? 1 : 0, value ? 1 : 0 );
			}
		}

		public int MinWorkerThreads
		{
			get
			{
				ThreadPool.GetMinThreads( out int minWorkers, out int _ );
				return minWorkers;
			}
			set
			{
				ThreadPool.GetMinThreads( out int _, out int minPort );
				ThreadPool.SetMinThreads( value, minPort );
				RecordSystemState();
			}
		}

		public int MinPortThreads
		{
			get
			{
				ThreadPool.GetMinThreads( out int _, out int minPort );
				return minPort;
			}
			set
			{
				ThreadPool.GetMinThreads( out int minWorkers, out int _ );
				ThreadPool.SetMinThreads( minWorkers, value );
				RecordSystemState();
			}
		}

		public int MinInstanceWorkerThreads
		{
			get
			{
				return App.ThreadPool.MinThreads;
			}
			set
			{
				App.ThreadPool.MinThreads = value;
				RecordSystemState();
			}
		}

		public int MaxInstanceWorkerThreads
		{
			get
			{
				return App.ThreadPool.MaxThreads;
			}
			set
			{
				App.ThreadPool.MaxThreads = value;
				RecordSystemState();
			}
		}
		#endregion

		#region Charting
		private readonly Timer _recorderTimer = new Timer( 10000 );
		private readonly Timer _chartRefreshTimer = new Timer( 200 );

		private List<PartitionOwner> _partitionOwners;
		private TabItem _selectedChartTab;
		private bool _rebuildPartitionOwners;

		public TabItem SelectedChartTab
		{
			get { return _selectedChartTab; }
			set
			{
				_selectedChartTab = value;
				NotifyPropertyChanged();
			}
		}

		public PlotModel UserChangesPlot { get; set; }
		public PlotModel SystemStatePlot { get; set; }
		public PlotModel GlobalMessageCountPlot { get; set; }
		public PlotModel GlobalBatchLatencyPlot { get; set; }
		public PlotModel GlobalCheckpointLatencyPlot { get; set; }
		public PlotModel GlobalRerunLatencyPlot { get; set; }
		public PlotModel PartitionRerunLatencyPlot { get; set; }
		#endregion
		#endregion

		#region Constructors & Destructors
		public MainWindow()
		{
			UserChangesPlot = new PlotModel { Title = "User Changes", LegendPosition = LegendPosition.LeftTop, };
			UserChangesPlot.Axes.Add( new LogarithmicAxis() );
			SystemStatePlot = new PlotModel { Title = "System State", LegendPosition = LegendPosition.LeftTop, };
			SystemStatePlot.Axes.Add( new LogarithmicAxis() );
			GlobalMessageCountPlot = new PlotModel { Title = "Global Message Count (n)", LegendPosition = LegendPosition.LeftTop, };
			GlobalBatchLatencyPlot = new PlotModel { Title = "Global Batch Latency (ms)", LegendPosition = LegendPosition.LeftTop, };
			GlobalCheckpointLatencyPlot = new PlotModel { Title = "Global Checkpoint Latency (ms)", LegendPosition = LegendPosition.LeftTop, };
			GlobalRerunLatencyPlot = new PlotModel { Title = "Global Rerun Latency (ms)", LegendPosition = LegendPosition.LeftTop, };
			PartitionRerunLatencyPlot = new PlotModel { Title = "Partition Rerun Latency (ms)", LegendPosition = LegendPosition.LeftTop, };

			RecordUserSettings();
			RecordSystemState();

			_recorderTimer.Elapsed += RecorderTimer_ElapsedHandler;
			_recorderTimer.Start();
			_chartRefreshTimer.Elapsed += ChartRefreshTimer_ElapsedHandler;
			_chartRefreshTimer.Start();

			InitializeComponent();
		}

		protected override void OnClosed(EventArgs e)
		{
			Data.Info(nameof(MainWindow), "Window closing.");

			base.OnClosed(e);

			App.ThreadPool.Dispose();

			Data.Info(nameof(MainWindow), "Window closed.");
		}
		#endregion

		#region Reader/Writer Control
		private async void RestartReaders()
		{
			try
			{
				IsBusy = true;

				await Task.WhenAll( Readers.Select( x => x.Stop() ).ToArray() ).ConfigureAwait(false);

				Data.RecordSettingChange( "Reader Restart", 1, 0 );

				await Task.WhenAll( Readers.Select( x => x.Start() ).ToArray() ).ConfigureAwait(false);

				Data.RecordSettingChange( "Reader Restart", 0, 1 );
			}
			catch ( Exception e )
			{
				Data.Error( "MainWindow", "Failed to restart readers.", e );
			}
			finally
			{
				IsBusy = false;
				_rebuildPartitionOwners = true;
			}
		}

		private async void TerminateAll()
		{
			try
			{
				IsBusy = true;

				int writerCount = Writers.Count;
				int readerCount = Readers.Count;

				await Task.WhenAll( Writers.Select( x => x.Stop() ).Union( Readers.Select( x => x.Stop() ) ).ToArray() ).ConfigureAwait(false);

				Writers.Clear();
				Readers.Clear();

				Data.RecordSettingChange( "Writer Count", writerCount, Writers.Count );
				Data.RecordSettingChange( "Reader Count", readerCount, Readers.Count );
			}
			catch ( Exception e )
			{
				Data.Error( "MainWindow", "Failed to terminate everything.", e );
			}
			finally
			{
				IsBusy = false;
				_rebuildPartitionOwners = true;
			}
		}
		#endregion

		#region Charting
		private void RecordUserSettings()
		{
			WriterSleep = WriterSleep;
			ReaderSleep = ReaderSleep;

			WriterBatchSize = WriterBatchSize;
			ReaderBatchSize = ReaderBatchSize;

			ReaderMessageBatchSize = ReaderMessageBatchSize;
			ReaderMessageDuration = ReaderMessageDuration;
			ReaderCheckpointEvery = ReaderCheckpointEvery;
			ReaderRenewLeaseDuration = ReaderRenewLeaseDuration;
			ReaderExpireLeaseDuration = ReaderExpireLeaseDuration;
			ReaderEnableMetrics = ReaderEnableMetrics;

			UseServiceBus = UseServiceBus;
			Data.RecordSetting("Use Blocking Work", UseBlockingWork ? 1 : 0);
			Data.RecordSetting("Use Instance ThreadPool", UseInstanceThreadPool ? 1 : 0);

			Data.RecordSetting( "Reader Restart", 0 );

			Data.RecordSetting( "Writer Count", Writers.Count );
			Data.RecordSetting( "Reader Count", Readers.Count );
		}

		private void RecordSystemState()
		{
			ThreadPool.GetMinThreads( out int minWorkers, out int minPort );
			ThreadPool.GetAvailableThreads( out int availableWorkers, out int availablePort );
			ThreadPool.GetMaxThreads( out int maxWorkers, out int maxPort );

			Data.RecordSystem( "-Threads (W)", minWorkers );
			Data.RecordSystem( "-Threads (P)", minPort );
			Data.RecordSystem( "-Threads (I)", App.ThreadPool.MinThreads );

			Data.RecordSystem( "+Threads (I)", App.ThreadPool.MaxThreads );

			Data.RecordSystem( "# Threads (W)", maxWorkers - availableWorkers );
			Data.RecordSystem( "# Threads (P)", maxPort - availablePort );
			Data.RecordSystem( "# Threads (I)", App.ThreadPool.ActiveThreads );

			Data.RecordSystem( "Av. Threads (I)", App.ThreadPool.AvailableThreads );

			Data.RecordSystem( "Queue (I)", App.ThreadPool.QueueLength );
		}

		private Task RefreshCharts()
		{
			TabItem selectedTab = SelectedChartTab;

			// Ordered by most used for faster decision making.
			if ( Equals( selectedTab, GlobalCheckpointLatencyChartTab ) )
			{
				return GenerateGlobalCheckpointLatencyChart();
			}

			if ( Equals( selectedTab, GlobalBatchLatencyChartTab) )
			{
				return GenerateGlobalBatchLatencyChart();
			}

			if ( Equals( selectedTab, GlobalMessageCountChartTab ) )
			{
				return GenerateGlobalMessageCountChart();
			}

			if (Equals(selectedTab, GlobalRerunLatencyChartTab))
			{
				return GenerateGlobalRerunLatencyChart();
			}

			if ( Equals( selectedTab, PartitionDivisionGridTab ) )
			{
				return GeneratePartitionDivisionGrid();
			}

			if ( Equals( selectedTab, SystemStateChartTab ) )
			{
				return GenerateSystemStateChart();
			}

			if ( Equals( selectedTab, UserChangesChartTab ) )
			{
				return GenerateUserChangeChart();
			}

			if ( Equals( selectedTab, PartitionRerunLatencyChartTab ) )
			{
				return GeneratePartitionRerunLatencyChart();
			}

			return Task.CompletedTask;
		}

		private async Task GenerateUserChangeChart()
		{
			// Breaks the method into an async method so it can work in peace and be awaited.
			await Task.Delay( 1 ).ConfigureAwait(false);

			// TODO: Add locking to this collection. As it grows, it will start to crash while generating the chart.
			var data =
				Data.UserChangeStats.
					ToList().
					OrderBy( x => x.Second ).
					GroupBy( x => x.Name ).
					Select( x => new
					{
						x.Key,
						Points = x.Select( y => new DataPoint( y.Second, Math.Max( 0.1, y.Value ) ) ).ToList(),
					} ).
					ToList();

			List<LineSeries> allSeries = new List<LineSeries>();

			foreach ( var singleSeries in data )
			{
				LineSeries series = new LineSeries { Title = singleSeries.Key, };
				series.Points.AddRange( singleSeries.Points );

				allSeries.Add( series );
			}

			// This (partially?)fixes an issue where adding a lot of series will cause exceptions when refreshing the
			// chart. Locking on the sync root will allow us exclusive access to quickly update the charts data without
			// crossing the charts internal operations. It must be kept short though.
			lock ( UserChangesPlot.SyncRoot )
			{
				UserChangesPlot.Series.Clear();

				foreach ( LineSeries series in allSeries )
				{
					UserChangesPlot.Series.Add( series );
				}
			}

			UserChangesPlot.InvalidatePlot( true );
		}

		private async Task GenerateSystemStateChart()
		{
			// Breaks the method into an async method so it can work in peace and be awaited.
			await Task.Delay( 1 ).ConfigureAwait(false);

			// TODO: Add locking to this collection. As it grows, it will start to crash while generating the chart.
			var data =
				Data.SystemStateStats.
					ToList().
					OrderBy( x => x.Second ).
					GroupBy( x => x.Name ).
					Select( x => new
					{
						x.Key,
						Points = x.Select( y => new DataPoint( y.Second, Math.Max( 0.1, y.Value ) ) ).ToList(),
					} ).
					ToList();

			List<LineSeries> allSeries = new List<LineSeries>();

			foreach ( var singleSeries in data )
			{
				LineSeries series = new LineSeries { Title = singleSeries.Key, };
				series.Points.AddRange( singleSeries.Points );

				allSeries.Add( series );
			}

			// This (partially?)fixes an issue where adding a lot of series will cause exceptions when refreshing the
			// chart. Locking on the sync root will allow us exclusive access to quickly update the charts data without
			// crossing the charts internal operations. It must be kept short though.
			lock ( SystemStatePlot.SyncRoot )
			{
				SystemStatePlot.Series.Clear();

				foreach ( LineSeries series in allSeries )
				{
					SystemStatePlot.Series.Add( series );
				}
			}

			SystemStatePlot.InvalidatePlot( true );
		}

		private async Task GenerateGlobalMessageCountChart()
		{
			// Breaks the method into an async method so it can work in peace and be awaited.
			await Task.Delay( 1 ).ConfigureAwait(false);

			var data =
				Data.StatsByTypeAndSecond.
					ToList().
					Select( x => new
					{
						IsReader = x.Key,
						Points = x.Value.OrderBy( y => y.Key ).Select( y => new DataPoint( y.Key, y.Sum( z => z.ProcessedMessages ) ) ).ToList(),
					} ).
					ToList();

			lock ( GlobalMessageCountPlot.SyncRoot )
			{
				GlobalMessageCountPlot.Series.Clear();

				foreach ( var entry in data )
				{
					LineSeries series = new LineSeries { Title = entry.IsReader ? "Read" : "Write", };
					series.Points.AddRange( entry.Points );

					GlobalMessageCountPlot.Series.Add( series );
				}
			}

			GlobalMessageCountPlot.InvalidatePlot( true );
		}

		private async Task GenerateGlobalBatchLatencyChart()
		{
			// Breaks the method into an async method so it can work in peace and be awaited.
			await Task.Delay( 1 ).ConfigureAwait(false);

			var data =
				Data.ReaderStatsBySecond.
					ToList().
					OrderBy( x => x.Key ).
					Select( x => new
					{
						AvgPoint = new DataPoint( x.Key, x.Average( z => z.BatchDuration ) ),
						MaxPoint = new DataPoint( x.Key, x.Max( z => z.BatchDuration) ),
					} ).
					ToList();

			LineSeries maxSeries = new LineSeries { Title = "Maximum", Color = OxyColors.Firebrick, };
			LineSeries avgSeries = new LineSeries { Title = "Average", };
			maxSeries.Points.AddRange(data.Select(x => x.MaxPoint));
			avgSeries.Points.AddRange(data.Select(x => x.AvgPoint));

			lock ( GlobalBatchLatencyPlot.SyncRoot )
			{
				GlobalBatchLatencyPlot.Series.Clear();
				GlobalBatchLatencyPlot.Series.Add(maxSeries);
				GlobalBatchLatencyPlot.Series.Add(avgSeries);
			}

			GlobalBatchLatencyPlot.InvalidatePlot( true );
		}

		private async Task GenerateGlobalCheckpointLatencyChart()
		{
			// Breaks the method into an async method so it can work in peace and be awaited.
			await Task.Delay( 1 ).ConfigureAwait(false);

			var data =
				Data.ReaderStatsBySecond.
					ToList().
					OrderBy( x => x.Key ).
					Select( x => new
					{
						AvgPoint = new DataPoint( x.Key, x.Average( z => z.CheckpointDuration ) ),
						MaxPoint = new DataPoint( x.Key, x.Max( z => z.CheckpointDuration ) ),
					} ).
					ToList();

			LineSeries maxSeries = new LineSeries { Title = "Maximum", Color = OxyColors.Firebrick, };
			LineSeries avgSeries = new LineSeries { Title = "Average", };
			maxSeries.Points.AddRange(data.Select(x => x.MaxPoint));
			avgSeries.Points.AddRange(data.Select(x => x.AvgPoint));

			lock ( GlobalCheckpointLatencyPlot.SyncRoot )
			{
				GlobalCheckpointLatencyPlot.Series.Clear();
				GlobalCheckpointLatencyPlot.Series.Add(maxSeries);
				GlobalCheckpointLatencyPlot.Series.Add(avgSeries);
			}

			GlobalCheckpointLatencyPlot.InvalidatePlot( true );
		}

		private async Task GenerateGlobalRerunLatencyChart()
		{
			// Breaks the method into an async method so it can work in peace and be awaited.
			await Task.Delay( 1 ).ConfigureAwait(false);

			var data =
				Data.ReaderStatsBySecond.
					ToList().
					OrderBy( x => x.Key ).
					Select( x => new
					{
						AvgPoint = new DataPoint( x.Key, x.Average( z => z.RerunLatency ) ),
						MaxPoint = new DataPoint( x.Key, x.Max( z => z.RerunLatency ) ),
					} ).
					ToList();

			LineSeries maxSeries = new LineSeries { Title = "Maximum", Color = OxyColors.Firebrick, };
			LineSeries avgSeries = new LineSeries { Title = "Average", };
			maxSeries.Points.AddRange(data.Select(x => x.MaxPoint));
			avgSeries.Points.AddRange(data.Select(x => x.AvgPoint));

			lock (GlobalRerunLatencyPlot.SyncRoot)
			{
				GlobalRerunLatencyPlot.Series.Clear();
				GlobalRerunLatencyPlot.Series.Add(maxSeries);
				GlobalRerunLatencyPlot.Series.Add(avgSeries);
			}

			GlobalRerunLatencyPlot.InvalidatePlot(true);
		}

		private async Task GeneratePartitionRerunLatencyChart()
		{
			// Breaks the method into an async method so it can work in peace and be awaited.
			await Task.Delay( 1 ).ConfigureAwait(false);

			var data =
				Data.StatsByPartitionAndSecond.
					ToList().
					Select( x => new
					{
						PartitionId = x.Key,
						Points = x.Value.OrderBy(y => y.Key).Select( y => new DataPoint( y.Key, y.Sum( z => z.RerunLatency ) ) ).ToList(),
					} ).
					ToList();

			List<LineSeries> allSeries = new List<LineSeries>();

			foreach ( var entry in data )
			{
				LineSeries series = new LineSeries { Title = $"Partition {entry.PartitionId}", };
				series.Points.AddRange( entry.Points );

				allSeries.Add( series );
				PartitionRerunLatencyPlot.Series.Add( series );
			}

			// This (partially?)fixes an issue where adding a lot of series will cause exceptions when refreshing the
			// chart. Locking on the sync root will allow us exclusive access to quickly update the charts data without
			// crossing the charts internal operations. It must be kept short though.
			lock ( PartitionRerunLatencyPlot.SyncRoot )
			{
				PartitionRerunLatencyPlot.Series.Clear();

				foreach ( LineSeries series in allSeries )
				{
					PartitionRerunLatencyPlot.Series.Add( series );
				}
			}

			PartitionRerunLatencyPlot.InvalidatePlot( true );
		}

		private async Task GeneratePartitionDivisionGrid()
		{
			// This should help smooth over any refreshing issues when e.g. a reader is restarted but takes a while to claim a partition.
			_rebuildPartitionOwners = _rebuildPartitionOwners ||
									  Data.PartitionOwnerStats.Count != PartitionOwners.Count;

			if ( !_rebuildPartitionOwners )
			{
				foreach ( PartitionOwner x in PartitionOwners )
				{
					x.NotifyPartitionAge();
				}
				return;
			}

			// Breaks the method into an async method so it can work in peace and be awaited.
			await Task.Delay( 1 ).ConfigureAwait(false);

			PartitionOwners = Data.PartitionOwnerStats.
				Where( x => x.HasPartition00Since.HasValue ||
							x.HasPartition01Since.HasValue ||
							x.HasPartition02Since.HasValue ||
							x.HasPartition03Since.HasValue ||
							x.HasPartition04Since.HasValue ||
							x.HasPartition05Since.HasValue ||
							x.HasPartition06Since.HasValue ||
							x.HasPartition07Since.HasValue ||
							x.HasPartition08Since.HasValue ||
							x.HasPartition09Since.HasValue ||
							x.HasPartition10Since.HasValue ||
							x.HasPartition11Since.HasValue ||
							x.HasPartition12Since.HasValue ||
							x.HasPartition13Since.HasValue ||
							x.HasPartition14Since.HasValue ||
							x.HasPartition15Since.HasValue ||
							x.HasPartition16Since.HasValue ||
							x.HasPartition17Since.HasValue ||
							x.HasPartition18Since.HasValue ||
							x.HasPartition19Since.HasValue ||
							x.HasPartition20Since.HasValue ||
							x.HasPartition21Since.HasValue ||
							x.HasPartition22Since.HasValue ||
							x.HasPartition23Since.HasValue ||
							x.HasPartition24Since.HasValue ||
							x.HasPartition25Since.HasValue ||
							x.HasPartition26Since.HasValue ||
							x.HasPartition27Since.HasValue ||
							x.HasPartition28Since.HasValue ||
							x.HasPartition29Since.HasValue ||
							x.HasPartition30Since.HasValue ||
							x.HasPartition31Since.HasValue ).
				OrderBy( x => x.Name ).
				ToList();

			_rebuildPartitionOwners = false;
		}
		#endregion

		#region Events
		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void NotifyPropertyChanged( [CallerMemberName] string propertyName = null )
		{
			PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
		}

		private void BusyIndicatorCloseButton_ClickHandler(object sender, RoutedEventArgs e)
		{
			IsBusy = false;
		}

		#region Read/Write Control
		private async void AddWriterButton_ClickHandler( object sender, RoutedEventArgs e )
		{
			try
			{
				IsBusy = true;

				int previousCount = Writers.Count;
				IRunnable runnable = UseServiceBus ? (IRunnable)new ServiceBusWriter() : new EventHubsWriter();

				if ( !Settings.DisableRunning )
				{
					await runnable.Start().ConfigureAwait(false);
				}

				Writers.Add( runnable );

				Data.RecordSettingChange( "Writer Count", previousCount, Writers.Count );
			}
			catch ( Exception ex )
			{
				Data.Error( "MainWindow", "Failed to start writer.", ex );
			}
			finally
			{
				IsBusy = false;
			}
		}

		private async void AddMultipleWriterButton_ClickHandler( object sender, RoutedEventArgs e )
		{
			try
			{
				IsBusy = true;

				int previousCount = Writers.Count;

				for ( int i = 0; i < 10; i++ )
				{
					IRunnable runnable = UseServiceBus ? (IRunnable)new ServiceBusWriter() : new EventHubsWriter();

					if ( !Settings.DisableRunning )
					{
						await runnable.Start().ConfigureAwait(false);
					}

					Writers.Add( runnable );
				}

				Data.RecordSettingChange( "Writer Count", previousCount, Writers.Count );
			}
			catch ( Exception ex )
			{
				Data.Error( "MainWindow", "Failed to start multiple writers.", ex );
			}
			finally
			{
				IsBusy = false;
			}
		}

		private async void RemoveWriterButton_ClickHandler( object sender, RoutedEventArgs e )
		{
			try
			{
				IsBusy = true;

				int previousCount = Writers.Count;
				IRunnable runnable = Writers.FirstOrDefault();

				if ( runnable != null )
				{
					await runnable.Stop().ConfigureAwait(false);
					Writers.Remove( runnable );

					Data.RecordSettingChange( "Writer Count", previousCount, Writers.Count );
				}
			}
			catch ( Exception ex )
			{
				Data.Error( "MainWindow", "Failed to stop writer.", ex );
			}
			finally
			{
				IsBusy = false;
			}
		}

		private async void RemoveMultipleWriterButton_ClickHandler( object sender, RoutedEventArgs e )
		{
			try
			{
				IsBusy = true;

				int previousCount = Writers.Count;
				List<IRunnable> runnables = Writers.Take( 10 ).ToList();

				foreach ( IRunnable runnable in runnables )
				{
					await runnable.Stop().ConfigureAwait(false);
					Writers.Remove( runnable );
				}

				Data.RecordSettingChange( "Writer Count", previousCount, Writers.Count );
			}
			catch ( Exception ex )
			{
				Data.Error( "MainWindow", "Failed to stop multiple writers.", ex );
			}
			finally
			{
				IsBusy = false;
			}
		}

		private async void AddReaderButton_ClickHandler( object sender, RoutedEventArgs e )
		{
			try
			{
				IsBusy = true;

				int previousCount = Readers.Count;
				IRunnable runnable = UseServiceBus ? (IRunnable)new ServiceBusReader() : new EventHubsReader();

				if ( !Settings.DisableRunning )
				{
					await runnable.Start().ConfigureAwait(false);
				}

				Readers.Add( runnable );

				Data.RecordSettingChange( "Reader Count", previousCount, Readers.Count );
			}
			catch ( Exception ex )
			{
				Data.Error( "MainWindow", "Failed to start reader.", ex );
			}
			finally
			{
				IsBusy = false;
				_rebuildPartitionOwners = true;
			}
		}

		private async void RemoveReaderButton_ClickHandler( object sender, RoutedEventArgs e )
		{
			try
			{
				IsBusy = true;

				int previousCount = Readers.Count;
				IRunnable runnable = Readers.FirstOrDefault();

				if ( runnable != null )
				{
					await runnable.Stop().ConfigureAwait(false);
					Readers.Remove( runnable );

					Data.RecordSettingChange( "Reader Count", previousCount, Readers.Count );
				}
			}
			catch ( Exception ex )
			{
				Data.Error( "MainWindow", "Failed to stop reader.", ex );
			}
			finally
			{
				IsBusy = false;
				_rebuildPartitionOwners = true;
			}
		}

		private void LeaseTypeComboBox_SelectionChangedHandler( object sender, SelectionChangedEventArgs e )
		{
			FrameworkElement selectedItem = LeaseTypeComboBox.SelectedItem as FrameworkElement;
			LeaseTypeComboBox.ToolTip = selectedItem?.ToolTip ?? "Unknown Selection";

			string data = selectedItem?.DataContext as string;
			LeaseType previousLeaseType = Settings.LeaseType;

			switch ( data )
			{
				case nameof(LeaseType.CloudSqlStorage):
					Settings.LeaseType = LeaseType.CloudSqlStorage;
					break;
				case nameof(LeaseType.LocalDiskStorage):
					Settings.LeaseType = LeaseType.LocalDiskStorage;
					break;
				default:
					Settings.LeaseType = LeaseType.AzureStorageAccount;
					break;
			}

			if ( previousLeaseType != Settings.LeaseType )
			{
				RestartReaders();
			}
		}

		private void RestartAllReadersButton_ClickHandler( object sender, RoutedEventArgs e )
		{
			RestartReaders();
		}

		private void TerminateAllButton_ClickHandler( object sender, RoutedEventArgs e )
		{
			TerminateAll();
		}
		#endregion

		#region Charting
		private void RecorderTimer_ElapsedHandler( object sender, ElapsedEventArgs e )
		{
			try
			{
				_recorderTimer.Stop();

				try
				{
					RecordUserSettings();
				}
				catch ( Exception ex )
				{
					Data.Error( "MainWindow", "Failed to record user settings.", ex );
				}

				try
				{
					RecordSystemState();
				}
				catch ( Exception ex )
				{
					Data.Error( "MainWindow", "Failed to record system state.", ex );
				}
			}
			finally
			{
				_recorderTimer.Start();
			}
		}

		private async void ChartRefreshTimer_ElapsedHandler( object sender, ElapsedEventArgs e )
		{
			try
			{
				_chartRefreshTimer.Stop();

				await RefreshCharts().ConfigureAwait(false);
			}
			catch ( Exception ex )
			{
				Data.Error( "MainWindow", "Failed to refresh charts.", ex );
			}
			finally
			{
				_chartRefreshTimer.Start();
			}
		}

		private void TabControl_SelectionChangedHandler(object sender, SelectionChangedEventArgs e)
		{
			_rebuildPartitionOwners = true;

			TabItem selectedTab = SelectedChartTab;

			// This plot is really slowing us down. If it was drawn once but we're not looking at it, clear it.
			if ( !Equals( selectedTab, PartitionRerunLatencyChartTab ) &&
				 PartitionRerunLatencyPlot.Series.Count > 0 )
			{
				PartitionRerunLatencyPlot.Series.Clear();
				PartitionRerunLatencyPlot.InvalidatePlot( true );
			}

			RefreshCharts();
		}

		private void ResetUserChangesPlotButton_ClickHandler( object sender, RoutedEventArgs e )
		{
			UserChangesPlot.ResetAllAxes();
		}

		private void ResetSystemStatePlotButton_ClickHandler( object sender, RoutedEventArgs e )
		{
			SystemStatePlot.ResetAllAxes();
		}

		private void ResetGlobalMessageCountPlotButton_ClickHandler( object sender, RoutedEventArgs e )
		{
			GlobalMessageCountPlot.ResetAllAxes();
		}

		private void ResetGlobalBatchLatencyPlotButton_ClickHandler( object sender, RoutedEventArgs e )
		{
			GlobalBatchLatencyPlot.ResetAllAxes();
		}

		private void ResetGlobalCheckpointLatencyPlotButton_ClickHandler( object sender, RoutedEventArgs e )
		{
			GlobalCheckpointLatencyPlot.ResetAllAxes();
		}

		private void ResetGlobalRerunLatencyPlotButton_ClickHandler( object sender, RoutedEventArgs e )
		{
			GlobalRerunLatencyPlot.ResetAllAxes();
		}

		private void ResetPartitionRerunLatencyPlotButton_ClickHandler( object sender, RoutedEventArgs e )
		{
			PartitionRerunLatencyPlot.ResetAllAxes();
		}

		private void PartitionDivisionDataGrid_AutoGeneratingColumnHandler( object sender, DataGridAutoGeneratingColumnEventArgs e )
		{
			switch ( e.PropertyName )
			{
				case nameof(PartitionOwner.Name):
					e.Column = new DataGridTextColumn { Header = "Name", Binding = new Binding( "Name" ), };
					break;
				case nameof(PartitionOwner.OwnerBrush):
					e.Cancel = true;
					break;
				default:
					string partitionId = e.PropertyName.
						Replace( "HasPartition", string.Empty ).
						Replace( "Since", string.Empty );

					e.Column = new DataGridTemplateColumn
					{
						Header = partitionId,

						// Ugh, don't even ask. Dynamic creation of the data template of a dynamically created column. I agree, it's hideous.
						CellTemplate = (DataTemplate)
							XamlReader.Parse( $"<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"" +
											  $"			  xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"" +
											  $"			  xmlns:general=\"http://www.allsetra.nl/ThroughputTest/General\">" +
											  $"	<DataTemplate.Resources>" +
											  $"		<general:DateTimeToStringConverter x:Key=\"DateTimeToStringConverter\" />" +
											  $"		<general:AgeToBrushConverter x:Key=\"AgeToBrushConverter\" />" +
											  $"		<general:NullToVisibilityConverter x:Key=\"VisibilityConverter\" />" +
											  $"	</DataTemplate.Resources>" +
											  $"	<Grid>" +
											  $"		<Border Background=\"{{Binding OwnerBrush}}\"" +
											  $"				ToolTip=\"{{Binding {e.PropertyName}, Converter={{StaticResource DateTimeToStringConverter}}}}\"" +
											  $"				Visibility=\"{{Binding {e.PropertyName}, Converter={{StaticResource VisibilityConverter}}}}\" />" +
											  $"		<Border Background=\"{{Binding {e.PropertyName}, Converter={{StaticResource AgeToBrushConverter}}}}\"" +
											  $"				BorderBrush=\"White\" BorderThickness=\"0 0 1 0\"" +
											  $"				Width=\"4\" HorizontalAlignment=\"Left\"" +
											  $"				Visibility=\"{{Binding {e.PropertyName}, Converter={{StaticResource VisibilityConverter}}}}\" />" +
											  $"	</Grid>" +
											  $"</DataTemplate>" ),
					};
					break;
			}
		}
		#endregion
		#endregion
	}
}
