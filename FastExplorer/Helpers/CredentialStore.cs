using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FastExplorer.Helpers {
	public static class CredentialStore {
		private static readonly string _credentialsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FastExplorer", "credentials.dat");
		private static Dictionary<string, string> _credentials = [];

		static CredentialStore() {
			Load();
		}

		private static void Load() {
			try {
				if (File.Exists(_credentialsPath)) {
					var encryptedData = File.ReadAllBytes(_credentialsPath);
					var jsonBytes = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
					var json = Encoding.UTF8.GetString(jsonBytes);
					var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
					if (loaded != null)
						_credentials = loaded;
				}
			}
			catch { }
		}

		private static void Save() {
			try {
				var json = JsonSerializer.Serialize(_credentials);
				var jsonBytes = Encoding.UTF8.GetBytes(json);
				var encryptedData = ProtectedData.Protect(jsonBytes, null, DataProtectionScope.CurrentUser);

				var dir = Path.GetDirectoryName(_credentialsPath);
				if (dir != null && !Directory.Exists(dir)) {
					Directory.CreateDirectory(dir);
				}

				File.WriteAllBytes(_credentialsPath, encryptedData);
			}
			catch { }
		}

		public static void SaveCredentials(string target, string username, string password) {
			var data = $"{username}|{password}";
			_credentials[target.ToLowerInvariant()] = data;
			Save();
		}

		public static (string Username, string Password)? GetCredentials(string target) {
			if (_credentials.TryGetValue(target.ToLowerInvariant(), out var data)) {
				var parts = data.Split('|', 2);
				if (parts.Length == 2)
					return (parts[0], parts[1]);
			}
			return null;
		}

		public static void RemoveCredentials(string target) {
			if (_credentials.Remove(target.ToLowerInvariant()))
				Save();
		}
	}
}
