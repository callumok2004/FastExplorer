using System.Windows;

namespace FastExplorer {
	public partial class CredentialsWindow : Window {
		public string Username { get; private set; } = string.Empty;
		public string Password { get; private set; } = string.Empty;
		public bool RememberMe { get; private set; }

		public CredentialsWindow() {
			InitializeComponent();
			UsernameBox.Focus();
		}

		private void Connect_Click(object sender, RoutedEventArgs e) {
			Username = UsernameBox.Text;
			Password = PasswordBox.Password;
			RememberMe = RememberMeCheck.IsChecked == true;
			DialogResult = true;
		}

		private void Cancel_Click(object sender, RoutedEventArgs e) {
			DialogResult = false;
		}
	}
}