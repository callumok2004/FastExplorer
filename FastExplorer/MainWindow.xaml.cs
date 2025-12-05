using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using FastExplorer.ViewModels;
using FastExplorer.Helpers;

namespace FastExplorer {
	public partial class MainWindow : Window {
		public ICommand ViewMoreCommand { get; }

		public MainWindow() {
			InitializeComponent();
			AppSettings.Load();

			ViewMoreCommand = new RelayCommand(param => {
				if (param is FileItemViewModel fileItem) {
					var point = PointToScreen(Mouse.GetPosition(this));
					ShowShellContextMenu(fileItem, point);
				}
			});

			DataContext = new MainViewModel();
			Loaded += OnLoaded;
			Closing += (s, e) => AppSettings.Save();
			PreviewMouseDown += Window_PreviewMouseDown;
		}

		private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
			if (SearchBox.IsKeyboardFocused && !SearchBox.IsMouseOver) {
				var hit = VisualTreeHelper.HitTest(this, e.GetPosition(this));
				if (hit?.VisualHit != null) {
					var border = FindAncestor<Border>(hit.VisualHit);
					if (border != null && border.Name == "SearchBorder") return;
				}
				Keyboard.ClearFocus();
			}
		}

		private void OnLoaded(object sender, RoutedEventArgs e) {
			try { _ = SetPreferredAppMode(2); } catch { }

			var windowInteropHelper = new WindowInteropHelper(this);
			var hwnd = windowInteropHelper.Handle;

			int micaValue = 2;
			_ = DwmSetWindowAttribute(hwnd, 38, ref micaValue, sizeof(int));

			int darkMode = 1;
			_ = DwmSetWindowAttribute(hwnd, 20, ref darkMode, sizeof(int));

			MouseDown += (s, e) => {
				if (e.ChangedButton == MouseButton.XButton1) {
					if (DataContext is MainViewModel vm && vm.SelectedTab != null) {
						if (vm.SelectedTab.BackCommand.CanExecute(null)) vm.SelectedTab.BackCommand.Execute(null);
					}
				}
				else if (e.ChangedButton == MouseButton.XButton2) {
					if (DataContext is MainViewModel vm && vm.SelectedTab != null) {
						if (vm.SelectedTab.ForwardCommand.CanExecute(null)) vm.SelectedTab.ForwardCommand.Execute(null);
					}
				}
			};
		}

		[DllImport("dwmapi.dll")]
		private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

		private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
		private const int DWMWCP_ROUND = 2;

		[DllImport("uxtheme.dll", EntryPoint = "#135")]
		private static extern int SetPreferredAppMode(int preferredAppMode);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetCursorPos(out POINT lpPoint);

		[StructLayout(LayoutKind.Sequential)]
		public struct POINT {
			public int X;
			public int Y;
		}

