using System.Globalization;
using System.Windows.Data;

namespace FastExplorer.Converters {
	public class DateToGroupHeaderConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is DateTime date) {
				var now = DateTime.Now;
				var today = now.Date;
				var yesterday = today.AddDays(-1);

				if (date.Date == today) return "Today";
				if (date.Date == yesterday) return "Yesterday";

				var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
				if (date.Date >= startOfWeek) return "Earlier this week";

				var startOfLastWeek = startOfWeek.AddDays(-7);
				if (date.Date >= startOfLastWeek) return "Last week";

				var startOfMonth = new DateTime(today.Year, today.Month, 1);
				if (date.Date >= startOfMonth) return "Earlier this month";

				var startOfLastMonth = startOfMonth.AddMonths(-1);
				if (date.Date >= startOfLastMonth) return "Last month";

				var startOfYear = new DateTime(today.Year, 1, 1);
				if (date.Date >= startOfYear) return "Earlier this year";

				return "A long time ago";
			}
			return value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			throw new NotImplementedException();
		}
	}
}
