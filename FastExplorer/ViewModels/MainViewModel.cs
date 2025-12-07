using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using FastExplorer.Helpers;

namespace FastExplorer.ViewModels {
	public class MainViewModel : ViewModelBase {
		private ObservableCollection<DirectoryItemViewModel> _drives;
		private ObservableCollection<DirectoryItemViewModel> _quickAccess;
		private ObservableCollection<TabViewModel> _tabs;
		private TabViewModel? _selectedTab;
		private RecycleBinItemViewModel _recycleBin;

		public ICommand AddTabCommand { get; }
		public ICommand CloseTabCommand { get; }
		public ICommand NavigateToThisPCCommand { get; }
		public ICommand NavigateToRecycleBinCommand { get; }
		public ICommand OpenOptionsCommand { get; }
		public ICommand PinToQuickAccessCommand { get; }
		public ICommand UnpinFromQuickAccessCommand { get; }
		public ICommand MovePinUpCommand { get; }
		public ICommand MovePinDownCommand { get; }
		public ICommand AddNetworkShareCommand { get; }
		public ICommand RemoveNetworkShareCommand { get; }

		private string _systemStatus = "CPU: 0%   Mem: 0 MB";
		public string SystemStatus {
			get => _systemStatus;
			set => SetProperty(ref _systemStatus, value);
		}

		public RecycleBinItemViewModel RecycleBin {
			get => _recycleBin;
			set => SetProperty(ref _recycleBin, value);
		}

		public static bool IsDebug {
			get {
#if DEBUG
				return true;
#else
				return false;
#endif
			}
		}

#if DEBUG
		private readonly System.Windows.Threading.DispatcherTimer _statusTimer;
		private readonly Process _currentProcess;
		private TimeSpan _lastProcessorTime;
		private DateTime _lastTimerTick;
#endif

