using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance.Buffers;


#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
#pragma warning disable SYSLIB1096 // Convert to 'GeneratedComInterface'
namespace FastExplorer.Helpers {
	public static class ShellHelper {
		public static List<(string Path, string Name)> GetQuickAccessFolders() {
			var list = new List<(string Path, string Name)>();
			try {
				Guid shellItemGuid = typeof(IShellItem).GUID;
				int hr = SHCreateItemFromParsingName("shell:::{52528A6B-B9E3-4ADD-B60D-588C2DBA842D}", IntPtr.Zero, shellItemGuid, out IShellItem shellItem);

				if (hr != 0) {
					hr = SHCreateItemFromParsingName("shell:::{679f85cb-0220-4080-b29b-5540cc05aab6}", IntPtr.Zero, shellItemGuid, out shellItem);
				}

				if (hr == 0 && shellItem != null) {
					Guid enumItemsGuid = typeof(IEnumShellItems).GUID;
					Guid bhidEnumItems = new("94f60519-2850-4924-aa5e-d05817189813");

					shellItem.BindToHandler(IntPtr.Zero, bhidEnumItems, enumItemsGuid, out IntPtr ppv);

					if (ppv != IntPtr.Zero) {
						var enumItems = (IEnumShellItems)Marshal.GetObjectForIUnknown(ppv);
						IShellItem[] buffer = new IShellItem[1];

						while (enumItems.Next(1, buffer, out uint fetched) == 0 && fetched == 1) {
							try {
								string? path = null;
								string? name = null;

								buffer[0].GetDisplayName(SIGDN.FILESYSPATH, out IntPtr ppszPath);
								if (ppszPath != IntPtr.Zero) {
									path = Marshal.PtrToStringUni(ppszPath);
									Marshal.FreeCoTaskMem(ppszPath);
								}

								buffer[0].GetDisplayName(SIGDN.NORMALDISPLAY, out IntPtr ppszName);
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

		[DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
		private static extern int SHCreateItemFromParsingName(
			[MarshalAs(UnmanagedType.LPWStr)] string pszPath,
			IntPtr pbc,
			[MarshalAs(UnmanagedType.LPStruct)] Guid riid,
			out IShellItem ppv);

		[ComImport]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
		private interface IShellItem {
			void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
			void GetParent(out IShellItem ppsi);
			void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
			void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
			void Compare(IShellItem psi, uint hint, out int piOrder);
		}

		[ComImport]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		[Guid("70629033-e363-4a28-a567-0db7800694d7")]
		private interface IEnumShellItems {
			[PreserveSig]
			int Next(uint celt, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Interface)] IShellItem[] rgelt, out uint pceltFetched);
			void Skip(uint celt);
			void Reset();
			void Clone(out IEnumShellItems ppenum);
		}

		private enum SIGDN : uint {
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
			SHELLEXECUTEINFO info = new();
			info.cbSize = Marshal.SizeOf(info);
			info.lpVerb = "properties";
			info.lpFile = filename;
			info.nShow = SW_SHOW;
			info.fMask = SEE_MASK_INVOKEIDLIST;
			return ShellExecuteEx(ref info);
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		public struct SHELLEXECUTEINFO {
			public int cbSize;
			public uint fMask;
			public IntPtr hwnd;
			[MarshalAs(UnmanagedType.LPTStr)]
			public string lpVerb;
			[MarshalAs(UnmanagedType.LPTStr)]
			public string lpFile;
			[MarshalAs(UnmanagedType.LPTStr)]
			public string lpParameters;
			[MarshalAs(UnmanagedType.LPTStr)]
			public string lpDirectory;
			public int nShow;
			public IntPtr hInstApp;
			public IntPtr lpIDList;
			[MarshalAs(UnmanagedType.LPTStr)]
			public string lpClass;
			public IntPtr hkeyClass;
			public uint dwHotKey;
			public IntPtr hIcon;
			public IntPtr hProcess;
		}

		private const int SW_SHOW = 5;
		private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;

		[DllImport("shell32.dll", CharSet = CharSet.Auto)]
		static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

		private const uint FO_MOVE = 0x0001;
		private const uint FO_COPY = 0x0002;
		private const uint FO_DELETE = 0x0003;
		private const uint FO_RENAME = 0x0004;
		private const ushort FOF_ALLOWUNDO = 0x0040;
		private const ushort FOF_NOCONFIRMATION = 0x0010;

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		public struct SHFILEOPSTRUCT {
			public IntPtr hwnd;
			public uint wFunc;
			[MarshalAs(UnmanagedType.LPTStr)]
			public string pFrom;
			[MarshalAs(UnmanagedType.LPTStr)]
			public string pTo;
			public ushort fFlags;
			public bool fAnyOperationsAborted;
			public IntPtr hNameMappings;
			[MarshalAs(UnmanagedType.LPTStr)]
			public string lpszProgressTitle;
		}

		[DllImport("shell32.dll", CharSet = CharSet.Auto)]
		static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

		public static void DeleteToRecycleBin(string path) {
			SHFILEOPSTRUCT shf = new() {
				wFunc = FO_DELETE,
				fFlags = FOF_ALLOWUNDO,
				pFrom = path + "\0\0"
			};
			_ = SHFileOperation(ref shf);
		}

		public static void CopyFile(string source, string dest) {
			SHFILEOPSTRUCT shf = new() {
				wFunc = FO_COPY,
				fFlags = FOF_ALLOWUNDO,
				pFrom = source + "\0\0",
				pTo = dest + "\0\0"
			};
			_ = SHFileOperation(ref shf);
		}

		public static void MoveFile(string source, string dest) {
			SHFILEOPSTRUCT shf = new() {
				wFunc = FO_MOVE,
				fFlags = FOF_ALLOWUNDO,
				pFrom = source + "\0\0",
				pTo = dest + "\0\0"
			};
			_ = SHFileOperation(ref shf);
		}
	}
}
#pragma warning restore SYSLIB1096 // Convert to 'GeneratedComInterface'
#pragma warning restore SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
