using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace FastExplorer {
	public class FolderViewState {
		public double ItemSize { get; set; }
		public string SortProperty { get; set; } = "Name";
		public string SortDirection { get; set; } = "Ascending";
	}

	public class AppSettings : INotifyPropertyChanged {
		public static AppSettings Current { get; private set; } = new AppSettings();
		private static readonly string _settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FastExplorer");
		private static readonly string _settingsPath = Path.Combine(_settingsFolder, "settings.json");

		private int _maxSearchResults = int.MaxValue;
		public int MaxSearchResults {
			get => _maxSearchResults;
			set => SetProperty(ref _maxSearchResults, value);
		}

		private double _itemSize = 16;
		public double ItemSize {
			get => _itemSize;
			set => SetProperty(ref _itemSize, value);
		}

		private string _sortProperty = "Name";
		public string SortProperty {
			get => _sortProperty;
			set => SetProperty(ref _sortProperty, value);
		}

		private string _sortDirection = "Ascending";
		public string SortDirection {
			get => _sortDirection;
			set => SetProperty(ref _sortDirection, value);
		}

		public Dictionary<string, FolderViewState> FolderViewStates { get; set; } = [];
		public List<string> PinnedFolders { get; set; } = [];

		public event PropertyChangedEventHandler? PropertyChanged;
		protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
			if (EqualityComparer<T>.Default.Equals(field, value)) return false;
			field = value;
			OnPropertyChanged(propertyName);
			return true;
		}

		public static void Load() {
			try {
				if (File.Exists(_settingsPath)) {
					var json = File.ReadAllText(_settingsPath);
					var loaded = JsonSerializer.Deserialize<AppSettings>(json);
					if (loaded != null) {
						Current = loaded;
					}
				}
			}
			catch { }
		}

		public static void Save() {
			try {
				if (!Directory.Exists(_settingsFolder)) {
					_ = Directory.CreateDirectory(_settingsFolder);
				}
				var json = JsonSerializer.Serialize(Current);
				File.WriteAllText(_settingsPath, json);
			}
			catch { }
		}
	}
}
