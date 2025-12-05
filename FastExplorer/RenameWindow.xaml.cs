using System.Windows;
using System.Windows.Input;

namespace FastExplorer {
    public partial class RenameWindow : Window {
        public string NewName { get; private set; } = string.Empty;

        public RenameWindow(string currentName) {
            InitializeComponent();
            NameBox.Text = currentName;
            NameBox.SelectAll();
            NameBox.Focus();
            Loaded += (s, e) => NameBox.Focus();
        }

        private void OK_Click(object sender, RoutedEventArgs e) {
            NewName = NameBox.Text;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
        }

        private void NameBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                OK_Click(sender, e);
            }
            else if (e.Key == Key.Escape) {
                Cancel_Click(sender, e);
            }
        }
    }
}
