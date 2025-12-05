using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FastExplorer.Converters {
	public class PercentToColorConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is double percent) {
				if (percent >= 90) return Brushes.Red;
				if (percent >= 75) return Brushes.Orange;
				return Brushes.LimeGreen;
			}
			return Brushes.Gray;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			throw new NotImplementedException();
		}
	}
}
