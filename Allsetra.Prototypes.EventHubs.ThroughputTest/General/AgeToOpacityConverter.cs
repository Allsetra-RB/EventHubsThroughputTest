using System;
using System.Globalization;
using System.Windows.Data;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest.General
{
	public class AgeToOpacityConverter : IValueConverter
	{
		public bool IsInverted { get; set; }

		public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
		{
			DateTime? birth = value as DateTime?;

			if ( !birth.HasValue )
			{
				return 0.0;
			}

			int age = (int)( DateTime.Now - birth.Value ).TotalSeconds;

			if ( IsInverted )
			{
				// Opacity ranges between 0.0 and 0.75. The older the value, the more transparent.
				return Math.Max( 0.0, Math.Min( age / 60.0, 0.75 ) );
			}

			// Opacity ranges between 0.25 and 1.0. The older the value, the less transparent.
			return Math.Max( 0.25, Math.Min( 1.0 - age / 60.0, 1.0 ) );
		}

		public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture )
		{
			throw new NotSupportedException();
		}
	}
}