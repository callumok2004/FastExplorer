using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using FastExplorer.Helpers;

namespace FastExplorer.ViewModels {
	public class FileItemViewModel : ViewModelBase {
		private static readonly Dictionary<string, string> _typeCache = new(StringComparer.OrdinalIgnoreCase);
		private ImageSource? _icon;
		private bool _iconLoaded;
		private ObservableCollection<ShellContextMenu.ShellMenuItem>? _shellMenuItems;
		private bool _isRenaming;
		private string _renameText = string.Empty;
		private bool _isDropTarget;

		public string Name { get; }
		public string FullPath { get; }
		public long Size { get; protected set; }
		public DateTime DateModified { get; }

		public bool IsRenaming {
			get => _isRenaming;
			set => SetProperty(ref _isRenaming, value);
		}

		public string RenameText {
			get => _renameText;
			set => SetProperty(ref _renameText, value);
		}

		public bool IsDropTarget {
			get => _isDropTarget;
			set => SetProperty(ref _isDropTarget, value);
		}

		public ObservableCollection<ShellContextMenu.ShellMenuItem> ShellMenuItems {
			get {
				if (_shellMenuItems == null) {
					_shellMenuItems = [];
				}
				return _shellMenuItems;
			}
		}

		public ImageSource? Icon {
			get {
				if (!_iconLoaded) {
					_icon = LoadIcon(32);
					_iconLoaded = true;
				}
				return _icon;
			}
		}

		public void RefreshIcon(int size) {
			_icon = LoadIcon(size);
			_iconLoaded = true;
			OnPropertyChanged(nameof(Icon));
		}

		public virtual bool IsFolder => false;
		public virtual bool IsDrive => false;
		public virtual double PercentUsed => 0;
		public virtual string FreeSpaceText => "";

		public virtual string DisplaySize => FormatSize(Size);
		public string DateModifiedString => DateModified.ToString("dd/MM/yyyy HH:mm");

		public DateTime DateGroupKey {
			get {
				var now = DateTime.Now;
				var date = DateModified.Date;
				var today = now.Date;
				var yesterday = today.AddDays(-1);

				if (date == today) return today;
				if (date == yesterday) return yesterday;

				var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
				if (date >= startOfWeek) return startOfWeek;

				var startOfLastWeek = startOfWeek.AddDays(-7);
				if (date >= startOfLastWeek) return startOfLastWeek;

				var startOfMonth = new DateTime(today.Year, today.Month, 1);
				if (date >= startOfMonth) return startOfMonth;

				var startOfLastMonth = startOfMonth.AddMonths(-1);
				if (date >= startOfLastMonth) return startOfLastMonth;

				var startOfYear = new DateTime(today.Year, 1, 1);
				if (date >= startOfYear) return startOfYear;

				return DateTime.MinValue;
			}
		}

		public virtual string Type {
			get {
				try {
					string ext = Path.GetExtension(FullPath);
					if (string.IsNullOrEmpty(ext)) return "File";

					string key = ext.ToLower();
					lock (_typeCache) {
						if (!_typeCache.TryGetValue(key, out var typeStr)) {
							typeStr = IconHelper.GetFileType(ext);
							if (string.IsNullOrEmpty(typeStr)) {
								typeStr = ext.TrimStart('.').ToUpper() + " File";
							}
							_typeCache[key] = typeStr;
						}
						return typeStr;
					}
				}
				catch { return "File"; }
			}
		}

		public override bool Equals(object? obj) {
			if (obj is FileItemViewModel other) {
				return FullPath.Equals(other.FullPath, StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}

		public override int GetHashCode() {
			return FullPath.GetHashCode(StringComparison.OrdinalIgnoreCase);
		}

		public FileItemViewModel(FileInfo info) {
			Name = info.Name;
			FullPath = info.FullName;
			Size = info.Length;
			DateModified = info.LastWriteTime;
		}

		protected FileItemViewModel(string name, string fullPath, DateTime dateModified) {
			Name = name;
			FullPath = fullPath;
			DateModified = dateModified;
			Size = 0;
		}

		public void LoadShellMenu() {
			if (_shellMenuItems != null && _shellMenuItems.Count > 0) return;

			try {
				var scm = new ShellContextMenu();
				var items = scm.GetContextMenuItems([new FileInfo(FullPath)]);
				foreach (var item in items) {
					ShellMenuItems.Add(item);
				}
			}
			catch { }
		}

		protected virtual ImageSource? LoadIcon(int size) {
			if (size > 20) {
				string ext = Path.GetExtension(FullPath).ToLower();
				string[] mediaExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webp"];
				if (mediaExtensions.Contains(ext)) {
					var thumb = IconHelper.GetThumbnail(FullPath, size);
					if (thumb != null) return thumb;
				}
			}
			return IconHelper.GetFileIcon(FullPath, size);
		}

		public static string FormatSize(long bytes) {
			string[] sizes = ["B", "KB", "MB", "GB", "TB"];
			double len = bytes;
			int order = 0;
			while (len >= 1024 && order < sizes.Length - 1) {
				order++;
				len /= 1024;
			}
			return $"{len:0.##} {sizes[order]}";
		}
	}

	public class FolderItemViewModel : FileItemViewModel {
		public override bool IsFolder => true;
		public override string Type => "File folder";
		public override string DisplaySize => "";

		public FolderItemViewModel(DirectoryInfo info)
			: base(info.Name, info.FullName, info.LastWriteTime) {
		}

		protected override ImageSource? LoadIcon(int size) {
			return IconHelper.GetFolderIcon(FullPath, false, size);
		}
	}

	public class DriveItemViewModel : FileItemViewModel {
		public override bool IsFolder => true;
		public override bool IsDrive => true;
		public override string Type => "Local Disk";

		public override double PercentUsed { get; }
		public override string FreeSpaceText { get; }

		public DriveItemViewModel(DriveInfo info)
			: base($"{info.VolumeLabel} ({info.Name})", info.RootDirectory.FullName, DateTime.Now) {

			if (info.IsReady) {
				long total = info.TotalSize;
				long free = info.TotalFreeSpace;
				Size = total;
				double used = total - free;
				PercentUsed = used / total * 100.0;
				FreeSpaceText = $"{FormatSize(free)} free of {FormatSize(total)}";
			}
			else {
				FreeSpaceText = "";
			}
		}

		protected override ImageSource? LoadIcon(int size) {
			return IconHelper.GetFolderIcon(FullPath, false, size);
		}
	}
}
