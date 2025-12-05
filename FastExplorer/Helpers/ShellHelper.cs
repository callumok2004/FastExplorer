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

		[LibraryImport("shell32.dll")]
		private static partial int SHGetSpecialFolderLocation(IntPtr hwnd, int csidl, out IntPtr ppidl);

		[LibraryImport("shell32.dll")]
		private static partial int SHCreateItemFromIDList(IntPtr pidl, in Guid riid, out IShellItem ppv);

		[LibraryImport("shell32.dll")]
		private static partial int SHCreateShellItem(IntPtr pidlParent, IShellFolder psfParent, IntPtr pidl, out IShellItem ppsi);

		[LibraryImport("shell32.dll")]
		private static partial int SHBindToObject(IntPtr pidlParent, IntPtr pidl, IntPtr pbc, in Guid riid, out IShellFolder ppv);

		[GeneratedComInterface]
		[Guid("7e9fb0d3-919f-4307-ab2e-9b1860310c93")]
		internal partial interface IShellItem2 : IShellItem {
			[PreserveSig]
			int GetPropertyStore(int flags, in Guid riid, out IntPtr ppv);
			[PreserveSig]
			int GetPropertyStoreWithCreateObject(int flags, IntPtr punkCreateObject, in Guid riid, out IntPtr ppv);
			[PreserveSig]
			int GetPropertyStoreForKeys(IntPtr rgKeys, uint cKeys, int flags, in Guid riid, out IntPtr ppv);
			[PreserveSig]
			int GetPropertyDescriptionList(in PROPERTYKEY keyType, in Guid riid, out IntPtr ppv);
			[PreserveSig]
			int Update(IntPtr pbc);
			[PreserveSig]
			int GetProperty(in PROPERTYKEY key, out PROPVARIANT ppropvar);
			[PreserveSig]
			int GetCLSID(in PROPERTYKEY key, out Guid pclsid);
			[PreserveSig]
			int GetFileTime(in PROPERTYKEY key, out long pft);
			[PreserveSig]
			int GetInt32(in PROPERTYKEY key, out int pi);
			[PreserveSig]
			int GetString(in PROPERTYKEY key, out IntPtr ppsz);
			[PreserveSig]
			int GetUInt32(in PROPERTYKEY key, out uint pui);
			[PreserveSig]
			int GetUInt64(in PROPERTYKEY key, out ulong pull);
			[PreserveSig]
			int GetBool(in PROPERTYKEY key, out int pf);
		}

		[StructLayout(LayoutKind.Sequential)]
		internal struct PROPERTYKEY {
			public Guid fmtid;
			public uint pid;
		}

		[StructLayout(LayoutKind.Explicit)]
		internal struct PROPVARIANT {
			[FieldOffset(0)] public ushort vt;
			[FieldOffset(8)] public IntPtr ptr;
			[FieldOffset(8)] public int intVal;
			[FieldOffset(8)] public uint uintVal;
			[FieldOffset(8)] public byte bVal;
			[FieldOffset(8)] public short iVal;
			[FieldOffset(8)] public ushort uiVal;
			[FieldOffset(8)] public long lVal;
			[FieldOffset(8)] public ulong ulVal;
			[FieldOffset(8)] public float fltVal;
			[FieldOffset(8)] public double dblVal;
			[FieldOffset(8)] public short boolVal;
		}

		[GeneratedComInterface]
		[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
		internal partial interface IShellItem {
			[PreserveSig]
			int BindToHandler(IntPtr pbc, in Guid bhid, in Guid riid, out IEnumShellItems ppv);
			void GetParent(out IShellItem ppsi);
			[PreserveSig]
			int GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
			void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
			void Compare(IShellItem psi, uint hint, out int piOrder);
		}

		[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
		[Guid("000214E6-0000-0000-C000-000000000046")]
		internal partial interface IShellFolder {
			[PreserveSig]
			int ParseDisplayName(IntPtr hwnd, IntPtr pbc, string pszDisplayName, out uint pchEaten, out IntPtr ppidl, out uint pdwAttributes);
			[PreserveSig]
			int EnumObjects(IntPtr hwnd, uint grfFlags, out IEnumIDList ppenumIDList);
			[PreserveSig]
			int BindToObject(IntPtr pidl, IntPtr pbc, in Guid riid, out IntPtr ppv);
			[PreserveSig]
			int BindToStorage(IntPtr pidl, IntPtr pbc, in Guid riid, out IntPtr ppv);
			[PreserveSig]
			int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
			[PreserveSig]
			int CreateViewObject(IntPtr hwndOwner, in Guid riid, out IntPtr ppv);
			[PreserveSig]
			int GetAttributesOf(uint cidl, IntPtr apidl, ref uint rgfInOut);
			[PreserveSig]
			int GetUIObjectOf(IntPtr hwndOwner, uint cidl, IntPtr apidl, in Guid riid, ref uint rgfReserved, out IntPtr ppv);
			[PreserveSig]
			int GetDisplayNameOf(IntPtr pidl, uint uFlags, out IntPtr pName);
			[PreserveSig]
			int SetNameOf(IntPtr hwnd, IntPtr pidl, string pszName, uint uFlags, out IntPtr ppidlOut);
		}

		[GeneratedComInterface]
		[Guid("000214F2-0000-0000-C000-000000000046")]
		internal partial interface IEnumIDList {
			[PreserveSig]
			int Next(uint celt, out IntPtr rgelt, out uint pceltFetched);
			[PreserveSig]
			int Skip(uint celt);
			[PreserveSig]
			int Reset();
			[PreserveSig]
			int Clone(out IEnumIDList ppenum);
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

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct SHQUERYRBINFO {
			public int cbSize;
			public long i64Size;
			public long i64NumItems;
		}

		[LibraryImport("shell32.dll", EntryPoint = "SHQueryRecycleBinW", StringMarshalling = StringMarshalling.Utf16)]
		public static partial int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

		[LibraryImport("kernel32.dll", EntryPoint = "GetDiskFreeSpaceExW", StringMarshalling = StringMarshalling.Utf16)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static partial bool GetDiskFreeSpaceEx(
			string lpDirectoryName,
			out ulong lpFreeBytesAvailable,
			out ulong lpTotalNumberOfBytes,
			out ulong lpTotalNumberOfFreeBytes);

		public static List<(string Path, string Name, bool IsFolder, long Size, DateTime DateModified, string Type, string OriginalLocation)> EnumerateShellFolder(string parsingName) {
			var list = new List<(string Path, string Name, bool IsFolder, long Size, DateTime DateModified, string Type, string OriginalLocation)>();
			try {
				Guid shellItemGuid = typeof(IShellItem).GUID;
				int hr = -1;
				IShellItem? shellItem = null;
				bool isRecycleBin = parsingName.Equals("shell:RecycleBinFolder", StringComparison.OrdinalIgnoreCase) ||
									parsingName.Contains("645FF040-5081-101B-9F08-00AA002F954E", StringComparison.OrdinalIgnoreCase);

				if (isRecycleBin) {
					IntPtr pidlFolder = IntPtr.Zero;
					try {
						if (SHGetSpecialFolderLocation(IntPtr.Zero, 0x000a, out pidlFolder) == 0) {
							Guid shellFolderGuid = typeof(IShellFolder).GUID;
							if (SHBindToObject(IntPtr.Zero, pidlFolder, IntPtr.Zero, in shellFolderGuid, out IShellFolder shellFolder) == 0) {
								if (shellFolder.EnumObjects(IntPtr.Zero, 0x00010 | 0x00020 | 0x00040, out IEnumIDList enumIDList) == 0) {
									while (enumIDList.Next(1, out IntPtr pidlChild, out uint fetched) == 0 && fetched == 1) {
										try {
											if (SHCreateShellItem(IntPtr.Zero, shellFolder, pidlChild, out IShellItem item) == 0) {
												AddItemToList(list, item, isRecycleBin);
											}
										}
										finally {
											Marshal.FreeCoTaskMem(pidlChild);
										}
									}
								}
							}
						}
					}
					catch { }
					finally {
						if (pidlFolder != IntPtr.Zero) Marshal.FreeCoTaskMem(pidlFolder);
					}
					
					if (list.Count > 0) return list;
				}

				if (hr != 0) {
					hr = SHCreateItemFromParsingName(parsingName, IntPtr.Zero, in shellItemGuid, out shellItem);
				}

				if (hr != 0 && !parsingName.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) && !parsingName.StartsWith("::")) {
					hr = SHCreateItemFromParsingName("shell:" + parsingName, IntPtr.Zero, in shellItemGuid, out shellItem);
				}

				if (hr == 0 && shellItem != null) {
					Guid enumItemsGuid = typeof(IEnumShellItems).GUID;
					Guid bhidEnumItems = new("94f60519-2850-4924-aa5e-d05817189813");

					int bindHr = shellItem.BindToHandler(IntPtr.Zero, in bhidEnumItems, in enumItemsGuid, out IEnumShellItems enumItems);

					if (bindHr == 0 && enumItems != null) {
						while (enumItems.Next(1, out IShellItem item, out uint fetched) == 0 && fetched == 1) {
							AddItemToList(list, item, isRecycleBin);
						}
					}
				}
			}
			catch { }
			
			return list;
		}

		private static void AddItemToList(List<(string Path, string Name, bool IsFolder, long Size, DateTime DateModified, string Type, string OriginalLocation)> list, IShellItem item, bool isRecycleBin) {
			try {
				string? path = null;
				string? name = null;
				bool isFolder = false;
				long size = 0;
				DateTime date = DateTime.Now;
				string type = "File";
				string originalLocation = "";

				item.GetDisplayName(SIGDN.FILESYSPATH, out IntPtr ppszPath);
				if (ppszPath != IntPtr.Zero) {
					path = Marshal.PtrToStringUni(ppszPath);
					Marshal.FreeCoTaskMem(ppszPath);
				}

				if (string.IsNullOrEmpty(path)) {
					item.GetDisplayName(SIGDN.DESKTOPABSOLUTEPARSING, out IntPtr ppszParsing);
					if (ppszParsing != IntPtr.Zero) {
						path = Marshal.PtrToStringUni(ppszParsing);
						Marshal.FreeCoTaskMem(ppszParsing);
					}
				}

				item.GetDisplayName(SIGDN.NORMALDISPLAY, out IntPtr ppszName);
				if (ppszName != IntPtr.Zero) {
					name = Marshal.PtrToStringUni(ppszName);
					Marshal.FreeCoTaskMem(ppszName);
					if (!string.IsNullOrEmpty(name)) {
						name = Path.GetFileName(name);
					}
				}

				item.GetAttributes(0xFFFFFFFF, out uint attribs);
				if ((attribs & 0x20000000) != 0) {
					isFolder = true;
					type = "File folder";
				}

				if (item is IShellItem2 item2) {
					PROPERTYKEY pkeySize = new() { fmtid = new Guid("b725f130-47ef-101a-a5f1-02608c9eebac"), pid = 12 };
					if (item2.GetUInt64(in pkeySize, out ulong sizeVal) == 0) {
						size = (long)sizeVal;
					}

					PROPERTYKEY pkeyDate = new() { fmtid = new Guid("b725f130-47ef-101a-a5f1-02608c9eebac"), pid = 14 };
					if (item2.GetFileTime(in pkeyDate, out long dateVal) == 0) {
						date = DateTime.FromFileTime(dateVal);
					}
					
					PROPERTYKEY pkeyType = new() { fmtid = new Guid("B725F130-47EF-101A-A5F1-02608C9EEBAC"), pid = 4 };
					if (item2.GetString(in pkeyType, out IntPtr ppszType) == 0 && ppszType != IntPtr.Zero) {
						type = Marshal.PtrToStringUni(ppszType) ?? type;
						Marshal.FreeCoTaskMem(ppszType);
					}

					if (isRecycleBin) {
						PROPERTYKEY pkeyDeleted = new() { fmtid = new Guid("302359C2-936B-41B7-B234-7BB1243465F9"), pid = 2 };
						if (item2.GetFileTime(in pkeyDeleted, out long deletedVal) == 0) {
							date = DateTime.FromFileTime(deletedVal);
						}

						PROPERTYKEY pkeyOriginal = new() { fmtid = new Guid("9B174B33-40FF-11D2-A27E-00C04FC30871"), pid = 2 };
						if (item2.GetString(in pkeyOriginal, out IntPtr ppszOriginal) == 0 && ppszOriginal != IntPtr.Zero) {
							originalLocation = Marshal.PtrToStringUni(ppszOriginal) ?? "";
							Marshal.FreeCoTaskMem(ppszOriginal);
						}
					}
				}

				if (!string.IsNullOrEmpty(path)) {
					list.Add((path, name ?? "Unknown", isFolder, size, date, type, originalLocation));
				}
			}
			catch { }
		}
	}
}
