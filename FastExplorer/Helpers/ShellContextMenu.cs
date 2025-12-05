using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows;

namespace FastExplorer.Helpers {
	public partial class ShellContextMenu {
		#region Interop

		[LibraryImport("shell32.dll")]
		private static partial int SHGetDesktopFolder(out IShellFolder ppshf);

		[LibraryImport("user32.dll")]
		private static partial IntPtr CreatePopupMenu();

		[LibraryImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static partial bool DestroyMenu(IntPtr hMenu);

		[LibraryImport("user32.dll")]
		private static partial int TrackPopupMenuEx(IntPtr hmenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

		[LibraryImport("user32.dll")]
		private static partial IntPtr GetForegroundWindow();

		[GeneratedComInterface]
		[Guid("000214E6-0000-0000-C000-000000000046")]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal partial interface IShellFolder {
			void ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, out uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);
			void EnumObjects(IntPtr hwnd, int grfFlags, out IntPtr ppenumIDList);
			void BindToObject(IntPtr pidl, IntPtr pbc, in Guid riid, out IntPtr ppv);
			void BindToStorage(IntPtr pidl, IntPtr pbc, in Guid riid, out IntPtr ppv);
			void CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
			void CreateViewObject(IntPtr hwndOwner, in Guid riid, out IntPtr ppv);
			void GetAttributesOf(uint cidl, IntPtr apidl, ref uint rgfInOut);
			void GetUIObjectOf(IntPtr hwndOwner, uint cidl, IntPtr apidl, in Guid riid, ref uint rgfReserved, out IntPtr ppv);
			void GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr pName);
			void SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
		}

		[GeneratedComInterface]
		[Guid("000214e4-0000-0000-c000-000000000046")]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal partial interface IContextMenu {
			[PreserveSig]
			int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
			[PreserveSig]
			int InvokeCommand(ref CMINVOKECOMMANDINFO pici);
			[PreserveSig]
			int GetCommandString(uint idCmd, uint uType, ref uint pwReserved, IntPtr pszName, uint cchMax);
		}

		[StructLayout(LayoutKind.Sequential)]
		internal struct CMINVOKECOMMANDINFO {
			public int cbSize;
			public int fMask;
			public IntPtr hwnd;
			public IntPtr lpVerb;
			public IntPtr lpParameters;
			public IntPtr lpDirectory;
			public int nShow;
			public int dwHotKey;
			public IntPtr hIcon;
		}

		[LibraryImport("user32.dll")]
		private static partial int GetMenuItemCount(IntPtr hMenu);

