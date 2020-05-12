using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest.General
{
	public class AgeToBrushConverter : IValueConverter
	{
		public Brush New { get; set; } = Brushes.ForestGreen;
		public Brush Old { get; set; } = Brushes.Goldenrod;
		public Brush Ancient { get; set; } = Brushes.Firebrick;

		public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
		{
			DateTime? birth = value as DateTime?;

			if ( !birth.HasValue )
			{
				return 0.0;
			}

			int age = (int)( DateTime.Now - birth.Value ).TotalSeconds;

			return age < Settings.RenewLeaseEvery.TotalSeconds * 0.9 ? New : age < Settings.ExpireLeaseEvery.TotalSeconds * 0.9 ? Old : Ancient;
		}

		public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture )
		{
			throw new NotSupportedException();
		}
	}
}