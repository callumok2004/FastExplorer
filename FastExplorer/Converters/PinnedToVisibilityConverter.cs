using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FastExplorer.Converters {
	public class PinnedToVisibilityConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is string path) {
				bool isPinned = AppSettings.Current.PinnedFolders.Any(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
				bool invert = parameter?.ToString() == "Invert";
				return (isPinned ^ invert) ? Visibility.Visible : Visibility.Collapsed;
			}
			return Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			throw new NotImplementedException();
		}
	}
}
