using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
#pragma warning disable SYSLIB1096 // Convert to 'GeneratedComInterface'
namespace FastExplorer.Helpers {
	public static class IconHelper {
		private static readonly Dictionary<string, ImageSource> _iconCache = [];
		private static readonly Lock _cacheLock = new();
		private const int MaxCacheSize = 500;

		private static readonly HashSet<string> _specialPaths = new(StringComparer.OrdinalIgnoreCase) {
			Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
			Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
			Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
			Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
			Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
			Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads"
		};

		public static string GetFileType(string extension) {
			SHFILEINFO shinfo = new();
			uint flags = SHGFI_TYPENAME | SHGFI_USEFILEATTRIBUTES;
			if (SHGetFileInfo(extension, FILE_ATTRIBUTE_NORMAL, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags) != IntPtr.Zero) {
				return shinfo.szTypeName;
			}
			return string.Empty;
		}

		public static ImageSource? GetFileIcon(string path) {
			string ext = Path.GetExtension(path).ToLower();
			if (string.IsNullOrEmpty(ext)) ext = ".file";

			if (ext == ".exe" || ext == ".lnk" || ext == ".ico") {
				return GetIcon(path, false, false);
			}

			lock (_cacheLock) {
				if (_iconCache.TryGetValue(ext, out var cachedIcon)) return cachedIcon;
			}

			var icon = GetIcon(ext, false, true);

			if (icon != null) {
				lock (_cacheLock) {
					if (_iconCache.Count >= MaxCacheSize) _iconCache.Clear();
					if (!_iconCache.ContainsKey(ext)) _iconCache[ext] = icon;
				}
			}
			return icon;
		}

		public static ImageSource? GetFolderIcon(string path, bool open) {
			if (path.Length <= 3 || _specialPaths.Contains(path)) {
				return GetIcon(path, true, false);
			}

			string key = open ? "folder_open" : "folder_closed";

			lock (_cacheLock) {
				if (_iconCache.TryGetValue(key, out var cachedIcon)) return cachedIcon;
			}

			var icon = GetIcon(path, true, true);

			if (icon != null) {
				lock (_cacheLock) {
					if (_iconCache.Count >= MaxCacheSize) _iconCache.Clear();
					if (!_iconCache.ContainsKey(key)) _iconCache[key] = icon;
				}
			}
			return icon;
		}

