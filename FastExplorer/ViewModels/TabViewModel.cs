using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

using CommunityToolkit.HighPerformance.Buffers;

using FastExplorer.Helpers;

namespace FastExplorer.ViewModels {
	public class TabViewModel : ViewModelBase {
		private readonly MainViewModel _mainViewModel;
		private string _currentPath = string.Empty;
		private string _tabName = "Home";
		private ImageSource? _icon;
		private ObservableCollection<FileItemViewModel> _files;
		private List<FileItemViewModel> _allFiles;
		private DirectoryItemViewModel? _selectedDirectory;
		private string _statusText = "Ready";
		private int _itemCount;
		private string _searchText = string.Empty;
		private readonly Stack<string> _backHistory = new();
		private readonly Stack<string> _forwardHistory = new();
		private bool _isNavigating;
		private CancellationTokenSource? _searchCts;
		private string _sortProperty = AppSettings.Current.SortProperty;
		private ListSortDirection _sortDirection = AppSettings.Current.SortDirection == "Ascending" ? ListSortDirection.Ascending : ListSortDirection.Descending;
		private double _itemSize = AppSettings.Current.ItemSize;
		private bool _isSearching;
		private ObservableCollection<PathSegmentViewModel> _pathSegments = [];
		private bool _isPathEditing;
		private string _addressBarText = string.Empty;
		private ObservableCollection<string> _suggestions = [];
		private List<FileItemViewModel> _selectedItems = [];

		public ICommand NavigateCommand { get; }
		public ICommand BackCommand { get; }
		public ICommand ForwardCommand { get; }
		public ICommand OpenCommand { get; }
		public ICommand CopyPathCommand { get; }
		public ICommand ShowInExplorerCommand { get; }
		public ICommand SetViewCommand { get; }
		public ICommand SortFilesCommand { get; }
		public ICommand CutCommand { get; }
		public ICommand CopyCommand { get; }
		public ICommand PasteCommand { get; }
		public ICommand RenameCommand { get; }
		public ICommand CommitRenameCommand { get; }
		public ICommand CancelRenameCommand { get; }
		public ICommand ShareCommand { get; }
		public ICommand DeleteCommand { get; }
		public ICommand PropertiesCommand { get; }
		public ICommand RefreshCommand { get; }
		public ICommand TogglePathEditCommand { get; }

		private bool _isRefreshing;
		public bool IsRefreshing {
			get => _isRefreshing;
			set => SetProperty(ref _isRefreshing, value);
		}

		private bool _isLoading;
		public bool IsLoading {
			get => _isLoading;
			set => SetProperty(ref _isLoading, value);
		}

		public bool IsThisPC => CurrentPath == "This PC";

