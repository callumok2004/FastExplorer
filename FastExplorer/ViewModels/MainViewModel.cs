using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace FastExplorer.ViewModels {
	public class MainViewModel : ViewModelBase {
		private ObservableCollection<DirectoryItemViewModel> _drives;
		private ObservableCollection<DirectoryItemViewModel> _quickAccess;
		private ObservableCollection<TabViewModel> _tabs;
		private TabViewModel? _selectedTab;

		public ICommand AddTabCommand { get; }
		public ICommand CloseTabCommand { get; }
		public ICommand NavigateToThisPCCommand { get; }
		public ICommand OpenOptionsCommand { get; }
		public ICommand PinToQuickAccessCommand { get; }
		public ICommand UnpinFromQuickAccessCommand { get; }
		public ICommand MovePinUpCommand { get; }
		public ICommand MovePinDownCommand { get; }

		private string _systemStatus = "CPU: 0%   Mem: 0 MB";
		public string SystemStatus {
			get => _systemStatus;
			set => SetProperty(ref _systemStatus, value);
		}

		private readonly System.Windows.Threading.DispatcherTimer _statusTimer;
		private readonly Process _currentProcess;
		private TimeSpan _lastProcessorTime;
		private DateTime _lastTimerTick;

		public MainViewModel() {
			_drives = [];
			_quickAccess = [];
			_tabs = [];

#if DEBUG
			_currentProcess = Process.GetCurrentProcess();
			_lastProcessorTime = _currentProcess.TotalProcessorTime;
			_lastTimerTick = DateTime.Now;
			_statusTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
			_statusTimer.Tick += UpdateSystemStatus;
			_statusTimer.Start();
#endif

			LoadQuickAccess();
			LoadDrives();

			AddTabCommand = new RelayCommand(param => AddTab(param as string));
			CloseTabCommand = new RelayCommand(param => CloseTab(param as TabViewModel));
			NavigateToThisPCCommand = new RelayCommand(_ => {
				SelectedTab?.NavigateTo("This PC");
				ClearSelection(QuickAccess);
				ClearSelection(Drives);
			});
			OpenOptionsCommand = new RelayCommand(_ => {
				var options = new OptionsWindow { Owner = Application.Current.MainWindow };
				_ = options.ShowDialog();
			});
			PinToQuickAccessCommand = new RelayCommand(param => {
				if (param is string path) PinToQuickAccess(path);
				else if (param is DirectoryItemViewModel dir) PinToQuickAccess(dir.FullPath);
				else if (param is FolderItemViewModel folder) PinToQuickAccess(folder.FullPath);
			});
			UnpinFromQuickAccessCommand = new RelayCommand(param => {
				if (param is string path) UnpinFromQuickAccess(path);
				else if (param is DirectoryItemViewModel dir) UnpinFromQuickAccess(dir.FullPath);
				else if (param is FolderItemViewModel folder) UnpinFromQuickAccess(folder.FullPath);
			});
			MovePinUpCommand = new RelayCommand(param => MovePin(param, -1));
			MovePinDownCommand = new RelayCommand(param => MovePin(param, 1));

			AddTab();
		}

		private void UpdateSystemStatus(object? sender, EventArgs e) {
#if DEBUG
			try {
				_currentProcess.Refresh();
				var now = DateTime.Now;
				var currentProcessorTime = _currentProcess.TotalProcessorTime;
				var cpuUsage = (currentProcessorTime - _lastProcessorTime).TotalMilliseconds / (now - _lastTimerTick).TotalMilliseconds / Environment.ProcessorCount * 100;
				_lastProcessorTime = currentProcessorTime;
				_lastTimerTick = now;

				long memory = _currentProcess.PrivateMemorySize64;
				SystemStatus = $"CPU: {cpuUsage:0}%   Mem: {FileItemViewModel.FormatSize(memory)}";
			}
			catch { }
#endif
		}

		private void MovePin(object? param, int direction) {
			string? path = null;
			if (param is DirectoryItemViewModel dir) path = dir.FullPath;

			if (path != null) {
				var list = AppSettings.Current.PinnedFolders;
				int index = list.FindIndex(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
				if (index >= 0) {
					int newIndex = index + direction;
					if (newIndex >= 0 && newIndex < list.Count) {
						var item = list[index];
						list.RemoveAt(index);
						list.Insert(newIndex, item);
						AppSettings.Save();
						LoadQuickAccess();
					}
				}
			}
		}

		public static void ClearSelection(IEnumerable<DirectoryItemViewModel> items) {
			foreach (var item in items) {
				if (item.IsSelected) item.IsSelected = false;
				ClearSelection(item.SubDirectories);
			}
		}

		public ObservableCollection<DirectoryItemViewModel> Drives {
			get => _drives;
			set => SetProperty(ref _drives, value);
		}

		public ObservableCollection<DirectoryItemViewModel> QuickAccess {
			get => _quickAccess;
			set => SetProperty(ref _quickAccess, value);
		}

		public ObservableCollection<TabViewModel> Tabs {
			get => _tabs;
			set => SetProperty(ref _tabs, value);
		}

		public TabViewModel? SelectedTab {
			get => _selectedTab;
			set => SetProperty(ref _selectedTab, value);
		}

		public void ReorderQuickAccess(DirectoryItemViewModel source, DirectoryItemViewModel target) {
			var list = AppSettings.Current.PinnedFolders;
			int oldIndex = list.FindIndex(p => p.Equals(source.FullPath, StringComparison.OrdinalIgnoreCase));
			int newIndex = list.FindIndex(p => p.Equals(target.FullPath, StringComparison.OrdinalIgnoreCase));

			if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex) {
				var item = list[oldIndex];
				list.RemoveAt(oldIndex);
				list.Insert(newIndex, item);
				AppSettings.Save();
				LoadQuickAccess();
			}
		}

		private void AddTab(string? path = null) {
			var newTab = new TabViewModel(this);
			Tabs.Add(newTab);
			SelectedTab = newTab;
			newTab.NavigateTo(path ?? "This PC");
		}

		private void CloseTab(TabViewModel? tab) {
			if (tab == null) return;

			if (tab == SelectedTab) {
				int index = Tabs.IndexOf(tab);
				if (index > 0) {
					SelectedTab = Tabs[index - 1];
				}
				else if (Tabs.Count > 1) {
					SelectedTab = Tabs[index + 1];
				}
			}

			_ = Tabs.Remove(tab);
			if (Tabs.Count == 0) Application.Current.Shutdown();
		}

		private void LoadQuickAccess() {
			QuickAccess.Clear();

			if (AppSettings.Current.PinnedFolders.Count == 0) {
				var defaults = new[] {
					Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
					Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
					Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
					Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
					Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
					Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads"
				};

				foreach (var path in defaults) {
					if (Directory.Exists(path) && !AppSettings.Current.PinnedFolders.Any(p => p.Equals(path, StringComparison.OrdinalIgnoreCase))) {
						AppSettings.Current.PinnedFolders.Add(path);
					}
				}
				AppSettings.Save();
			}

			foreach (var path in AppSettings.Current.PinnedFolders) {
				if (Directory.Exists(path)) {
					QuickAccess.Add(new DirectoryItemViewModel(path));
				}
			}
		}

		public void PinToQuickAccess(string path) {
			if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
			if (!AppSettings.Current.PinnedFolders.Any(p => p.Equals(path, StringComparison.OrdinalIgnoreCase))) {
				AppSettings.Current.PinnedFolders.Add(path);
				AppSettings.Save();
				LoadQuickAccess();
			}
		}

		public void UnpinFromQuickAccess(string path) {
			var item = AppSettings.Current.PinnedFolders.FirstOrDefault(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
			if (item != null) {
				_ = AppSettings.Current.PinnedFolders.Remove(item);
				AppSettings.Save();
				LoadQuickAccess();
			}
		}

		private void LoadDrives() {
			foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady)) {
				Drives.Add(new DirectoryItemViewModel(drive.RootDirectory.FullName, drive.Name, drive.VolumeLabel, true));
			}
		}
	}
}
