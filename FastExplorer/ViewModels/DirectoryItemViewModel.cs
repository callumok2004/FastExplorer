using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
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
		public double PercentUsed { get; }

		public bool IsDropTarget {
			get => _isDropTarget;
			set => SetProperty(ref _isDropTarget, value);
		}

		public DirectoryItemViewModel(string fullPath, string? name = null, string? label = null, bool isDrive = false) {
			FullPath = fullPath;
			if (!string.IsNullOrEmpty(label)) {
				Name = !string.IsNullOrEmpty(name) ? $"{label} ({name})" : label;
			}
			else {
				if (fullPath == "This PC") Name = "This PC";
				else Name = name ?? new DirectoryInfo(fullPath).Name;
			}

			if (fullPath == "This PC") {
				Icon = IconHelper.GetFolderIcon(Environment.GetFolderPath(Environment.SpecialFolder.MyComputer), false, 16);
			}
			else {
				Icon = IconHelper.GetFolderIcon(fullPath, false, 16);
			}

			IsDrive = isDrive;

			if (IsDrive) {
				try {
					var di = new DriveInfo(fullPath);
					if (di.IsReady) {
						long total = di.TotalSize;
						long free = di.TotalFreeSpace;
						PercentUsed = (double)(total - free) / total * 100.0;
					}
				}
				catch { }
			}

			_subDirectories = [];

			if (fullPath != "This PC" && HasSubDirectories(fullPath)) {
				_subDirectories.Add(new DirectoryItemViewModel("dummy"));
				_hasDummyChild = true;
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
					var info = new DirectoryInfo(FullPath);
					foreach (var dir in info.GetDirectories()) {
						if ((dir.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden) {
							list.Add(new DirectoryItemViewModel(dir.FullName));
						}
					}
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