		private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
			if (DataContext is MainViewModel vm && vm.SelectedTab != null && e.NewValue is DirectoryItemViewModel selectedDir) {
				vm.SelectedTab.NavigateTo(selectedDir.FullPath);

				if (sender is TreeView tv) {
					if (tv.ItemsSource == vm.QuickAccess) {
						vm.ClearSelection(vm.Drives);
					}
					else if (tv.ItemsSource == vm.Drives) {
						vm.ClearSelection(vm.QuickAccess);
					}
				}
			}
		}

		private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
			if (e.ChangedButton != MouseButton.Left) return;

			if (((ListView)sender).DataContext is TabViewModel vm && ((ListView)sender).SelectedItem is FileItemViewModel item) {
				vm.OpenCommand.Execute(item);
			}
		}

		private bool _isDraggingSelection;
		private Point _startPoint;

		private void ListView_MouseDown(object sender, MouseButtonEventArgs e) {
			Keyboard.ClearFocus();

			if (sender is ListView lv) {
				if (e.ChangedButton == MouseButton.Left) {
					_startPoint = e.GetPosition(lv);
				}

				var hit = VisualTreeHelper.HitTest(lv, e.GetPosition(lv));
				if (hit?.VisualHit != null) {
					if (FindAncestor<ScrollBar>(hit.VisualHit) != null) return;
					if (FindAncestor<GridViewColumnHeader>(hit.VisualHit) != null) return;

					var item = FindAncestor<ListViewItem>(hit.VisualHit);
					if (item == null) {
						lv.SelectedItems.Clear();

						if (e.ChangedButton == MouseButton.Left) {
							_isDraggingSelection = true;
							_ = Mouse.Capture(lv);

							if (FindName("SelectionBox") is Border selectionBox && FindName("SelectionCanvas") is Canvas selectionCanvas) {
								selectionBox.Width = 0;
								selectionBox.Height = 0;
								selectionCanvas.Visibility = Visibility.Visible;
								Canvas.SetLeft(selectionBox, _startPoint.X);
								Canvas.SetTop(selectionBox, _startPoint.Y);
							}
						}
					}
					else {
						if (e.ClickCount == 1 && e.ChangedButton == MouseButton.Left && item.IsSelected && (Keyboard.Modifiers & ModifierKeys.Control) == 0 && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) {
							e.Handled = true;
						}
					}

					if (item != null && e.ChangedButton == MouseButton.Middle) {
						if (item.DataContext is FileItemViewModel fileItem && fileItem.IsFolder) {
							if (DataContext is MainViewModel vm) {
								vm.AddTabCommand.Execute(fileItem.FullPath);
							}
						}
					}
				}
			}
		}

		private void ListView_MouseMove(object sender, MouseEventArgs e) {
			if (_isDraggingSelection && sender is ListView lv) {
				var currentPoint = e.GetPosition(lv);
				var x = Math.Min(currentPoint.X, _startPoint.X);
				var y = Math.Min(currentPoint.Y, _startPoint.Y);
				var w = Math.Abs(currentPoint.X - _startPoint.X);
				var h = Math.Abs(currentPoint.Y - _startPoint.Y);

				if (FindName("SelectionBox") is Border selectionBox) {
					Canvas.SetLeft(selectionBox, x);
					Canvas.SetTop(selectionBox, y);
					selectionBox.Width = w;
					selectionBox.Height = h;
				}

				UpdateSelection(lv, new Rect(x, y, w, h));
			}
			else if (e.LeftButton == MouseButtonState.Pressed && sender is ListView listView) {
				Point mousePos = e.GetPosition(listView);
				Vector diff = _startPoint - mousePos;
				if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
					Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance) {

					var listViewItem = FindAncestor<ListViewItem>((DependencyObject)e.OriginalSource);
					if (listViewItem != null && listView.SelectedItems.Count > 0) {
						foreach (var item in listView.SelectedItems) {
							if (item is DriveItemViewModel) return;
						}

						var files = new System.Collections.Specialized.StringCollection();
						foreach (FileItemViewModel item in listView.SelectedItems) {
							files.Add(item.FullPath);
						}

						var data = new DataObject();
						data.SetFileDropList(files);
						data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(2)));

						try {
							var result = DragDrop.DoDragDrop(listView, data, DragDropEffects.Copy | DragDropEffects.Move);

							if (DataContext is MainViewModel vm && vm.SelectedTab != null) {
								vm.SelectedTab.RefreshCommand.Execute(null);
							}
						}
						catch { }
					}
				}
			}
		}

		private void UpdateSelection(ListView lv, Rect selectionRect) {
			foreach (var item in lv.Items) {
				if (lv.ItemContainerGenerator.ContainerFromItem(item) is not ListViewItem container) continue;

				var transform = container.TransformToAncestor(lv);
				var itemRect = transform.TransformBounds(new Rect(0, 0, container.ActualWidth, container.ActualHeight));

				if (selectionRect.IntersectsWith(itemRect)) {
					container.IsSelected = true;
				}
				else {
					container.IsSelected = false;
				}
			}
		}

		private void ListView_MouseUp(object sender, MouseButtonEventArgs e) {
			if (_isDraggingSelection) {
				_isDraggingSelection = false;
				_ = Mouse.Capture(null);
				if (FindName("SelectionCanvas") is Canvas selectionCanvas) {
					selectionCanvas.Visibility = Visibility.Collapsed;
				}
			}
			else if (sender is ListView lv) {
				var hit = VisualTreeHelper.HitTest(lv, e.GetPosition(lv));
				var item = FindAncestor<ListViewItem>(hit?.VisualHit);
				if (item != null && (Keyboard.Modifiers & ModifierKeys.Control) == 0 && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) {
					if (item.IsSelected) {
						lv.SelectedItems.Clear();
						item.IsSelected = true;
					}
				}
			}
		}

		private FileItemViewModel? _lastDropTarget;

		private void FileListView_DragOver(object sender, DragEventArgs e) {
			e.Effects = DragDropEffects.Move;
			if ((e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey) {
				e.Effects = DragDropEffects.Copy;
			}

			if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
				if (sender is ListView lv2) {
					var hit = VisualTreeHelper.HitTest(lv2, e.GetPosition(lv2));
					FileItemViewModel? currentTarget = null;

					if (hit?.VisualHit != null) {
						var item = FindAncestor<ListViewItem>(hit.VisualHit);
						if (item != null && item.DataContext is FileItemViewModel fileItem && (fileItem.IsFolder || fileItem.IsDrive)) {
							currentTarget = fileItem;
						}
					}

					if (_lastDropTarget != currentTarget) {
						if (_lastDropTarget != null) _lastDropTarget.IsDropTarget = false;
						_lastDropTarget = currentTarget;
						if (_lastDropTarget != null) _lastDropTarget.IsDropTarget = true;
					}
				}
			}
			else {
				e.Effects = DragDropEffects.None;
			}
		}

		private void FileListView_DragLeave(object sender, DragEventArgs e) {
			if (_lastDropTarget != null) {
				_lastDropTarget.IsDropTarget = false;
				_lastDropTarget = null;
			}
		}

		private void FileListView_Drop(object sender, DragEventArgs e) {
			if (_lastDropTarget != null) {
				_lastDropTarget.IsDropTarget = false;
				_lastDropTarget = null;
			}

			if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
				var files = (string[])e.Data.GetData(DataFormats.FileDrop);
				if (files == null || files.Length == 0) return;

				string? destPath = null;

				if (sender is ListView lv) {
					var hit = VisualTreeHelper.HitTest(lv, e.GetPosition(lv));
					if (hit?.VisualHit != null) {
						var item = FindAncestor<ListViewItem>(hit.VisualHit);
						if (item != null && item.DataContext is FolderItemViewModel folder) {
							destPath = folder.FullPath;
						}
						else if (item != null && item.DataContext is DriveItemViewModel drive) {
							destPath = drive.FullPath;
						}
					}
				}

				if (string.IsNullOrEmpty(destPath)) {
					if (DataContext is MainViewModel vm && vm.SelectedTab != null) {
						destPath = vm.SelectedTab.CurrentPath;
					}
				}

				if (string.IsNullOrEmpty(destPath) || destPath == "This PC") return;

				bool isSame = false;
				foreach (var file in files) {
					if (string.Equals(file, destPath, StringComparison.OrdinalIgnoreCase)) {
						isSame = true;
						break;
					}
					var parent = Path.GetDirectoryName(file);
					if (string.Equals(parent, destPath, StringComparison.OrdinalIgnoreCase)) {
						isSame = true;
						break;
					}
				}
				if (isSame) return;

				bool isMove = true;
				if ((e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey) isMove = false;

				foreach (var file in files) {
					try {
						if (isMove) ShellHelper.MoveFile(file, destPath);
						else ShellHelper.CopyFile(file, destPath);
					}
					catch { }
				}

				if (DataContext is MainViewModel vm2 && vm2.SelectedTab != null) {
					vm2.SelectedTab.RefreshCommand.Execute(null);
				}
			}
		}

		private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject {
			while (current != null) {
				if (current is T t) return t;
				current = VisualTreeHelper.GetParent(current);
			}
			return null;
		}

		private void ListViewItem_MouseRightButtonUp(object sender, MouseButtonEventArgs e) {
		}

		private void ViewMore_Click(object sender, RoutedEventArgs e) {
			if (sender is MenuItem menuItem && menuItem.DataContext is FileItemViewModel fileItem) {
				var point = PointToScreen(Mouse.GetPosition(this));
				ShowShellContextMenu(fileItem, point);
			}
		}

		private void ContextMenu_Opened(object sender, RoutedEventArgs e) {
			if (sender is ContextMenu menu) {
				if (PresentationSource.FromVisual(menu) is HwndSource source) {
					WindowAccentCompositor.EnableBlur(source.Handle);

					int preference = DWMWCP_ROUND;
					_ = DwmSetWindowAttribute(source.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
				}

				if (menu.DataContext is FileItemViewModel fileItem) {
					_ = Dispatcher.BeginInvoke(new Action(() => {
						fileItem.LoadShellMenu();
						UpdateContextMenu(menu, fileItem.ShellMenuItems);
					}), System.Windows.Threading.DispatcherPriority.Background);
				}
			}
		}

		private void MenuItem_SubmenuOpened(object sender, RoutedEventArgs e) {
			if (sender is MenuItem menuItem) {
				_ = Dispatcher.BeginInvoke(new Action(() => {
					if (menuItem.Template.FindName("Popup", menuItem) is Popup popup && popup.Child is FrameworkElement child) {
						if (PresentationSource.FromVisual(child) is HwndSource source) {
							WindowAccentCompositor.EnableBlur(source.Handle);
							int preference = DWMWCP_ROUND;
							_ = DwmSetWindowAttribute(source.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
						}
					}
				}), System.Windows.Threading.DispatcherPriority.Input);
			}
		}

		private void UpdateContextMenu(ContextMenu menu, ObservableCollection<ShellContextMenu.ShellMenuItem> shellItems) {
			var itemsToRemove = menu.Items.OfType<MenuItem>().Where(x => x.Tag is string s && s == "ShellItem").ToList();
			foreach (var item in itemsToRemove) menu.Items.Remove(item);

			var separatorsToRemove = menu.Items.OfType<Separator>().Where(x => x.Tag is string s && s == "ShellItem").ToList();
			foreach (var item in separatorsToRemove) menu.Items.Remove(item);

			if (shellItems.Count > 0) {
				if (!shellItems[0].IsSeparator) {
					var sep = new Separator { Tag = "ShellItem" };
					if (Application.Current.MainWindow?.FindResource("ContextMenuSeparatorStyle") is Style style) {
						sep.Style = style;
					}
					_ = menu.Items.Add(sep);
				}

				foreach (var shellItem in shellItems) {
					_ = menu.Items.Add(CreateMenuItem(shellItem));
				}

				var moreSep = new Separator { Tag = "ShellItem" };
				if (Application.Current.MainWindow?.FindResource("ContextMenuSeparatorStyle") is Style moreSepStyle) {
					moreSep.Style = moreSepStyle;
				}
				_ = menu.Items.Add(moreSep);
				var showMore = new MenuItem {
					Header = "Show more options",
					Tag = "ShellItem",
					Icon = new TextBlock { Text = "\uE712", FontFamily = new FontFamily("Segoe Fluent Icons") }
				};
				showMore.Click += (s, e) => {
					if (menu.DataContext is FileItemViewModel fileItem) {
						menu.IsOpen = false;
						_ = Dispatcher.BeginInvoke(new Action(() => {
							_ = GetCursorPos(out POINT p);
							ShowShellContextMenu(fileItem, new Point(p.X, p.Y));
						}), System.Windows.Threading.DispatcherPriority.Input);
					}
				};
				_ = menu.Items.Add(showMore);
			}
		}

		private Control CreateMenuItem(ShellContextMenu.ShellMenuItem item) {
			if (item.IsSeparator) {
				var sep = new Separator { Tag = "ShellItem" };
				if (Application.Current.MainWindow?.FindResource("ContextMenuSeparatorStyle") is Style style) {
					sep.Style = style;
				}
				return sep;
			}

			var menuItem = new MenuItem {
				Header = item.Name,
				Command = item.Command,
				Tag = "ShellItem"
			};

			if (item.Icon != null) {
				menuItem.Icon = new Image { Source = item.Icon, Width = 16, Height = 16 };
			}

			if (item.Children.Count > 0) {
				foreach (var child in item.Children) {
					_ = menuItem.Items.Add(CreateMenuItem(child));
				}
			}

			return menuItem;
		}

		private void ShowShellContextMenu(FileItemViewModel fileItem, Point point) {
			var shellMenu = new ShellContextMenu();
			try {
				var fileInfo = new FileInfo(fileItem.FullPath);
				shellMenu.ShowContextMenu([fileInfo], point);
			}
			catch { }
		}

		private void TreeViewItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
			if (e.OriginalSource is DependencyObject d && FindAncestor<ToggleButton>(d) != null) return;

			if (sender is TreeViewItem item && item.DataContext is DirectoryItemViewModel dir) {
				if (DataContext is MainViewModel vm && vm.SelectedTab != null) {
					vm.SelectedTab.NavigateTo(dir.FullPath);
					e.Handled = true;
				}
			}
		}

		private void Sidebar_MouseDown(object sender, MouseButtonEventArgs e) {
			if (FindName("FileListView") is ListView lv) {
				lv.SelectedItems.Clear();
			}
		}

		private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e) {
			if (e.OriginalSource is GridViewColumnHeader header &&
				header.Column != null &&
				DataContext is MainViewModel vm &&
				vm.SelectedTab != null) {

				vm.SelectedTab.SortFiles(header.Column.Header.ToString());
			}
		}

		public static int MaxSearchResults { get; set; } = 1000;

		private void OpenContextMenu_Click(object sender, RoutedEventArgs e) {
			if (sender is FrameworkElement element && element.ContextMenu != null) {
				element.ContextMenu.PlacementTarget = element;
				element.ContextMenu.Placement = PlacementMode.Bottom;
				element.ContextMenu.IsOpen = true;
			}
		}

		private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (DataContext is MainViewModel vm && vm.SelectedTab != null && sender is ListView lv) {
				vm.SelectedTab.UpdateSelectionStatus(lv.SelectedItems.Cast<FileItemViewModel>().ToList());
			}
		}

		private void TreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
			_startPoint = e.GetPosition(null);
		}

		private void TreeView_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
			if (!e.Handled) {
				e.Handled = true;
				var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
				eventArg.RoutedEvent = UIElement.MouseWheelEvent;
				eventArg.Source = sender;
				var parent = VisualTreeHelper.GetParent((DependencyObject)sender) as UIElement;
				parent?.RaiseEvent(eventArg);
			}
		}

		private void TreeView_MouseMove(object sender, MouseEventArgs e) {
			if (e.LeftButton == MouseButtonState.Pressed) {
				Point mousePos = e.GetPosition(null);
				Vector diff = _startPoint - mousePos;
				if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
					Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance) {

					if (sender is TreeView treeView) {
						var treeViewItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);

						if (treeViewItem != null && treeViewItem.DataContext is DirectoryItemViewModel item) {
							if (DataContext is MainViewModel vm && treeView.ItemsSource == vm.QuickAccess) {
								try {
									_ = DragDrop.DoDragDrop(treeViewItem, item, DragDropEffects.Move);
								}
								catch { }
							}
						}
					}
				}
			}
		}

		private DirectoryItemViewModel? _lastTreeDropTarget;

		private void TreeView_DragOver(object sender, DragEventArgs e) {
			if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
				e.Effects = DragDropEffects.Move;
				if ((e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey) {
					e.Effects = DragDropEffects.Copy;
				}

				if (sender is TreeView tv) {
					var hit = VisualTreeHelper.HitTest(tv, e.GetPosition(tv));
					DirectoryItemViewModel? currentTarget = null;

					if (hit?.VisualHit != null) {
						var item = FindAncestor<TreeViewItem>(hit.VisualHit);
						if (item != null && item.DataContext is DirectoryItemViewModel dirItem) {
							currentTarget = dirItem;
						}
					}

					if (_lastTreeDropTarget != currentTarget) {
						if (_lastTreeDropTarget != null) _lastTreeDropTarget.IsDropTarget = false;
						_lastTreeDropTarget = currentTarget;
						if (_lastTreeDropTarget != null) _lastTreeDropTarget.IsDropTarget = true;
					}
				}
			}
			else if (!e.Data.GetDataPresent(typeof(DirectoryItemViewModel))) {
				e.Effects = DragDropEffects.None;
			}
		}

		private void TreeView_DragLeave(object sender, DragEventArgs e) {
			if (_lastTreeDropTarget != null) {
				_lastTreeDropTarget.IsDropTarget = false;
				_lastTreeDropTarget = null;
			}
		}

		private void TreeView_Drop(object sender, DragEventArgs e) {
			if (_lastTreeDropTarget != null) {
				_lastTreeDropTarget.IsDropTarget = false;
				_lastTreeDropTarget = null;
			}

			if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
				var files = (string[])e.Data.GetData(DataFormats.FileDrop);
				if (files == null || files.Length == 0) return;

				string? destPath = null;
				if (sender is TreeView tv) {
					var hit = VisualTreeHelper.HitTest(tv, e.GetPosition(tv));
					if (hit?.VisualHit != null) {
						var item = FindAncestor<TreeViewItem>(hit.VisualHit);
						if (item != null && item.DataContext is DirectoryItemViewModel dirItem) {
							destPath = dirItem.FullPath;
						}
					}
				}

				if (string.IsNullOrEmpty(destPath) || destPath == "This PC") return;

				bool isSame = false;
				foreach (var file in files) {
					if (string.Equals(file, destPath, StringComparison.OrdinalIgnoreCase)) {
						isSame = true;
						break;
					}
					var parent = Path.GetDirectoryName(file);
					if (string.Equals(parent, destPath, StringComparison.OrdinalIgnoreCase)) {
						isSame = true;
						break;
					}
				}
				if (isSame) return;

				bool isMove = true;
				if ((e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey) isMove = false;

				foreach (var file in files) {
					try {
						if (isMove) ShellHelper.MoveFile(file, destPath);
						else ShellHelper.CopyFile(file, destPath);
					}
					catch { }
				}

				if (DataContext is MainViewModel vm2 && vm2.SelectedTab != null) {
					vm2.SelectedTab.RefreshCommand.Execute(null);
				}
			}
			else if (e.Data.GetDataPresent(typeof(DirectoryItemViewModel))) {
				var source = e.Data.GetData(typeof(DirectoryItemViewModel)) as DirectoryItemViewModel;
				var treeViewItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);

				if (source != null && treeViewItem != null && treeViewItem.DataContext is DirectoryItemViewModel target) {
					if (DataContext is MainViewModel vm) {
						vm.ReorderQuickAccess(source, target);
					}
				}
			}
		}

		private PathSegmentViewModel? _lastPathDropTarget;

		private void PathSegment_DragOver(object sender, DragEventArgs e) {
			if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
				e.Effects = DragDropEffects.Move;
				if ((e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey) {
					e.Effects = DragDropEffects.Copy;
				}

				if (sender is FrameworkElement element && element.DataContext is PathSegmentViewModel segment) {
					if (_lastPathDropTarget != segment) {
						if (_lastPathDropTarget != null) _lastPathDropTarget.IsDropTarget = false;
						_lastPathDropTarget = segment;
						if (_lastPathDropTarget != null) _lastPathDropTarget.IsDropTarget = true;
					}
				}
			}
			else {
				e.Effects = DragDropEffects.None;
			}
			e.Handled = true;
		}

		private void PathSegment_DragLeave(object sender, DragEventArgs e) {
			if (_lastPathDropTarget != null) {
				_lastPathDropTarget.IsDropTarget = false;
				_lastPathDropTarget = null;
			}
			e.Handled = true;
		}

		private void PathSegment_Drop(object sender, DragEventArgs e) {
			if (_lastPathDropTarget != null) {
				_lastPathDropTarget.IsDropTarget = false;
				_lastPathDropTarget = null;
			}

			if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
				var files = (string[])e.Data.GetData(DataFormats.FileDrop);
				if (files == null || files.Length == 0) return;

				string? destPath = null;
				if (sender is FrameworkElement element && element.DataContext is PathSegmentViewModel segment) {
					destPath = segment.Path;
				}

				if (string.IsNullOrEmpty(destPath) || destPath == "This PC") return;

				// Check if source is same as destination
				bool isSame = false;
				foreach (var file in files) {
					if (string.Equals(file, destPath, StringComparison.OrdinalIgnoreCase)) {
						isSame = true;
						break;
					}
					var parent = Path.GetDirectoryName(file);
					if (string.Equals(parent, destPath, StringComparison.OrdinalIgnoreCase)) {
						isSame = true;
						break;
					}
				}
				if (isSame) return;

				bool isMove = true;
				if ((e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey) isMove = false;

				foreach (var file in files) {
					try {
						if (isMove) ShellHelper.MoveFile(file, destPath);
						else ShellHelper.CopyFile(file, destPath);
					}
					catch { }
				}

				if (DataContext is MainViewModel vm2 && vm2.SelectedTab != null) {
					vm2.SelectedTab.RefreshCommand.Execute(null);
				}
			}
			e.Handled = true;
		}

		private void TabItem_MouseDown(object sender, MouseButtonEventArgs e) {
			if (e.ChangedButton == MouseButton.Middle && sender is ListBoxItem item && item.DataContext is TabViewModel tab) {
				if (DataContext is MainViewModel vm) {
					vm.CloseTabCommand.Execute(tab);
				}
			}
		}

		private void RenameTextBox_LostFocus(object sender, RoutedEventArgs e) {
			if (sender is TextBox textBox && textBox.DataContext is FileItemViewModel fileItem) {
				if (DataContext is MainViewModel vm && vm.SelectedTab != null) {
					if (vm.SelectedTab.CommitRenameCommand.CanExecute(fileItem)) {
						vm.SelectedTab.CommitRenameCommand.Execute(fileItem);
					}
				}
			}
		}

		private void MenuItemButton_Click(object sender, RoutedEventArgs e) {
			if (sender is Button button) {
				var contextMenu = FindAncestor<ContextMenu>(button);
				if (contextMenu != null) {
					contextMenu.IsOpen = false;
				}
			}
		}

		private void PathBox_LostFocus(object sender, RoutedEventArgs e) {
			_ = Dispatcher.BeginInvoke(new Action(() => {
				if (DataContext is MainViewModel vm && vm.SelectedTab != null) {
					if (!vm.SelectedTab.IsSearching)
						vm.SelectedTab.IsPathEditing = false;
				}
			}), System.Windows.Threading.DispatcherPriority.Input);
		}

		private void Suggestion_Click(object sender, MouseButtonEventArgs e) {
			if (sender is ListBoxItem item && item.Content is string path) {
				if (DataContext is MainViewModel vm && vm.SelectedTab != null) {
					vm.SelectedTab.NavigateTo(path);
					vm.SelectedTab.IsPathEditing = false;
				}
			}
		}
	}
}
