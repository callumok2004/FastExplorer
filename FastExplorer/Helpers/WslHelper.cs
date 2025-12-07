using Microsoft.Win32;

namespace FastExplorer.Helpers {
	public static class WslHelper {
		public static List<string> GetDistributions() {
			var distros = new List<string>();
			try {
				using var lxssKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Lxss");
				if (lxssKey != null) {
					foreach (var subKeyName in lxssKey.GetSubKeyNames()) {
						using var subKey = lxssKey.OpenSubKey(subKeyName);
						var name = subKey?.GetValue("DistributionName") as string;
						if (!string.IsNullOrEmpty(name)) {
							distros.Add(name);
						}
					}
				}
			}
			catch { }
			return distros;
		}
	}
}
