using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FastExplorer.Converters {
	public class UniversalBoolToVisibilityConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			bool bVal = false;
			if (value is bool b) bVal = b;

			if (parameter?.ToString() == "Invert") bVal = !bVal;

			return bVal ? Visibility.Visible : Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is Visibility v) {
				bool bVal = v == Visibility.Visible;
				if (parameter?.ToString() == "Invert") bVal = !bVal;
				return bVal;
			}
			return false;
		}
	}
}
