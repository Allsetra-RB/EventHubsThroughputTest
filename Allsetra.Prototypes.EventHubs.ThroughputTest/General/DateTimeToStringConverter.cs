using System;
using System.Globalization;
using System.Windows.Data;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest.General
{
	public class DateTimeToStringConverter : IValueConverter
	{
		public string Format { get; set; } = "HH:mm:ss";

		public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
		{
			DateTime? x = value as DateTime?;

			if ( !x.HasValue )
			{
				return "<unknown>";
			}

			return x.Value.ToString( Format );
		}

		public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture )
		{
			throw new NotSupportedException();
		}
	}
}