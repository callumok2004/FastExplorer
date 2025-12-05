using System.Windows;

namespace FastExplorer {
	public partial class InputWindow : Window {
		public string ResponseText { get; private set; } = string.Empty;

		public InputWindow() {
			InitializeComponent();
			InputTextBox.Focus();
		}

		private void Add_Click(object sender, RoutedEventArgs e) {
			ResponseText = InputTextBox.Text;
			DialogResult = true;
		}

		private void Cancel_Click(object sender, RoutedEventArgs e) {
			DialogResult = false;
		}
	}
}