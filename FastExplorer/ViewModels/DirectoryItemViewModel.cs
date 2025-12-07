using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.HighPerformance.Buffers;
using FastExplorer.Helpers;

namespace FastExplorer.ViewModels {
	public class DirectoryItemViewModel : ViewModelBase {
		private bool _isExpanded;
		private bool _isSelected;
		private ObservableCollection<DirectoryItemViewModel> _subDirectories;
		private bool _hasDummyChild;
		private bool _isDropTarget;

		public string FullPath { get; }
		public string Name { get; }
		public ImageSource? Icon { get; }
		public bool IsDrive { get; }
		public bool IsNetworkShare { get; }
		public bool ShowCapacityBar => IsDrive || IsNetworkShare;
		public double PercentUsed {
			get => _percentUsed;
			set => SetProperty(ref _percentUsed, value);
		}
		private double _percentUsed;

		public bool IsDropTarget {
			get => _isDropTarget;
			set => SetProperty(ref _isDropTarget, value);
		}

		public DirectoryItemViewModel(string fullPath, string? name = null, string? label = null, bool isDrive = false, bool isNetworkShare = false) {
			FullPath = StringPool.Shared.GetOrAdd(fullPath);
			if (!string.IsNullOrEmpty(label)) {
				Name = StringPool.Shared.GetOrAdd(!string.IsNullOrEmpty(name) ? $"{label} ({name})" : label);
			}
			else {
				if (fullPath == "This PC") Name = "This PC";
				else {
					if (isNetworkShare) {
						var span = fullPath.AsSpan().TrimEnd('\\');
						var lastIndex = span.LastIndexOf('\\');
						var shareName = lastIndex >= 0 ? span[(lastIndex + 1)..] : span;
						Name = StringPool.Shared.GetOrAdd(shareName);
					}
					else {
						if (name != null) {
							Name = StringPool.Shared.GetOrAdd(name);
						} else {
							var span = fullPath.AsSpan().TrimEnd(Path.DirectorySeparatorChar);
							var lastIndex = span.LastIndexOf(Path.DirectorySeparatorChar);
							var dirName = lastIndex >= 0 ? span[(lastIndex + 1)..] : span;
							Name = StringPool.Shared.GetOrAdd(dirName);
						}
					}
				}
			}

			if (fullPath == "This PC") {
				Icon = IconHelper.GetFolderIcon("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", false, 24);
			}
			else {
				if (isNetworkShare) {
					Icon = IconHelper.GetFolderIcon("::{208D2C60-3AEA-1069-A2D7-08002B30309D}", false, 24);
				}
				else {
					Icon = IconHelper.GetFolderIcon(fullPath, false, 24);
				}
			}

			IsDrive = isDrive;
			IsNetworkShare = isNetworkShare;

			_subDirectories = [];

			if (fullPath != "This PC" && !fullPath.StartsWith("::") && !fullPath.StartsWith("shell:")) {
				if (isNetworkShare) {
					_subDirectories.Add(new DirectoryItemViewModel("dummy"));
					_hasDummyChild = true;
				}
				else if (HasSubDirectories(fullPath)) {
					_subDirectories.Add(new DirectoryItemViewModel("dummy"));
					_hasDummyChild = true;
				}
			}
		}

		public async Task LoadDriveDetailsAsync() {
			if (IsDrive || IsNetworkShare) {
				await Task.Run(() => {
					try {
						ulong total = 0;
						ulong free = 0;
						bool success = false;

						if (IsNetworkShare || FullPath.StartsWith(@"\\")) {
							if (ShellHelper.GetDiskFreeSpaceEx(FullPath, out ulong freeBytes, out ulong totalBytes, out _)) {
								total = totalBytes;
								free = freeBytes;
								success = true;
							}
						}
						else {
							var di = new DriveInfo(FullPath);
							if (di.IsReady) {
								total = (ulong)di.TotalSize;
								free = (ulong)di.TotalFreeSpace;
								success = true;
							}
						}

						if (success && total > 0) {
							var percent = (double)(total - free) / total * 100.0;
							Application.Current.Dispatcher.Invoke(() => {
								PercentUsed = percent;
							});
						}
					}
					catch { }
				});
			}
		}

		public ObservableCollection<DirectoryItemViewModel> SubDirectories {
			get => _subDirectories;
			set => SetProperty(ref _subDirectories, value);
		}

		public bool IsExpanded {
			get => _isExpanded;
			set {
				if (SetProperty(ref _isExpanded, value)) {
					if (value && _hasDummyChild) {
						SubDirectories.Clear();
						_hasDummyChild = false;
						_ = LoadSubDirectoriesAsync();
					}
				}
			}
		}

		public bool IsSelected {
			get => _isSelected;
			set => SetProperty(ref _isSelected, value);
		}

		private static bool HasSubDirectories(string path) {
			try {
				return Directory.EnumerateDirectories(path).Any();
			}
			catch {
				return false;
			}
		}

		private async Task LoadSubDirectoriesAsync() {
			try {
				var dirs = await Task.Run(() => {
					var list = new List<DirectoryItemViewModel>();
					try {
						var info = new DirectoryInfo(FullPath);
						foreach (var dir in info.EnumerateDirectories()) {
							if ((dir.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden) {
								list.Add(new DirectoryItemViewModel(dir.FullName));
							}
						}
					}
					catch { }
					return list;
				});

				SubDirectories = new ObservableCollection<DirectoryItemViewModel>(dirs);
			}
			catch { }
		}

		private List<ShellContextMenu.ShellMenuItem>? _shellMenuItems;
		public IEnumerable<ShellContextMenu.ShellMenuItem> ShellMenuItems => _shellMenuItems ?? Enumerable.Empty<ShellContextMenu.ShellMenuItem>();

		public void LoadShellMenu() {
			if (_shellMenuItems != null && _shellMenuItems.Count > 0) return;

			try {
				_shellMenuItems ??= [];
				var items = ShellContextMenu.GetContextMenuItems([FullPath]);
				foreach (var item in items) {
					_shellMenuItems.Add(item);
				}
			}
			catch { }
		}
	}
}
