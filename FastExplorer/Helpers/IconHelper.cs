using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices.Marshalling;

using CommunityToolkit.HighPerformance.Buffers;

namespace FastExplorer.Helpers {
	public static partial class IconHelper {
		private static readonly Dictionary<string, ImageSource> _stringCache = [];
		private static readonly Dictionary<long, ImageSource> _indexCache = [];
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
				unsafe {
					return new string(shinfo.szTypeName);
				}
			}
			return string.Empty;
		}

		public static ImageSource? GetFileIcon(string path, int size) {
			var extSpan = Path.GetExtension(path.AsSpan());
			string ext;

			if (extSpan.Length <= 64) {
				Span<char> buffer = stackalloc char[extSpan.Length];
				extSpan.ToLower(buffer, System.Globalization.CultureInfo.CurrentCulture);
				ext = StringPool.Shared.GetOrAdd(buffer);
			}
			else {
				ext = StringPool.Shared.GetOrAdd(extSpan.ToString().ToLower());
			}

			if (string.IsNullOrEmpty(ext)) ext = ".file";

			if (ext == ".exe" || ext == ".lnk" || ext == ".ico" || ext == ".url") {
				return GetIcon(path, false, false, false, size, ext == ".lnk" || ext == ".url");
			}

			lock (_cacheLock) {
				if (_stringCache.TryGetValue(ext, out var cachedIcon)) return cachedIcon;
			}

			var icon = GetIcon(ext, false, false, true, size, false);

			if (icon != null) {
				lock (_cacheLock) {
					if (_stringCache.Count >= MaxCacheSize) _stringCache.Clear();
					if (!_stringCache.ContainsKey(ext)) _stringCache[ext] = icon;
				}
			}
			return icon;
		}

		public static ImageSource? GetFolderIcon(string path, bool open, int size) {
			return GetIcon(path, true, open, false, size, false);
		}



		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private unsafe struct SHFILEINFO {
			public IntPtr hIcon;
			public int iIcon;
			public uint dwAttributes;
			public fixed char szDisplayName[260];
			public fixed char szTypeName[80];
		};

		private const uint SHGFI_ICON = 0x100;
		private const uint SHGFI_LARGEICON = 0x0;
		private const uint SHGFI_SMALLICON = 0x1;
		private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
		private const uint SHGFI_SYSICONINDEX = 0x4000;
		private const uint SHGFI_TYPENAME = 0x400;
		private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
		private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;

		[LibraryImport("shell32.dll", EntryPoint = "SHGetFileInfoW", StringMarshalling = StringMarshalling.Utf16)]
		private static partial IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

		[LibraryImport("shell32.dll", EntryPoint = "SHGetFileInfoW")]
		private static partial IntPtr SHGetFileInfo(IntPtr pidl, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

		[LibraryImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static partial bool DestroyIcon(IntPtr hIcon);

		[LibraryImport("shell32.dll", EntryPoint = "#727")]
		private static partial int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

		[GeneratedComInterface]
		[Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal partial interface IImageList {
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
		internal struct IMAGELISTDRAWPARAMS {
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
		private const uint SHGFI_LINKOVERLAY = 0x08000;

		[LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
		private static partial int SHParseDisplayName(
			string pszName,
			IntPtr pbc,
			out IntPtr ppidl,
			uint sfgaoIn,
			out uint psfgaoOut);

		[LibraryImport("shell32.dll")]
		private static partial void ILFree(IntPtr pidl);

		private const uint SHGFI_PIDL = 0x8;

		private static ImageSource? GetIcon(string path, bool isFolder, bool isOpen, bool useFileAttributes, int size, bool isShortcut) {
			IntPtr pidl = IntPtr.Zero;
			bool pidlCreated = false;

			if (path.StartsWith("::") || path.StartsWith("shell:")) {
				var thumb = GetThumbnail(path, size);
				if (thumb != null) return thumb;
				
				if (SHParseDisplayName(path, IntPtr.Zero, out pidl, 0, out _) == 0) {
					pidlCreated = true;
					useFileAttributes = false;
				}
			}

			var flags = SHGFI_SYSICONINDEX;
			if (useFileAttributes) flags |= SHGFI_USEFILEATTRIBUTES;
			if (isOpen) flags |= SHGFI_OPENICON;
			if (pidlCreated) flags |= SHGFI_PIDL;
			if (isShortcut) flags |= SHGFI_LINKOVERLAY;

			var attributes = isFolder ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;

			SHFILEINFO shinfo = new();
			IntPtr result;
			
			if (pidlCreated) {
				result = SHGetFileInfo(pidl, attributes, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
			} else {
				result = SHGetFileInfo(path, attributes, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
			}

			if (pidlCreated) ILFree(pidl);

			if (result == IntPtr.Zero) return null;

			int iconIndex = shinfo.iIcon;
			long cacheKey = ((long)iconIndex << 32) | (uint)size;

			lock (_cacheLock) {
				if (_indexCache.TryGetValue(cacheKey, out var cachedIcon)) return cachedIcon;
			}

			ImageSource? icon = null;

			if (size <= 16) {
				var loadFlags = SHGFI_ICON | SHGFI_SMALLICON;
				if (useFileAttributes) loadFlags |= SHGFI_USEFILEATTRIBUTES;
				if (isOpen) loadFlags |= SHGFI_OPENICON;
				if (pidlCreated) loadFlags |= SHGFI_PIDL;

				SHFILEINFO loadInfo = new();
				IntPtr hImg;
				
				if (pidlCreated) {
					if (SHParseDisplayName(path, IntPtr.Zero, out IntPtr pidl2, 0, out _) == 0) {
						hImg = SHGetFileInfo(pidl2, attributes, ref loadInfo, (uint)Marshal.SizeOf(loadInfo), loadFlags);
						ILFree(pidl2);
					} else {
						hImg = IntPtr.Zero;
					}
				} else {
					hImg = SHGetFileInfo(path, attributes, ref loadInfo, (uint)Marshal.SizeOf(loadInfo), loadFlags);
				}
				
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
					if (_indexCache.Count >= MaxCacheSize) _indexCache.Clear();
					_indexCache[cacheKey] = icon;
				}
			}

			return icon;
		}

		[LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
		private static partial int SHCreateItemFromParsingName(
			string pszPath,
			IntPtr pbc,
			in Guid riid,
			out IShellItemImageFactory ppv);

		[GeneratedComInterface]
		[Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal partial interface IShellItemImageFactory {
			void GetImage(
				SIZE size,
				int flags,
				out IntPtr phbm);
		}

		[StructLayout(LayoutKind.Sequential)]
		internal struct SIZE {
			public int cx;
			public int cy;
		}

		[LibraryImport("gdi32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static partial bool DeleteObject(IntPtr hObject);

		public static ImageSource? GetThumbnail(string path, int size) {
			try {
				Guid uuid = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");
				int hr = SHCreateItemFromParsingName(path, IntPtr.Zero, uuid, out IShellItemImageFactory factory);

				if (hr == 0 && factory != null) {
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
			}
			catch { }
			return null;
		}
	}
}
