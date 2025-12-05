using System;
using System.Globalization;
using System.Windows.Data;

namespace FastExplorer.Converters {
	public class MathConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is double d && double.TryParse(parameter?.ToString(), out double factor)) {
				return d * factor;
			}
			return value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			throw new NotImplementedException();
		}
	}
}