		[LibraryImport("user32.dll", EntryPoint = "GetMenuItemInfoW", StringMarshalling = StringMarshalling.Utf16)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static partial bool GetMenuItemInfo(IntPtr hMenu, uint uItem, [MarshalAs(UnmanagedType.Bool)] bool fByPosition, ref MENUITEMINFO lpmii);

		[StructLayout(LayoutKind.Sequential)]
		internal struct MENUITEMINFO {
			public uint cbSize;
			public uint fMask;
			public uint fType;
			public uint fState;
			public uint wID;
			public IntPtr hSubMenu;
			public IntPtr hbmpChecked;
			public IntPtr hbmpUnchecked;
			public IntPtr dwItemData;
			public IntPtr dwTypeData;
			public uint cch;
			public IntPtr hbmpItem;
		}

		private const uint MIIM_ID = 0x00000002;
		private const uint MIIM_STRING = 0x00000040;
		private const uint MIIM_FTYPE = 0x00000100;
		private const uint MIIM_BITMAP = 0x00000080;
		private const uint MIIM_SUBMENU = 0x00000004;
		private const uint MFT_STRING = 0x00000000;
		private const uint MFT_SEPARATOR = 0x00000800;

		#endregion

		private static readonly HashSet<string> _excludedItems = new(StringComparer.OrdinalIgnoreCase) {
						"Open", "Pin to Quick access", "Copy as path", "Properties",
						"Cut", "Copy", "Paste", "Rename", "Delete", "Share",
						"Pin to Start", "Unpin from Start",
						"Scan with Microsoft Defender...",
						"Give access to", "Restore previous versions",
						"Include in library", "Send to", "Create shortcut",
						"Open in Terminal", "Open in new window", "Open file location",
						"Sign and encrypt", "More GpgEX options"
				};

		public class ShellMenuItem {
			public string Name { get; set; } = string.Empty;
			public int Id { get; set; }
			public bool IsSeparator { get; set; }
			public ICommand? Command { get; set; }
			public ImageSource? Icon { get; set; }
			public List<ShellMenuItem> Children { get; set; } = [];
		}

		private class ShellCommand(IContextMenu contextMenu, int id) : ICommand {
			private readonly IContextMenu _contextMenu = contextMenu;
			private readonly int _id = id;

			public event EventHandler? CanExecuteChanged { add { } remove { } }
			public bool CanExecute(object? parameter) => true;

			public void Execute(object? parameter) {
				try {
					var ici = new CMINVOKECOMMANDINFO {
						cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFO>(),
						lpVerb = _id,
						nShow = 1,
					};
					_ = _contextMenu.InvokeCommand(ref ici);
				}
				catch (Exception) {
				}
			}
		}

		private static List<ShellMenuItem> GetMenuItems(IntPtr hMenu, IContextMenu contextMenu, bool isRoot) {
			var items = new List<ShellMenuItem>();
			int count = GetMenuItemCount(hMenu);
			for (uint i = 0; i < count; i++) {
				MENUITEMINFO mii = new();
				mii.cbSize = (uint)Marshal.SizeOf(mii);
				mii.fMask = MIIM_ID | MIIM_STRING | MIIM_FTYPE | MIIM_BITMAP | MIIM_SUBMENU;
				mii.dwTypeData = IntPtr.Zero;

				if (GetMenuItemInfo(hMenu, i, true, ref mii)) {
					if ((mii.fType & MFT_SEPARATOR) == MFT_SEPARATOR) {
						if (items.Count > 0 && !items[^1].IsSeparator) {
							items.Add(new ShellMenuItem { IsSeparator = true });
						}
					}
					else {
						mii.cch++;
						mii.dwTypeData = Marshal.AllocCoTaskMem((int)mii.cch * 2);

						if (GetMenuItemInfo(hMenu, i, true, ref mii)) {
							string? title = Marshal.PtrToStringUni(mii.dwTypeData);
							if (!string.IsNullOrEmpty(title)) {
								string cleanName = title.Replace("&", "");

								if (isRoot && _excludedItems.Contains(cleanName)) {
									Marshal.FreeCoTaskMem(mii.dwTypeData);
									continue;
								}

								var item = new ShellMenuItem {
									Name = cleanName,
									Id = (int)mii.wID,
									Command = new ShellCommand(contextMenu, (int)mii.wID - 1)
								};

								if (mii.hbmpItem != IntPtr.Zero) {
									try {
										item.Icon = Imaging.CreateBitmapSourceFromHBitmap(
												mii.hbmpItem,
												IntPtr.Zero,
												Int32Rect.Empty,
												System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
									}
									catch { }
								}

								if (mii.hSubMenu != IntPtr.Zero) {
									item.Children = GetMenuItems(mii.hSubMenu, contextMenu, false);
								}

								items.Add(item);
							}
						}
						Marshal.FreeCoTaskMem(mii.dwTypeData);
					}
				}
			}

			if (items.Count > 0 && items[^1].IsSeparator) {
				items.RemoveAt(items.Count - 1);
			}

			return items;
		}

		public static List<ShellMenuItem> GetContextMenuItems(FileInfo[] files) {
			var items = new List<ShellMenuItem>();
			if (files == null || files.Length == 0) return items;

			try {
				_ = SHGetDesktopFolder(out IShellFolder desktopFolder);

				IntPtr[] pidls = new IntPtr[files.Length];
				IShellFolder? parentFolder = null;

				var parentDir = files[0].DirectoryName;
				if (string.IsNullOrEmpty(parentDir)) return items;

				uint pdwAttributes = 0;

				desktopFolder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, parentDir, out uint pchEaten, out IntPtr parentPidl, ref pdwAttributes);
				desktopFolder.BindToObject(parentPidl, IntPtr.Zero, typeof(IShellFolder).GUID, out IntPtr parentFolderPtr);
				
				// Convert IntPtr to IShellFolder
				var strategy = new StrategyBasedComWrappers();
				parentFolder = (IShellFolder)strategy.GetOrCreateObjectForComInstance(parentFolderPtr, CreateObjectFlags.None);

				for (int i = 0; i < files.Length; i++) {
					uint attr = 0;
					parentFolder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, files[i].Name, out uint eaten, out pidls[i], ref attr);
				}

				IContextMenu contextMenu;
				unsafe {
					fixed (IntPtr* pPidls = pidls) {
						uint rgfReserved = 0;
						parentFolder.GetUIObjectOf(IntPtr.Zero, (uint)pidls.Length, (IntPtr)pPidls, typeof(IContextMenu).GUID, ref rgfReserved, out IntPtr contextMenuPtr);
						contextMenu = (IContextMenu)strategy.GetOrCreateObjectForComInstance(contextMenuPtr, CreateObjectFlags.None);
					}
				}

				IntPtr hMenu = CreatePopupMenu();
				_ = contextMenu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, 0);

				items = GetMenuItems(hMenu, contextMenu, true);

				_ = DestroyMenu(hMenu);
				// ReleaseComObject is not needed for StrategyBasedComWrappers, just let them go out of scope
			}
			catch { }