		public TabViewModel(MainViewModel mainViewModel) {
			_mainViewModel = mainViewModel;
			_files = [];
			_allFiles = [];

			NavigateCommand = new RelayCommand(param => NavigateTo(param as string));
			BackCommand = new RelayCommand(_ => GoBack(), _ => _backHistory.Count > 0);
			ForwardCommand = new RelayCommand(_ => GoForward(), _ => _forwardHistory.Count > 0);
			OpenCommand = new RelayCommand(OpenItem);
			CopyPathCommand = new RelayCommand(param => {
				if (param is FileItemViewModel item) {
					try { Clipboard.SetText(item.FullPath); } catch { }
				}
			});
			ShowInExplorerCommand = new RelayCommand(param => {
				if (param is FileItemViewModel item) {
					try { _ = Process.Start("explorer.exe", $"/select,\"{item.FullPath}\""); } catch { }
				}
			});
			SetViewCommand = new RelayCommand(param => {
				if (double.TryParse(param?.ToString(), out double size)) {
					ItemSize = size;
				}
			});
			SortFilesCommand = new RelayCommand(param => SortFiles(param as string));

			CutCommand = new RelayCommand(param => {
				var itemsToCut = new List<string>();
				if (param is FileItemViewModel item) {
					if (_selectedItems.Contains(item)) itemsToCut.AddRange(_selectedItems.Select(x => x.FullPath));
					else itemsToCut.Add(item.FullPath);
				}
				else if (_selectedItems.Count > 0) {
					itemsToCut.AddRange(_selectedItems.Select(x => x.FullPath));
				}

				if (itemsToCut.Count > 0) {
					try {
						var list = new System.Collections.Specialized.StringCollection();
						list.AddRange([.. itemsToCut]);
						var data = new DataObject();
						data.SetFileDropList(list);
						data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(5)));
						Clipboard.SetDataObject(data, true);
					}
					catch { }
				}
			});
			CopyCommand = new RelayCommand(param => {
				var itemsToCopy = new List<string>();
				if (param is FileItemViewModel item) {
					if (_selectedItems.Contains(item)) itemsToCopy.AddRange(_selectedItems.Select(x => x.FullPath));
					else itemsToCopy.Add(item.FullPath);
				}
				else if (_selectedItems.Count > 0) {
					itemsToCopy.AddRange(_selectedItems.Select(x => x.FullPath));
				}

				if (itemsToCopy.Count > 0) {
					try {
						var list = new System.Collections.Specialized.StringCollection();
						list.AddRange([.. itemsToCopy]);
						Clipboard.SetFileDropList(list);

						var data = new DataObject();
						data.SetFileDropList(list);
						data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(2)));
						Clipboard.SetDataObject(data, true);
					}
					catch { }
				}
			});
			PasteCommand = new RelayCommand(async _ => {
				if (Clipboard.ContainsFileDropList()) {
					var files = Clipboard.GetFileDropList();
					bool isMove = false;
					try {
						var data = Clipboard.GetDataObject();
						if (data != null && data.GetDataPresent("Preferred DropEffect")) {
							if (data.GetData("Preferred DropEffect") is MemoryStream stream) {
								byte[] bytes = stream.ToArray();
								if (bytes.Length > 0 && bytes[0] == 5) isMove = true;
							}
						}
					}
					catch { }

					foreach (string? sourcePath in files) {
						if (sourcePath == null) continue;
						if (isMove) ShellHelper.MoveFile(sourcePath, CurrentPath);
						else ShellHelper.CopyFile(sourcePath, CurrentPath);
					}
					await LoadFilesAsync(CurrentPath);
				}
			}, _ => Clipboard.ContainsFileDropList() && !string.IsNullOrEmpty(CurrentPath) && CurrentPath != "This PC" && !CurrentPath.StartsWith("shell:RecycleBinFolder") && !CurrentPath.Contains("645FF040-5081-101B-9F08-00AA002F954E"));
			RenameCommand = new RelayCommand(param => {
				var itemToRename = param as FileItemViewModel;
				if (itemToRename == null && _selectedItems.Count == 1) itemToRename = _selectedItems[0];

				if (itemToRename != null) {
					itemToRename.RenameText = itemToRename.Name;
					itemToRename.IsRenaming = true;
				}
			});
			CommitRenameCommand = new RelayCommand(async param => {
				if (param is FileItemViewModel item) {
					item.IsRenaming = false;
					if (item.RenameText != item.Name && !string.IsNullOrWhiteSpace(item.RenameText)) {
						string newName = item.RenameText;
						string oldExt = Path.GetExtension(item.Name);
						string newExt = Path.GetExtension(newName);

						if (!string.Equals(oldExt, newExt, StringComparison.OrdinalIgnoreCase)) {
							var result = MessageBox.Show(
								"If you change a file name extension, the file might become unusable.\nAre you sure you want to change it?",
								"Rename",
								MessageBoxButton.YesNo,
								MessageBoxImage.Warning);

							if (result != MessageBoxResult.Yes) {
								item.RenameText = item.Name;
								return;
							}
						}

						string newPath = Path.Combine(Path.GetDirectoryName(item.FullPath)!, newName);
						try {
							if (item.IsFolder) Directory.Move(item.FullPath, newPath);
							else File.Move(item.FullPath, newPath);
							await LoadFilesAsync(CurrentPath);
						}
						catch (Exception ex) {
							MessageBox.Show($"Error renaming: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
						}
					}
				}
			});
			CancelRenameCommand = new RelayCommand(param => {
				if (param is FileItemViewModel item) {
					item.IsRenaming = false;
					item.RenameText = item.Name;
				}
			});
			ShareCommand = new RelayCommand(param => { });
			DeleteCommand = new RelayCommand(param => {
				var itemsToDelete = new List<FileItemViewModel>();
				if (param is FileItemViewModel item) {
					if (_selectedItems.Contains(item)) itemsToDelete.AddRange(_selectedItems);
					else itemsToDelete.Add(item);
				}
				else if (_selectedItems.Count > 0) {
					itemsToDelete.AddRange(_selectedItems);
				}

				if (itemsToDelete.Count > 0) {
					string message = itemsToDelete.Count == 1
						? $"Are you sure you want to move '{itemsToDelete[0].Name}' to the Recycle Bin?"
						: $"Are you sure you want to move these {itemsToDelete.Count} items to the Recycle Bin?";

					if (MessageBox.Show(message, "Delete File", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) {
						try {
							foreach (var i in itemsToDelete) {
								ShellHelper.DeleteToRecycleBin(i.FullPath);
							}
							_ = LoadFilesAsync(CurrentPath);
						}
						catch (Exception ex) {
							_ = MessageBox.Show($"Error deleting: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
						}
					}
				}
			});
			PropertiesCommand = new RelayCommand(param => {
				if (param is FileItemViewModel item) {
					_ = ShellHelper.ShowFileProperties(item.FullPath);
				}
			});

			RefreshCommand = new RelayCommand(async _ => {
				IsRefreshing = true;
				await LoadFilesAsync(CurrentPath);
				await Task.Delay(500);
				IsRefreshing = false;
			}, _ => !IsSearchActive);
			TogglePathEditCommand = new RelayCommand(param => IsPathEditing = !IsPathEditing);
		}

		public string TabName {
			get => _tabName;
			set => SetProperty(ref _tabName, value);
		}

		public ImageSource? Icon {
			get => _icon;
			set => SetProperty(ref _icon, value);
		}

		public ObservableCollection<FileItemViewModel> Files {
			get => _files;
			set => SetProperty(ref _files, value);
		}

		public DirectoryItemViewModel? SelectedDirectory {
			get => _selectedDirectory;
			set => SetProperty(ref _selectedDirectory, value);
		}

		public string CurrentPath {
			get => _currentPath;
			set {
				if (SetProperty(ref _currentPath, value)) {
					if (!_isNavigating) {
						NavigateTo(value);
					}
					OnPropertyChanged(nameof(CanSortOrView));
					OnPropertyChanged(nameof(IsThisPC));
				}
			}
		}

		public string SearchText {
			get => _searchText;
			set {
				if (SetProperty(ref _searchText, value)) {
					OnPropertyChanged(nameof(IsSearchActive));
					CommandManager.InvalidateRequerySuggested();

					_searchCts?.Cancel();
					if (string.IsNullOrWhiteSpace(value)) {
						IsSearching = false;
						bool shouldGc = Files.Count > 2000;
						FilterFiles();
						if (shouldGc) {
							GC.Collect();
							GC.WaitForPendingFinalizers();
							GC.Collect();
						}
					}
					else {
						_searchCts = new CancellationTokenSource();
						var token = _searchCts.Token;
						_ = Task.Delay(300, token).ContinueWith(t => {
							if (t.IsCanceled) return;
							Application.Current.Dispatcher.Invoke(() => {
								if (SearchText == value) _ = PerformSearchAsync(value);
							});
						}, token);
					}
				}
			}
		}

		public bool IsSearchActive => !string.IsNullOrWhiteSpace(SearchText);

		public string StatusText {
			get => _statusText;
			set => SetProperty(ref _statusText, value);
		}

		public int ItemCount {
			get => _itemCount;
			set => SetProperty(ref _itemCount, value);
		}

		public double ItemSize {
			get => _itemSize;
			set {
				if (SetProperty(ref _itemSize, value)) {
					AppSettings.Current.ItemSize = value;
					if (!string.IsNullOrEmpty(CurrentPath)) {
						if (!AppSettings.Current.FolderViewStates.TryGetValue(CurrentPath, out var state)) {
							state = new FolderViewState {
								SortProperty = _sortProperty,
								SortDirection = _sortDirection == ListSortDirection.Ascending ? "Ascending" : "Descending"
							};
							AppSettings.Current.FolderViewStates[CurrentPath] = state;
						}
						state.ItemSize = value;
					}
					OnPropertyChanged(nameof(IsDetailsView));
					foreach (var file in Files) {
						file.RefreshIcon((int)value);
					}
				}
			}
		}

		public bool IsDetailsView => ItemSize <= 20;

		public bool IsSearching {
			get => _isSearching;
			set {
				if (SetProperty(ref _isSearching, value)) {
					OnPropertyChanged(nameof(CanSortOrView));
					CommandManager.InvalidateRequerySuggested();
				}
			}
		}

		public bool CanSortOrView => !IsSearching && CurrentPath != "This PC";

		public ObservableCollection<PathSegmentViewModel> PathSegments {
			get => _pathSegments;
			set => SetProperty(ref _pathSegments, value);
		}

		public bool IsPathEditing {
			get => _isPathEditing;
			set {
				if (SetProperty(ref _isPathEditing, value)) {
					if (value) {
						AddressBarText = CurrentPath;
					}
					else {
						Suggestions.Clear();
					}
				}
			}
		}

		public string AddressBarText {
			get => _addressBarText;
			set {
				if (SetProperty(ref _addressBarText, value)) {
					UpdateSuggestions();
				}
			}
		}

		public ObservableCollection<string> Suggestions {
			get => _suggestions;
			set {
				if (SetProperty(ref _suggestions, value)) {
					OnPropertyChanged(nameof(HasSuggestions));
				}
			}
		}

		public bool HasSuggestions => Suggestions.Count > 0;

		private void UpdateSuggestions() {
			if (!IsPathEditing || string.IsNullOrEmpty(AddressBarText)) {
				Suggestions.Clear();
				return;
			}

			try {
				string path = AddressBarText;
				string dir;
				string filter;

				if (path.EndsWith('\\')) {
					dir = path;
					filter = "";
				}
				else if (path.Length == 2 && path[1] == ':') {
					dir = path + "\\";
					filter = "";
				}
				else {
					dir = Path.GetDirectoryName(path) ?? "";
					filter = Path.GetFileName(path) ?? "";
				}

				if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) {
					var opts = new EnumerationOptions { IgnoreInaccessible = true };
					var dirs = Directory.EnumerateDirectories(dir, filter + "*", opts).Take(10);
					Suggestions = new ObservableCollection<string>(dirs);
				}
				else {
					Suggestions.Clear();
				}
			}
			catch {
				Suggestions.Clear();
			}
		}

		public void SortFiles(string? property) {
			if (string.IsNullOrEmpty(property)) return;

			if (_sortProperty == property) {
				_sortDirection = _sortDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
			}
			else {
				_sortProperty = property;
				_sortDirection = ListSortDirection.Ascending;
			}

			AppSettings.Current.SortProperty = _sortProperty;
			AppSettings.Current.SortDirection = _sortDirection == ListSortDirection.Ascending ? "Ascending" : "Descending";

			if (!string.IsNullOrEmpty(CurrentPath)) {
				if (!AppSettings.Current.FolderViewStates.TryGetValue(CurrentPath, out var state)) {
					state = new FolderViewState { ItemSize = _itemSize };
					AppSettings.Current.FolderViewStates[CurrentPath] = state;
				}
				state.SortProperty = _sortProperty;
				state.SortDirection = AppSettings.Current.SortDirection;
			}

			ApplySort();
		}

		private void ApplySort() {
			if (Files.Count == 0) return;

			Application.Current.Dispatcher.Invoke(() => {
				var view = CollectionViewSource.GetDefaultView(Files);
				view.SortDescriptions.Clear();

				if (IsDownloadsFolder(CurrentPath)) {
					if (view.GroupDescriptions.Count == 0) {
						view.GroupDescriptions.Add(new PropertyGroupDescription("DateGroupKey"));
					}
					view.SortDescriptions.Add(new SortDescription("DateGroupKey", ListSortDirection.Descending));
					view.SortDescriptions.Add(new SortDescription("IsFolder", ListSortDirection.Descending));

					if (_sortProperty != "Date modified") {
						view.SortDescriptions.Add(new SortDescription(_sortProperty, _sortDirection));
					}
					else {
						view.SortDescriptions.Add(new SortDescription("DateModified", _sortDirection));
					}
				}
				else if (CurrentPath == "This PC") {
					view.GroupDescriptions.Clear();
					view.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
					view.SortDescriptions.Add(new SortDescription("Category", ListSortDirection.Ascending));
					view.SortDescriptions.Add(new SortDescription("FullPath", ListSortDirection.Ascending));
				}
				else {
					view.GroupDescriptions.Clear();
					view.SortDescriptions.Add(new SortDescription("IsFolder", ListSortDirection.Descending));
					view.SortDescriptions.Add(new SortDescription(_sortProperty, _sortDirection));
				}
			});
		}

		public void NavigateTo(string? path) {
			if (string.IsNullOrEmpty(path)) return;

			if (!string.IsNullOrEmpty(SearchText)) {
				SearchText = string.Empty;
			}

			if (path.Length == 2 && path[1] == ':') {
				path += "\\";
			}

			bool isThisPC = path == "This PC";
			bool isRecycleBin = path.StartsWith("::") || path.StartsWith("shell:");
			bool isNetwork = path.StartsWith(@"\\");

			if (!isThisPC && !isRecycleBin && !isNetwork && !Directory.Exists(path)) return;

			if (_currentPath == path && Files.Count > 0) return;

			if (!_isNavigating && !string.IsNullOrEmpty(_currentPath)) {
				_backHistory.Push(_currentPath);
				_forwardHistory.Clear();
			}

			_isNavigating = true;
			CurrentPath = path;
			AddressBarText = path;

			Files.Clear();
			_allFiles.Clear();

			_mainViewModel.OnTabNavigated(this);

			if (AppSettings.Current.FolderViewStates.TryGetValue(path, out var state)) {
				if (_itemSize != state.ItemSize) {
					_itemSize = state.ItemSize;
					OnPropertyChanged(nameof(ItemSize));
					OnPropertyChanged(nameof(IsDetailsView));
				}
				_sortProperty = state.SortProperty;
				_sortDirection = state.SortDirection == "Ascending" ? ListSortDirection.Ascending : ListSortDirection.Descending;
			}

			if (isThisPC) {
				TabName = "This PC";
				Icon = IconHelper.GetFolderIcon("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", false, 16);
			}
			else if (isRecycleBin) {
				TabName = "Recycle Bin";
				Icon = IconHelper.GetThumbnail("shell:RecycleBinFolder", 16);
			}
			else {
				string name = new DirectoryInfo(path).Name;
				TabName = StringPool.Shared.GetOrAdd(string.IsNullOrEmpty(name) ? path : name);
				Icon = IconHelper.GetFolderIcon(path, false, 16);
			}

			UpdatePathSegments(path);
			IsPathEditing = false;
			_isNavigating = false;

			_ = LoadFilesAsync(path);
		}

		private void UpdatePathSegments(string path) {
			var segments = new List<PathSegmentViewModel>();

			if (path == "This PC") {
				segments.Add(new PathSegmentViewModel("This PC", "This PC", NavigateCommand));
			}
			else if (path.StartsWith("::") || path.StartsWith("shell:")) {
				segments.Add(new PathSegmentViewModel("This PC", "This PC", NavigateCommand));
				segments.Add(new PathSegmentViewModel("Recycle Bin", path, NavigateCommand));
			}
			else {
				var stack = new Stack<PathSegmentViewModel>();
				var dir = new DirectoryInfo(path);

				while (dir != null) {
					stack.Push(new PathSegmentViewModel(dir.Name.TrimEnd('\\'), dir.FullName, NavigateCommand));
					dir = dir.Parent;
				}

				segments.Add(new PathSegmentViewModel("This PC", "This PC", NavigateCommand));
				while (stack.Count > 0) {
					segments.Add(stack.Pop());
				}
			}

			PathSegments = new ObservableCollection<PathSegmentViewModel>(segments);
		}

		private void GoBack() {
			if (_backHistory.Count > 0) {
				_forwardHistory.Push(_currentPath);
				var path = _backHistory.Pop();
				_isNavigating = true;
				CurrentPath = path;
				string name = path == "This PC" ? "This PC" : new DirectoryInfo(path).Name;
				TabName = StringPool.Shared.GetOrAdd(string.IsNullOrEmpty(name) ? path : name);
				Icon = IconHelper.GetFolderIcon(path == "This PC" ? "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}" : path, false, 16);
				_isNavigating = false;
				_ = LoadFilesAsync(path);
			}
		}

		private void GoForward() {
			if (_forwardHistory.Count > 0) {
				_backHistory.Push(_currentPath);
				var path = _forwardHistory.Pop();
				_isNavigating = true;
				CurrentPath = path;
				string name = path == "This PC" ? "This PC" : new DirectoryInfo(path).Name;
				TabName = StringPool.Shared.GetOrAdd(string.IsNullOrEmpty(name) ? path : name);
				Icon = IconHelper.GetFolderIcon(path == "This PC" ? "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}" : path, false, 16);
				_isNavigating = false;
				_ = LoadFilesAsync(path);
			}
		}

		private void OpenItem(object? parameter) {
			if (parameter is FileItemViewModel fileItem) {
				if (CurrentPath.StartsWith("shell:RecycleBinFolder") || CurrentPath.Contains("645FF040-5081-101B-9F08-00AA002F954E")) {
					_ = ShellHelper.ShowFileProperties(fileItem.FullPath);
					return;
				}

				if (fileItem.IsFolder) {
					NavigateTo(fileItem.FullPath);
				}
				else {
					try {
						var psi = new ProcessStartInfo(fileItem.FullPath) {
							UseShellExecute = true,
							WorkingDirectory = Path.GetDirectoryName(fileItem.FullPath)
						};
						_ = Process.Start(psi);
					}
					catch (Exception ex) {
						_ = MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					}
				}
			}
		}

		private async Task LoadFilesAsync(string path) {
			IsLoading = true;
			try {
				if (path != CurrentPath) {
				Files.Clear();
				_allFiles.Clear();
			}
			StatusText = "Loading...";

			if (path == "This PC") {
				TabName = "This PC";
				await Task.Run(() => {
					var list = new List<FileItemViewModel>();

					foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady)) {
						list.Add(new DriveItemViewModel(drive));
					}

					foreach (var share in AppSettings.Current.NetworkShares) {
						list.Add(new NetworkShareItemViewModel(share));
					}

					foreach (var distro in WslHelper.GetDistributions()) {
						list.Add(new WslDistroItemViewModel(distro));
					}

					Application.Current.Dispatcher.Invoke(() => {
						_allFiles = list;
						Files = new ObservableCollection<FileItemViewModel>(list);
						StatusText = $"{Files.Count} items";
						ItemCount = Files.Count;
						ApplySort();
					});
				});
				return;
			}

			if (path.StartsWith("::") || path.StartsWith("shell:")) {
				await Task.Run(() => {
					var list = new List<FileItemViewModel>();
					try {
						var items = ShellHelper.EnumerateShellFolder(path);
						foreach (var item in items) {
							list.Add(new ShellFileItemViewModel(item.Name, item.Path, item.IsFolder, item.Size, item.DateModified, item.Type, item.OriginalLocation));
						}
					}
					catch { }

					Application.Current.Dispatcher.Invoke(() => {
						_allFiles = list;
						Files = new ObservableCollection<FileItemViewModel>(list);
						StatusText = $"{Files.Count} items";
						ItemCount = Files.Count;
						
						foreach (var file in Files) {
							file.RefreshIcon((int)ItemSize);
						}
						ApplySort();
					});
				});
				return;
			}

			try {
				var directoryInfo = new DirectoryInfo(path);
				var fileList = await Task.Run(() => {
					var list = new List<FileItemViewModel>();
					try {
						foreach (var dir in directoryInfo.GetDirectories()) {
							list.Add(new FolderItemViewModel(dir));
						}
						foreach (var file in directoryInfo.GetFiles()) {
							if (!file.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) {
								list.Add(new FileItemViewModel(file));
							}
						}
					}
					catch { }
					return list;
				});

				var currentSize = (int)ItemSize;
				foreach (var item in fileList) {
					item.RefreshIcon(currentSize);
				}

				_allFiles = fileList;

				Application.Current.Dispatcher.Invoke(() => {
					Files = new ObservableCollection<FileItemViewModel>(_allFiles);

					ApplySort();

					ItemCount = Files.Count;
					StatusText = $"{ItemCount} items";
				});

				Application.Current.Dispatcher.Invoke(() => {
					foreach (var file in Files) {
						file.RefreshIcon((int)ItemSize);
					}
					ApplySort();
				});
			}
			catch (UnauthorizedAccessException) {
				await HandleAccessDeniedAsync(path);
			}
			catch (IOException ex) {
				if (path.StartsWith(@"\\")) {
					await HandleAccessDeniedAsync(path);
				}
				else {
					StatusText = $"Error: {ex.Message}";
				}
			}
			catch (Exception ex) {
				StatusText = $"Error: {ex.Message}";
			}
			}
			finally {
				IsLoading = false;
			}
		}

		private async Task HandleAccessDeniedAsync(string path) {
			if (path.StartsWith(@"\\")) {
				string shareRoot = GetShareRoot(path);
				bool authenticated = false;

				var creds = CredentialStore.GetCredentials(shareRoot);
				if (creds != null) {
					try {
						NetworkHelper.ConnectToShare(shareRoot, creds.Value.Username, creds.Value.Password);
						authenticated = true;
					}
					catch {
						CredentialStore.RemoveCredentials(shareRoot);
					}
				}

				if (!authenticated) {
					await Application.Current.Dispatcher.InvokeAsync(() => {
						var dialog = new CredentialsWindow { Owner = Application.Current.MainWindow };
						if (dialog.ShowDialog() == true) {
							try {
								NetworkHelper.ConnectToShare(shareRoot, dialog.Username, dialog.Password);
								if (dialog.RememberMe) {
									CredentialStore.SaveCredentials(shareRoot, dialog.Username, dialog.Password);
								}
								authenticated = true;
							}
							catch (Exception ex) {
								MessageBox.Show($"Connection failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
							}
						}
					});
				}

				if (authenticated) {
					await LoadFilesAsync(path);
					return;
				}
			}
			StatusText = "Access Denied";
		}

		private static string GetShareRoot(string path) {
			if (!path.StartsWith(@"\\")) return path;
			var parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length >= 2) {
				return $@"\\{parts[0]}\{parts[1]}";
			}
			return path;
		}

		private static bool IsDownloadsFolder(string path) {
			return path.Equals(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads", StringComparison.OrdinalIgnoreCase);
		}

		private async Task PerformSearchAsync(string query) {
			_searchCts?.Cancel();
			_searchCts = new CancellationTokenSource();
			var token = _searchCts.Token;

			var searchSw = Stopwatch.StartNew();

			StatusText = "Searching...";
			IsSearching = true;
			Files.Clear();

			var resultsChannel = Channel.CreateUnbounded<FileItemViewModel>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
			var folders = new ConcurrentQueue<string>();

			if (_currentPath == "This PC") {
				foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady)) {
					folders.Enqueue(drive.RootDirectory.FullName);
				}
			}
			else {
				folders.Enqueue(_currentPath);
			}

			int activeWorkers = 0;
			int totalFound = 0;

			var consumerTask = Task.Run(async () => {
				var batch = new List<FileItemViewModel>();
				var timer = Stopwatch.StartNew();

				try {
					while (await resultsChannel.Reader.WaitToReadAsync(token)) {
						while (resultsChannel.Reader.TryRead(out var item)) {
							batch.Add(item);
							if (batch.Count >= 20 || timer.ElapsedMilliseconds > 40) {
								var update = batch.ToList();
								batch.Clear();
								timer.Restart();
								await Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => {
									if (token.IsCancellationRequested) return;
									foreach (var i in update) Files.Add(i);
									StatusText = $"Searching... Found {Files.Count} items";
									ItemCount = Files.Count;
								}));
							}
						}
					}
				}
				catch (OperationCanceledException) { }

				if (batch.Count > 0) {
					await Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => {
						if (token.IsCancellationRequested) return;
						foreach (var i in batch) Files.Add(i);
						StatusText = $"Found {Files.Count} items";
						ItemCount = Files.Count;
					}));
				}
			}, token);

			int maxConcurrency = Math.Max(4, Environment.ProcessorCount);
			var producers = Enumerable.Range(0, maxConcurrency).Select(_ => Task.Run(async () => {
				try {
					var opts = new EnumerationOptions {
						IgnoreInaccessible = true,
						ReturnSpecialDirectories = false,
						AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
					};

					while (!token.IsCancellationRequested) {
						if (folders.TryDequeue(out var dir)) {
							Interlocked.Increment(ref activeWorkers);
							try {
								var dirInfo = new DirectoryInfo(dir);
								foreach (var entry in dirInfo.EnumerateFileSystemInfos("*", opts)) {
									if (token.IsCancellationRequested) break;

									bool isDir = (entry.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
									if ((entry.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint) continue;

									if (isDir) {
										folders.Enqueue(entry.FullName);
									}

									if (entry.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;

									var nameSpan = entry.Name.AsSpan();
									if (nameSpan.Contains(query.AsSpan(), StringComparison.OrdinalIgnoreCase)) {
										if (AppSettings.Current.MaxSearchResults > 0 && totalFound >= AppSettings.Current.MaxSearchResults) {
											_searchCts.Cancel();
											break;
										}

										Interlocked.Increment(ref totalFound);

										FileItemViewModel? vm = null;
										if (isDir) vm = new FolderItemViewModel((DirectoryInfo)entry, false);
										else vm = new FileItemViewModel((FileInfo)entry, false);

										resultsChannel.Writer.TryWrite(vm);
									}
								}
							}
							catch { }
							finally {
								Interlocked.Decrement(ref activeWorkers);
							}
						}
						else {
							if (activeWorkers == 0 && folders.IsEmpty) break;
							await Task.Delay(10, token);
						}
					}
				}
				catch (OperationCanceledException) { }
			})).ToArray();

			try {
				await Task.WhenAll(producers);
				resultsChannel.Writer.Complete();
				await consumerTask;
			}
			catch (OperationCanceledException) { }
			finally {
				if (!token.IsCancellationRequested) {
					IsSearching = false;
					Application.Current?.Dispatcher.Invoke(ApplySort);
					searchSw.Stop();
					StatusText = $"Found {Files.Count} items ({searchSw.Elapsed.TotalSeconds:N2}s)";
					ItemCount = Files.Count;
				}
			}
		}

		private void FilterFiles() {
			Files = new ObservableCollection<FileItemViewModel>(_allFiles);
			ApplySort();
			ItemCount = Files.Count;
			StatusText = $"{ItemCount} items";
		}

		public void UpdateSelectionStatus(IList<FileItemViewModel> selectedItems) {
			_selectedItems = [.. selectedItems];
			if (selectedItems.Count == 0) {
				StatusText = $"{ItemCount} items";
			}
			else {
				long totalSize = selectedItems.Sum(x => x.Size);
				StatusText = $"{ItemCount} items   |   {selectedItems.Count} item{(selectedItems.Count > 1 ? "s" : "")} selected   {FileItemViewModel.FormatSize(totalSize)}";
			}
		}
	}
}
