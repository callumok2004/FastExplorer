using System.Runtime.InteropServices;
using System.ComponentModel;

namespace FastExplorer.Helpers {
	public static partial class NetworkHelper {
		[LibraryImport("mpr.dll", StringMarshalling = StringMarshalling.Utf16)]
		private static partial int WNetAddConnection2(IntPtr netResource, string password, string username, int flags);

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
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

			IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(resource));
			try {
				Marshal.StructureToPtr(resource, ptr, false);
				int result = WNetAddConnection2(ptr, password, username, 0);

				if (result != 0) {
					throw new Win32Exception(result);
				}
			}
			finally {
				Marshal.FreeHGlobal(ptr);
			}
		}
	}
}
