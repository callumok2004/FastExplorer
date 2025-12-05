using System.IO;
using System.Windows.Media;

using CommunityToolkit.HighPerformance.Buffers;

using FastExplorer.Helpers;

namespace FastExplorer.ViewModels {
	public class FileItemViewModel : ViewModelBase {
		private static readonly Dictionary<string, string> _typeCache = new(StringComparer.OrdinalIgnoreCase);
		private static readonly string[] _sizeSuffixes = ["B", "KB", "MB", "GB", "TB"];

		private readonly string _directory;
		private readonly string _fileName;
		private readonly string? _fullPathOverride;

		private byte _desiredIconSize = 32;
		private bool _iconLoaded;
		private bool _isRenaming;
		private bool _isDropTarget;

		private ImageSource? _icon;
		private List<ShellContextMenu.ShellMenuItem>? _shellMenuItems;
		private string _renameText = string.Empty;

		public string Name => _fileName;
		public string FullPath => _fullPathOverride ?? Path.Combine(_directory, _fileName);
		public long Size { get; protected set; }
		public DateTime DateModified { get; }
		public virtual string OriginalLocation => "";
		public virtual string Category => "Files";
		public virtual bool IsNetworkShare => false;

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

		public IEnumerable<ShellContextMenu.ShellMenuItem> ShellMenuItems => _shellMenuItems ?? Enumerable.Empty<ShellContextMenu.ShellMenuItem>();

		public ImageSource? Icon {
			get {
				if (!_iconLoaded) {
					_icon = LoadIcon(_desiredIconSize);
					_iconLoaded = true;
				}
				return _icon;
			}
		}

		public void RefreshIcon(int size) {
			if (_desiredIconSize != size || _iconLoaded) {
				_desiredIconSize = (byte)Math.Min(size, 255);
				_icon = null;
				_iconLoaded = false;
				OnPropertyChanged(nameof(Icon));
			}
		}

		public virtual bool IsFolder => false;
		public virtual bool IsDrive => false;
		public virtual double PercentUsed => 0;
		public virtual string FreeSpaceText => "";

		public virtual string DisplaySize => FormatSize(Size);
		public string DateModifiedString => DateModified.ToString("dd/MM/yyyy HH:mm");

		public DateTime DateGroupKey {
			get {
				var today = DateTime.Today;
				var date = DateModified.Date;
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
					string ext = _fullPathOverride != null ? Path.GetExtension(_fullPathOverride) : Path.GetExtension(_fileName);
					if (string.IsNullOrEmpty(ext)) return "File";

					string key;
					if (ext.Length <= 64) {
						Span<char> buffer = stackalloc char[ext.Length];
						ext.AsSpan().ToLower(buffer, System.Globalization.CultureInfo.CurrentCulture);
						key = StringPool.Shared.GetOrAdd(buffer);
					}
					else {
						key = StringPool.Shared.GetOrAdd(ext.ToLower());
					}

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
				if (_fullPathOverride != null) return _fullPathOverride.Equals(other.FullPath, StringComparison.OrdinalIgnoreCase);
				return _fileName.Equals(other._fileName, StringComparison.OrdinalIgnoreCase) &&
							 _directory.Equals(other._directory, StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}

		public override int GetHashCode() {
			if (_fullPathOverride != null) return _fullPathOverride.GetHashCode(StringComparison.OrdinalIgnoreCase);
			return HashCode.Combine(
				_directory.GetHashCode(StringComparison.OrdinalIgnoreCase),
				_fileName.GetHashCode(StringComparison.OrdinalIgnoreCase));
		}

		public FileItemViewModel(FileInfo info, bool poolName = true) {
			if (poolName)
				_fileName = StringPool.Shared.GetOrAdd(info.Name);
			else
				_fileName = info.Name;

			var dirSpan = Path.GetDirectoryName(info.FullName.AsSpan());
			_directory = StringPool.Shared.GetOrAdd(dirSpan);
			_fullPathOverride = null;

			Size = info.Length;
			DateModified = info.LastWriteTime;
		}

		protected FileItemViewModel(string name, string fullPath, DateTime dateModified, bool poolName = true) {
			if (poolName)
				_fileName = StringPool.Shared.GetOrAdd(name);
			else
				_fileName = name;

			_fullPathOverride = fullPath;
			_directory = string.Empty;

			DateModified = dateModified;
			Size = 0;
		}

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
			double len = bytes;
			int order = 0;
			while (len >= 1024 && order < _sizeSuffixes.Length - 1) {
				order++;
				len /= 1024;
			}
			return $"{len:0.##} {_sizeSuffixes[order]}";
		}
	}

	public class FolderItemViewModel : FileItemViewModel {
		public override bool IsFolder => true;
		public override string Type => "File folder";
		public override string DisplaySize => "";

		public FolderItemViewModel(DirectoryInfo info, bool poolName = true)
			: base(info.Name, info.FullName, GetDateModifiedSafe(info), poolName) {
		}

		private static DateTime GetDateModifiedSafe(DirectoryInfo info) {
			try {
				return info.LastWriteTime;
			}
			catch {
				return DateTime.MinValue;
			}
		}

		protected override ImageSource? LoadIcon(int size) {
			return IconHelper.GetFolderIcon(FullPath, false, size);
		}
	}

	public class DriveItemViewModel : FileItemViewModel {
		public override bool IsFolder => true;
		public override bool IsDrive => true;
		public override string Type => "Local Disk";
		public override string Category => "Devices and drives";

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

	public class ShellFileItemViewModel : FileItemViewModel {
		private readonly bool _isFolder;
		private readonly string _type;
		private readonly string _originalLocation;

		public override bool IsFolder => _isFolder;
		public override string Type => !string.IsNullOrEmpty(_type) ? _type : (_isFolder ? "File folder" : base.Type);
		public override string DisplaySize => (_isFolder && Size == 0) ? "" : base.DisplaySize;
		public override string OriginalLocation => _originalLocation;

		public ShellFileItemViewModel(string name, string fullPath, bool isFolder, long size, DateTime dateModified, string type = "", string originalLocation = "") 
			: base(name, fullPath, dateModified) {
			_isFolder = isFolder;
			Size = size;
			_type = type;
			_originalLocation = originalLocation;
		}

		protected override ImageSource? LoadIcon(int size) {
			if (_isFolder) {
				return IconHelper.GetFolderIcon(FullPath, false, size);
			}
			if (FullPath.StartsWith("::") || FullPath.StartsWith("shell:")) {
				return IconHelper.GetThumbnail(FullPath, size);
			}
			return base.LoadIcon(size);
		}
	}

	public class NetworkShareItemViewModel : FolderItemViewModel {
		public override string Type => "Network Connection";
		public override string Category => "Network locations";
		public override bool IsNetworkShare => true;

		public override double PercentUsed { get; }
		public override string FreeSpaceText { get; }

		public NetworkShareItemViewModel(string path) : base(new DirectoryInfo(path), false) {
			FreeSpaceText = "";
			try {
				if (ShellHelper.GetDiskFreeSpaceEx(path, out ulong freeBytesAvailable, out ulong totalNumberOfBytes, out _)) {
					Size = (long)totalNumberOfBytes;
					long free = (long)freeBytesAvailable;
					double used = Size - free;
					if (Size > 0) {
						PercentUsed = used / Size * 100.0;
						FreeSpaceText = $"{FormatSize(free)} free of {FormatSize(Size)}";
					}
				}
			}
			catch { }
		}

		protected override ImageSource? LoadIcon(int size) {
			return IconHelper.GetFolderIcon(FullPath, false, size);
		}
	}
}
