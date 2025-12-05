using System.Runtime.InteropServices;
using System.ComponentModel;

namespace FastExplorer.Helpers {
	public static class NetworkHelper {
		[DllImport("mpr.dll")]
		private static extern int WNetAddConnection2(NetResource netResource, string password, string username, int flags);

		[DllImport("mpr.dll")]
		private static extern int WNetCancelConnection2(string name, int flags, bool force);

		[StructLayout(LayoutKind.Sequential)]
		public class NetResource {
			public int Scope;
			public int Type;
			public int DisplayType;
			public int Usage;
			public string? LocalName;
			public string? RemoteName;
			public string? Comment;
			public string? Provider;
		}

		public static void ConnectToShare(string remoteName, string username, string password) {
			var resource = new NetResource {
				Scope = 2,
				Type = 1,
				DisplayType = 3,
				Usage = 1,
				RemoteName = remoteName
			};

			int result = WNetAddConnection2(resource, password, username, 0);

			if (result != 0) {
				throw new Win32Exception(result);
			}
		}
	}
}