		private static BitmapSource? GetIcon(string path, bool isFolder,  bool useFileAttributes) {
			var flags = SHGFI_ICON | SHGFI_SMALLICON;
			if (useFileAttributes) flags |= SHGFI_USEFILEATTRIBUTES;

			var attributes = isFolder ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;

			SHFILEINFO shinfo = new();
			IntPtr hImg = SHGetFileInfo(path, attributes, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);

			if (hImg == IntPtr.Zero) return null;

			var icon = Imaging.CreateBitmapSourceFromHIcon(
				shinfo.hIcon,
				Int32Rect.Empty,
				BitmapSizeOptions.FromEmptyOptions());

			_ = DestroyIcon(shinfo.hIcon);
			icon.Freeze();
			return icon;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct SHFILEINFO {
			public IntPtr hIcon;
			public int iIcon;
			public uint dwAttributes;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
			public string szDisplayName;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
			public string szTypeName;
		};

		private const uint SHGFI_ICON = 0x100;
		private const uint SHGFI_LARGEICON = 0x0;
		private const uint SHGFI_SMALLICON = 0x1;
		private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
		private const uint SHGFI_SYSICONINDEX = 0x4000;
		private const uint SHGFI_TYPENAME = 0x400;
		private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
		private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;

		[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool DestroyIcon(IntPtr hIcon);

		[DllImport("shell32.dll", EntryPoint = "#727")]
		private static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

		[ComImportAttribute()]
		[GuidAttribute("46EB5926-582E-4017-9FDF-E8998DAA0950")]
		[InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
		private interface IImageList {
			[PreserveSig]
			int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
			[PreserveSig]
			int ReplaceIcon(int i, IntPtr hIcon, ref int pi);
			[PreserveSig]
			int SetOverlayImage(int iImage, int iOverlay);
			[PreserveSig]
			int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
			[PreserveSig]
			int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
			[PreserveSig]
			int Draw(ref IMAGELISTDRAWPARAMS pimldp);
			[PreserveSig]
			int Remove(int i);
			[PreserveSig]
			int GetIcon(int i, int flags, out IntPtr picon);
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct IMAGELISTDRAWPARAMS {
			public int cbSize;
			public IntPtr himl;
			public int i;
			public IntPtr hdcDst;
			public int x;
			public int y;
			public int cx;
			public int cy;
			public int xBitmap;
			public int yBitmap;
			public int rgbBk;
			public int rgbFg;
			public int fStyle;
			public int dwRop;
			public int fState;
			public int Frame;
			public int crEffect;
		}

		private const int SHIL_JUMBO = 0x4;
		private const int SHIL_EXTRALARGE = 0x2;
		private const int ILD_TRANSPARENT = 0x1;

		private const uint SHGFI_OPENICON = 0x2;

		public static ImageSource? GetFileIcon(string path, int size) {
			string ext = Path.GetExtension(path).ToLower();
			if (string.IsNullOrEmpty(ext)) ext = ".file";

			if (ext == ".exe" || ext == ".lnk" || ext == ".ico") {
				return GetIcon(path, false, false, false, size);
			}

			return GetIcon(ext, false, false, true, size);
		}

		public static ImageSource? GetFolderIcon(string path, bool open, int size) {
			return GetIcon(path, true, open, false, size);
		}

		private static ImageSource? GetIcon(string path, bool isFolder, bool isOpen, bool useFileAttributes, int size) {
			var flags = SHGFI_SYSICONINDEX;
			if (useFileAttributes) flags |= SHGFI_USEFILEATTRIBUTES;
			if (isOpen) flags |= SHGFI_OPENICON;

			var attributes = isFolder ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;

			SHFILEINFO shinfo = new();
			IntPtr result = SHGetFileInfo(path, attributes, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);

			if (result == IntPtr.Zero) return null;

			int iconIndex = shinfo.iIcon;
			string cacheKey = $"{iconIndex}_{size}";

			lock (_cacheLock) {
				if (_iconCache.TryGetValue(cacheKey, out var cachedIcon)) return cachedIcon;
			}

			ImageSource? icon = null;

			if (size <= 16) {
				var loadFlags = SHGFI_ICON | SHGFI_SMALLICON;
				if (useFileAttributes) loadFlags |= SHGFI_USEFILEATTRIBUTES;
				if (isOpen) loadFlags |= SHGFI_OPENICON;

				SHFILEINFO loadInfo = new();
				IntPtr hImg = SHGetFileInfo(path, attributes, ref loadInfo, (uint)Marshal.SizeOf(loadInfo), loadFlags);
				
				if (hImg != IntPtr.Zero) {
					icon = Imaging.CreateBitmapSourceFromHIcon(loadInfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
					_ = DestroyIcon(loadInfo.hIcon);
				}
			}
			else {
				int imageListType = size > 48 ? SHIL_JUMBO : SHIL_EXTRALARGE;
				var iidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
				int hres = SHGetImageList(imageListType, ref iidImageList, out IImageList? iml);

				if (hres == 0 && iml != null) {
					hres = iml.GetIcon(iconIndex, ILD_TRANSPARENT, out nint hIcon);
					if (hres == 0 && hIcon != IntPtr.Zero) {
						icon = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
						_ = DestroyIcon(hIcon);
					}
				}
			}

			if (icon != null) {
				icon.Freeze();
				lock (_cacheLock) {
					if (_iconCache.Count >= MaxCacheSize) _iconCache.Clear();
					_iconCache[cacheKey] = icon;
				}
			}

			return icon;
		}

		[DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
		private static extern void SHCreateItemFromParsingName(
			[MarshalAs(UnmanagedType.LPWStr)] string pszPath,
			IntPtr pbc,
			[MarshalAs(UnmanagedType.LPStruct)] Guid riid,
			[MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

		[ComImport]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		[Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
		private interface IShellItemImageFactory {
			void GetImage(
				[MarshalAs(UnmanagedType.Struct)] SIZE size,
				int flags,
				out IntPtr phbm);
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct SIZE {
			public int cx;
			public int cy;
		}

		[DllImport("gdi32.dll")]
		private static extern bool DeleteObject(IntPtr hObject);

		public static ImageSource? GetThumbnail(string path, int size) {
			try {
				Guid uuid = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");
				SHCreateItemFromParsingName(path, IntPtr.Zero, uuid, out IShellItemImageFactory factory);

				factory.GetImage(new SIZE { cx = size, cy = size }, 0, out IntPtr hBitmap);

				if (hBitmap != IntPtr.Zero) {
					var source = Imaging.CreateBitmapSourceFromHBitmap(
						hBitmap,
						IntPtr.Zero,
						Int32Rect.Empty,
						BitmapSizeOptions.FromEmptyOptions());

					_ = DeleteObject(hBitmap);
					source.Freeze();
					return source;
				}
			}
			catch { }
			return null;
		}
	}
}
#pragma warning restore SYSLIB1096 // Convert to 'GeneratedComInterface'
#pragma warning restore SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
