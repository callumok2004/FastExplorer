using System.Runtime.InteropServices;
using FastExplorer.Helpers;

namespace FastExplorer.ViewModels {
	public class RecycleBinItemViewModel : DirectoryItemViewModel {
		public RecycleBinItemViewModel() : base("shell:RecycleBinFolder", "Recycle Bin") {
			_ = Task.Run(UpdateSize);
		}

		private string? _sizeText;
		public string? SizeText {
			get => _sizeText;
			private set => SetProperty(ref _sizeText, value);
		}

		public void UpdateSize() {
			try {
				var rbInfo = new ShellHelper.SHQUERYRBINFO {
					cbSize = Marshal.SizeOf<ShellHelper.SHQUERYRBINFO>()
				};
				int hr = ShellHelper.SHQueryRecycleBin(null, ref rbInfo);
				if (hr == 0) {
					SizeText = FileItemViewModel.FormatSize(rbInfo.i64Size);
				}
				else {
					hr = ShellHelper.SHQueryRecycleBin(string.Empty, ref rbInfo);
					if (hr == 0) {
						SizeText = FileItemViewModel.FormatSize(rbInfo.i64Size);
					}
					else {
						SizeText = "Unknown";
					}
				}
			}
			catch {
				SizeText = "Unknown";
			}
		}
	}
}
