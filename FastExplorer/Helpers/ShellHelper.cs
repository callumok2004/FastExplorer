using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using CommunityToolkit.HighPerformance.Buffers;

namespace FastExplorer.Helpers {
	public static partial class ShellHelper {
		public static List<(string Path, string Name)> GetQuickAccessFolders() {
			var list = new List<(string Path, string Name)>();
			try {
				Guid shellItemGuid = typeof(IShellItem).GUID;
				int hr = SHCreateItemFromParsingName("shell:::{52528A6B-B9E3-4ADD-B60D-588C2DBA842D}", IntPtr.Zero, in shellItemGuid, out IShellItem shellItem);

				if (hr != 0) {
					hr = SHCreateItemFromParsingName("shell:::{679f85cb-0220-4080-b29b-5540cc05aab6}", IntPtr.Zero, in shellItemGuid, out shellItem);
				}

				if (hr == 0 && shellItem != null) {
					Guid enumItemsGuid = typeof(IEnumShellItems).GUID;
					Guid bhidEnumItems = new("94f60519-2850-4924-aa5e-d05817189813");

					shellItem.BindToHandler(IntPtr.Zero, in bhidEnumItems, in enumItemsGuid, out IEnumShellItems enumItems);

					if (enumItems != null) {
						while (enumItems.Next(1, out IShellItem item, out uint fetched) == 0 && fetched == 1) {
							try {
								string? path = null;
								string? name = null;

								item.GetDisplayName(SIGDN.FILESYSPATH, out IntPtr ppszPath);
								if (ppszPath != IntPtr.Zero) {
									path = Marshal.PtrToStringUni(ppszPath);
									Marshal.FreeCoTaskMem(ppszPath);
								}

								item.GetDisplayName(SIGDN.NORMALDISPLAY, out IntPtr ppszName);
								if (ppszName != IntPtr.Zero) {
									name = Marshal.PtrToStringUni(ppszName);
									Marshal.FreeCoTaskMem(ppszName);
								}

								if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) {
									string pooledPath = StringPool.Shared.GetOrAdd(path);
									string pooledName = StringPool.Shared.GetOrAdd(name ?? new DirectoryInfo(path).Name);
									list.Add((pooledPath, pooledName));
								}
							}
							catch { }
						}
					}
				}
			}
			catch { }
			return list;
		}

		[LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
		private static partial int SHCreateItemFromParsingName(
			string pszPath,
			IntPtr pbc,
			in Guid riid,
			out IShellItem ppv);

		[GeneratedComInterface]
		[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
		internal partial interface IShellItem {
			void BindToHandler(IntPtr pbc, in Guid bhid, in Guid riid, out IEnumShellItems ppv);
			void GetParent(out IShellItem ppsi);
			void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
			void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
			void Compare(IShellItem psi, uint hint, out int piOrder);
		}

		[GeneratedComInterface]
		[Guid("70629033-e363-4a28-a567-0db7800694d7")]
		internal partial interface IEnumShellItems {
			[PreserveSig]
			int Next(uint celt, out IShellItem rgelt, out uint pceltFetched);
			void Skip(uint celt);
			void Reset();
			void Clone(out IEnumShellItems ppenum);
		}

		internal enum SIGDN : uint {
			NORMALDISPLAY = 0x00000000,
			PARENTRELATIVEPARSING = 0x80018001,
			DESKTOPABSOLUTEPARSING = 0x80028000,
			PARENTRELATIVEEDITING = 0x80031001,
			DESKTOPABSOLUTEEDITING = 0x8004c000,
			FILESYSPATH = 0x80058000,
			URL = 0x80068000,
			PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
			PARENTRELATIVE = 0x80080001
		}

		public static bool ShowFileProperties(string filename) {
			var info = new SHELLEXECUTEINFO();
			info.cbSize = Marshal.SizeOf<SHELLEXECUTEINFO>();
			info.lpVerb = Marshal.StringToCoTaskMemUni("properties");
			info.lpFile = Marshal.StringToCoTaskMemUni(filename);
			info.nShow = SW_SHOW;
			info.fMask = SEE_MASK_INVOKEIDLIST;

			try {
				return ShellExecuteEx(ref info);
			}
			finally {
				Marshal.FreeCoTaskMem(info.lpVerb);
				Marshal.FreeCoTaskMem(info.lpFile);
			}
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct SHELLEXECUTEINFO {
			public int cbSize;
			public uint fMask;
			public IntPtr hwnd;
			public IntPtr lpVerb;
			public IntPtr lpFile;
			public IntPtr lpParameters;
			public IntPtr lpDirectory;
			public int nShow;
			public IntPtr hInstApp;
			public IntPtr lpIDList;
			public IntPtr lpClass;
			public IntPtr hkeyClass;
			public uint dwHotKey;
			public IntPtr hIcon;
			public IntPtr hProcess;
		}

		private const int SW_SHOW = 5;
		private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;

		[LibraryImport("shell32.dll", EntryPoint = "ShellExecuteExW")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static partial bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

		private const uint FO_MOVE = 0x0001;
		private const uint FO_COPY = 0x0002;
		private const uint FO_DELETE = 0x0003;
		private const uint FO_RENAME = 0x0004;
		private const ushort FOF_ALLOWUNDO = 0x0040;
		private const ushort FOF_NOCONFIRMATION = 0x0010;

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct SHFILEOPSTRUCT {
			public IntPtr hwnd;
			public uint wFunc;
			public IntPtr pFrom;
			public IntPtr pTo;
			public ushort fFlags;
			public int fAnyOperationsAborted;
			public IntPtr hNameMappings;
			public IntPtr lpszProgressTitle;
		}

		[LibraryImport("shell32.dll", EntryPoint = "SHFileOperationW")]
		private static partial int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

		public static void DeleteToRecycleBin(string path) {
			var shf = new SHFILEOPSTRUCT {
				wFunc = FO_DELETE,
				fFlags = FOF_ALLOWUNDO,
				pFrom = Marshal.StringToCoTaskMemUni(path + "\0\0")
			};
			try {
				SHFileOperation(ref shf);
			}
			finally {
				Marshal.FreeCoTaskMem(shf.pFrom);
			}
		}

		public static void CopyFile(string source, string dest) {
			var shf = new SHFILEOPSTRUCT {
				wFunc = FO_COPY,
				fFlags = FOF_ALLOWUNDO,
				pFrom = Marshal.StringToCoTaskMemUni(source + "\0\0"),
				pTo = Marshal.StringToCoTaskMemUni(dest + "\0\0")
			};
			try {
				SHFileOperation(ref shf);
			}
			finally {
				Marshal.FreeCoTaskMem(shf.pFrom);
				Marshal.FreeCoTaskMem(shf.pTo);
			}
		}

		public static void MoveFile(string source, string dest) {
			var shf = new SHFILEOPSTRUCT {
				wFunc = FO_MOVE,
				fFlags = FOF_ALLOWUNDO,
				pFrom = Marshal.StringToCoTaskMemUni(source + "\0\0"),
				pTo = Marshal.StringToCoTaskMemUni(dest + "\0\0")
			};
			try {
				SHFileOperation(ref shf);
			}
			finally {
				Marshal.FreeCoTaskMem(shf.pFrom);
				Marshal.FreeCoTaskMem(shf.pTo);
			}
		}
	}
}