		public MainViewModel() {
			_drives = [];
			_quickAccess = [];
			_tabs = [];
			_recycleBin = new RecycleBinItemViewModel();

#if DEBUG
			_currentProcess = Process.GetCurrentProcess();
			_lastProcessorTime = _currentProcess.TotalProcessorTime;
			_lastTimerTick = DateTime.Now;
			_statusTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
			_statusTimer.Tick += UpdateSystemStatus;
			_statusTimer.Start();
#endif

			_ = LoadQuickAccessAsync();
			_ = LoadDrivesAsync();

			AddTabCommand = new RelayCommand(param => AddTab(param as string));
			CloseTabCommand = new RelayCommand(param => CloseTab(param as TabViewModel));
			NavigateToThisPCCommand = new RelayCommand(_ => {
				SelectedTab?.NavigateTo("This PC");
				ClearSelection(QuickAccess);
				ClearSelection(Drives);
				RecycleBin?.IsSelected = false;
				var thisPC = Drives.FirstOrDefault(d => d.FullPath == "This PC");
				if (thisPC != null) thisPC.IsSelected = true;
			});
			NavigateToRecycleBinCommand = new RelayCommand(_ => {
				SelectedTab?.NavigateTo(RecycleBin.FullPath);
				ClearSelection(QuickAccess);
				ClearSelection(Drives);
				RecycleBin.IsSelected = true;
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
			AddNetworkShareCommand = new RelayCommand(_ => AddNetworkShare());
			RemoveNetworkShareCommand = new RelayCommand(param => RemoveNetworkShare(param));

			AddTab();
		}

		private async void AddNetworkShare() {
			var input = new InputWindow { Owner = Application.Current.MainWindow };
			if (input.ShowDialog() == true) {
				string path = input.ResponseText;
				if (!string.IsNullOrWhiteSpace(path)) {
					if (!path.StartsWith(@"\\")) {
						path = @"\\" + path.TrimStart('\\');
					}

					if (AppSettings.Current.NetworkShares.Contains(path, StringComparer.OrdinalIgnoreCase)) {
						MessageBox.Show("This network location is already added.", "FastExplorer", MessageBoxButton.OK, MessageBoxImage.Information);
						return;
					}

					bool authenticated = false;
					try {
						await Task.Run(() => {
							if (Directory.Exists(path)) {
								authenticated = true;
							}
							else {
								var creds = CredentialStore.GetCredentials(path);
								if (creds != null) {
									NetworkHelper.ConnectToShare(path, creds.Value.Username, creds.Value.Password);
									if (Directory.Exists(path)) authenticated = true;
								}
							}
						});
					}
					catch { }

					if (!authenticated) {
						var dialog = new CredentialsWindow { Owner = Application.Current.MainWindow };
						if (dialog.ShowDialog() == true) {
							try {
								NetworkHelper.ConnectToShare(path, dialog.Username, dialog.Password);
								if (dialog.RememberMe) {
									CredentialStore.SaveCredentials(path, dialog.Username, dialog.Password);
								}
								authenticated = true;
							}
							catch (Exception ex) {
								MessageBox.Show($"Connection failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
							}
						}
					}

					if (authenticated) {
						AppSettings.Current.NetworkShares.Add(path);
						AppSettings.Save();
						_ = LoadDrivesAsync();
						if (SelectedTab?.CurrentPath == "This PC") {
							SelectedTab.RefreshCommand.Execute(null);
						}
					}
				}
			}
		}

		private void RemoveNetworkShare(object? param) {
			string? path = null;
			if (param is DirectoryItemViewModel d) path = d.FullPath;
			else if (param is NetworkShareItemViewModel n) path = n.FullPath;

			if (path != null) {
				if (AppSettings.Current.NetworkShares.Remove(path)) {
					AppSettings.Save();
					_ = LoadDrivesAsync();
					if (SelectedTab?.CurrentPath == "This PC") {
						SelectedTab.RefreshCommand.Execute(null);
					}
				}
			}
		}

#if DEBUG
		private void UpdateSystemStatus(object? sender, EventArgs e) {
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
		}
#endif

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
						_ = LoadQuickAccessAsync();
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

		public void OnTabNavigated(TabViewModel tab) {
			if (tab == SelectedTab) {
				UpdateSidebarSelection(tab.CurrentPath);
			}
		}

		private bool _isSidebarUpdating;
		public bool IsSidebarUpdating => _isSidebarUpdating;

		private void UpdateSidebarSelection(string path) {
			if (_isSidebarUpdating) return;
			_isSidebarUpdating = true;
			try {
				ClearSelection(QuickAccess);
				ClearSelection(Drives);
				RecycleBin?.IsSelected = false;

				if (path == "This PC") {
					var thisPC = Drives.FirstOrDefault(d => d.FullPath == "This PC");
					if (thisPC != null) thisPC.IsSelected = true;
					return;
				}

				if (RecycleBin != null && (path == RecycleBin.FullPath || path.StartsWith("shell:RecycleBinFolder") || path.StartsWith("::{645FF040-5081-101B-9F08-00AA002F954E}"))) {
					RecycleBin.IsSelected = true;
					return;
				}

				foreach (var item in QuickAccess) {
					if (item.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase)) {
						item.IsSelected = true;
						return;
					}
				}

				var root = Drives.FirstOrDefault(d => d.FullPath == "This PC");
				if (root != null) {
					foreach (var drive in root.SubDirectories) {
						if (path.Equals(drive.FullPath, StringComparison.OrdinalIgnoreCase)) {
							drive.IsSelected = true;
							return;
						}
					}
				}
			}
			finally {
				_isSidebarUpdating = false;
			}
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
				_ = LoadQuickAccessAsync();
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

		private async Task LoadQuickAccessAsync() {
			QuickAccess.Clear();

			await Task.Run(() => {
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
			});

			var items = await Task.Run(() => {
				var list = new List<DirectoryItemViewModel>();
				foreach (var path in AppSettings.Current.PinnedFolders) {
					if (Directory.Exists(path)) {
						list.Add(new DirectoryItemViewModel(path));
					}
				}
				return list;
			});

			foreach (var item in items) {
				QuickAccess.Add(item);
			}
		}

		public void PinToQuickAccess(string path) {
			if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
			if (!AppSettings.Current.PinnedFolders.Any(p => p.Equals(path, StringComparison.OrdinalIgnoreCase))) {
				AppSettings.Current.PinnedFolders.Add(path);
				AppSettings.Save();
				_ = LoadQuickAccessAsync();
			}
		}

		public void UnpinFromQuickAccess(string path) {
			var item = AppSettings.Current.PinnedFolders.FirstOrDefault(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
			if (item != null) {
				_ = AppSettings.Current.PinnedFolders.Remove(item);
				AppSettings.Save();
				_ = LoadQuickAccessAsync();
			}
		}

		private async Task LoadDrivesAsync() {
			try {
				var drives = await Task.Run(async () => {
					var list = new List<DirectoryItemViewModel>();
					
					var driveTasks = DriveInfo.GetDrives().Select(d => Task.Run(() => {
						try {
							if (d.IsReady) {
								return new DirectoryItemViewModel(d.RootDirectory.FullName, d.Name, d.VolumeLabel, true);
							}
						}
						catch { }
						return null;
					}));

					var driveResults = await Task.WhenAll(driveTasks);
					foreach (var item in driveResults) {
						if (item != null) list.Add(item);
					}

					foreach (var path in AppSettings.Current.NetworkShares) {
						list.Add(new DirectoryItemViewModel(path, null, null, false, true));
					}
					foreach (var distro in WslHelper.GetDistributions()) {
						list.Add(new DirectoryItemViewModel($@"\\wsl.localhost\{distro}", distro, null, true, false));
					}
					return list;
				});

				Application.Current.Dispatcher.Invoke(() => {
					Drives.Clear();
					var thisPC = new DirectoryItemViewModel("This PC");
					foreach (var drive in drives) {
						thisPC.SubDirectories.Add(drive);
						_ = drive.LoadDriveDetailsAsync();
					}
					thisPC.IsExpanded = true;
					Drives.Add(thisPC);
				});
			}
			catch { }
		}
	}
}