			return items;
		}

		public static void ShowContextMenu(FileInfo[] files, Point point) {
			if (files == null || files.Length == 0) return;

			try {
				_ = SHGetDesktopFolder(out IShellFolder desktopFolder);

				IntPtr[] pidls = new IntPtr[files.Length];
				IShellFolder? parentFolder = null;

				var parentDir = files[0].DirectoryName;
				if (string.IsNullOrEmpty(parentDir)) return;

				uint pdwAttributes = 0;

				desktopFolder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, parentDir, out uint pchEaten, out IntPtr parentPidl, ref pdwAttributes);
				desktopFolder.BindToObject(parentPidl, IntPtr.Zero, typeof(IShellFolder).GUID, out IntPtr parentFolderPtr);
				
				var strategy = new StrategyBasedComWrappers();
				parentFolder = (IShellFolder)strategy.GetOrCreateObjectForComInstance(parentFolderPtr, CreateObjectFlags.None);

				for (int i = 0; i < files.Length; i++) {
					uint attr = 0;
					parentFolder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, files[i].Name, out uint eaten, out pidls[i], ref attr);
				}

				IContextMenu contextMenu;
				unsafe {
					fixed (IntPtr* pPidls = pidls) {
						uint rgfReserved = 0;
						parentFolder.GetUIObjectOf(IntPtr.Zero, (uint)pidls.Length, (IntPtr)pPidls, typeof(IContextMenu).GUID, ref rgfReserved, out IntPtr contextMenuPtr);
						contextMenu = (IContextMenu)strategy.GetOrCreateObjectForComInstance(contextMenuPtr, CreateObjectFlags.None);
					}
				}

				IntPtr hMenu = CreatePopupMenu();
				_ = contextMenu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, 0);

				int selected = TrackPopupMenuEx(hMenu, 0x0100 | 0x0002, (int)point.X, (int)point.Y, GetForegroundWindow(), IntPtr.Zero);

				if (selected > 0) {
					CMINVOKECOMMANDINFO ici = new();
					ici.cbSize = Marshal.SizeOf(ici);
					ici.lpVerb = selected - 1;
					ici.nShow = 1;

					_ = contextMenu.InvokeCommand(ref ici);
				}

				_ = DestroyMenu(hMenu);
			}
			catch (Exception ex) {
				_ = System.Windows.MessageBox.Show("Error showing context menu: " + ex.Message);
			}
		}
	}
}
