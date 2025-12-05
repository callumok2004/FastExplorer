using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FastExplorer.ViewModels;

namespace FastExplorer {
    public partial class OptionsWindow : Window {
        public OptionsWindow() {
            InitializeComponent();
            DataContext = new SettingsViewModel();
            Closing += (s, e) => AppSettings.Save();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) {
            Close();
		}
	}

    public class SettingsViewModel : ViewModelBase {
        private SettingsPageViewModel? _selectedPage;

        public ObservableCollection<SettingsPageViewModel> Pages { get; } = [];

        public SettingsPageViewModel? SelectedPage {
            get => _selectedPage;
            set => SetProperty(ref _selectedPage, value);
        }

        public ICommand CloseCommand { get; }

        public SettingsViewModel() {
            Pages.Add(new GeneralPageViewModel());
            Pages.Add(new AppearancePageViewModel());

            SelectedPage = Pages.FirstOrDefault()!;

            CloseCommand = new RelayCommand(_ => {
                foreach (Window win in Application.Current.Windows) {
                    if (win is OptionsWindow) win.Close();
                }
            });
        }
    }

    public abstract class SettingsPageViewModel : ViewModelBase {
        public abstract string Title { get; }
        public abstract string Icon { get; }
        public abstract string Description { get; }
    }

    public class GeneralPageViewModel : SettingsPageViewModel {
        public override string Title => "General";
        public override string Icon => "\uE713";
        public override string Description => "Search, navigation, and behavior";

        public static AppSettings Settings => AppSettings.Current;
    }

    public class AppearancePageViewModel : SettingsPageViewModel {
        public override string Title => "Appearance";
        public override string Icon => "\uE790";
        public override string Description => "Customization and accessibility";

        public static AppSettings Settings => AppSettings.Current;
    }
}
