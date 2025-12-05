using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Documents;

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
				var hit = VisualTreeHelper.HitTest(lv, e.GetPosition(lv));
				if (hit?.VisualHit != null) {
					if (FindAncestor<ScrollBar>(hit.VisualHit) != null) return;
					if (FindAncestor<GridViewColumnHeader>(hit.VisualHit) != null) return;

					var item = FindAncestor<ListViewItem>(hit.VisualHit);
					if (item == null) {
						lv.SelectedItems.Clear();

						if (e.ChangedButton == MouseButton.Left) {
							_isDraggingSelection = true;
							_startPoint = e.GetPosition(lv);
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
					else if (e.ChangedButton == MouseButton.Middle) {
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

		private DragAdorner? _dragAdorner;
		private AdornerLayer? _adornerLayer;

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
								_adornerLayer = AdornerLayer.GetAdornerLayer(treeView);
								_dragAdorner = new DragAdorner(treeView, treeViewItem, e.GetPosition(treeViewItem));
								_adornerLayer.Add(_dragAdorner);

								try {
									_ = DragDrop.DoDragDrop(treeViewItem, item, DragDropEffects.Move);
								}
								finally {
									_adornerLayer.Remove(_dragAdorner);
									_dragAdorner = null;
									_adornerLayer = null;
								}
							}
						}
					}
				}
			}
		}

		private void TreeView_DragOver(object sender, DragEventArgs e) {
			if (_dragAdorner != null && sender is TreeView treeView) {
				_dragAdorner.UpdatePosition(e.GetPosition(treeView));
			}
		}

		private void TreeView_Drop(object sender, DragEventArgs e) {
			if (e.Data.GetDataPresent(typeof(DirectoryItemViewModel))) {
				var source = e.Data.GetData(typeof(DirectoryItemViewModel)) as DirectoryItemViewModel;
				var treeViewItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);

				if (source != null && treeViewItem != null && treeViewItem.DataContext is DirectoryItemViewModel target) {
					if (DataContext is MainViewModel vm) {
						vm.ReorderQuickAccess(source, target);
					}
				}
			}
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

	#region ViewModels

	public class PercentToColorConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			if (value is double percent) {
				if (percent >= 90) return Brushes.Red;
				if (percent >= 75) return Brushes.Orange;
				return Brushes.LimeGreen;
			}
			return Brushes.Gray;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			throw new NotImplementedException();
		}
	}

	public class MathConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			if (value is double d && double.TryParse(parameter?.ToString(), out double factor)) {
				return d * factor;
			}
			return value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			throw new NotImplementedException();
		}
	}

	public class MainViewModel : ViewModelBase {
		private ObservableCollection<DirectoryItemViewModel> _drives;
		private ObservableCollection<DirectoryItemViewModel> _quickAccess;
		private ObservableCollection<TabViewModel> _tabs;
		private TabViewModel? _selectedTab;

		public ICommand AddTabCommand { get; }
		public ICommand CloseTabCommand { get; }
		public ICommand NavigateToThisPCCommand { get; }
		public ICommand OpenOptionsCommand { get; }
		public ICommand PinToQuickAccessCommand { get; }
		public ICommand UnpinFromQuickAccessCommand { get; }
		public ICommand MovePinUpCommand { get; }
		public ICommand MovePinDownCommand { get; }

		private string _systemStatus = "CPU: 0%   Mem: 0 MB";
		public string SystemStatus {
			get => _systemStatus;
			set => SetProperty(ref _systemStatus, value);
		}

		private readonly System.Windows.Threading.DispatcherTimer _statusTimer;
		private readonly Process _currentProcess;
		private TimeSpan _lastProcessorTime;
		private DateTime _lastTimerTick;

		public MainViewModel() {
			_drives = [];
			_quickAccess = [];
			_tabs = [];

			_currentProcess = Process.GetCurrentProcess();
			_lastProcessorTime = _currentProcess.TotalProcessorTime;
			_lastTimerTick = DateTime.Now;
			_statusTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
			_statusTimer.Tick += UpdateSystemStatus;
			_statusTimer.Start();

			LoadQuickAccess();
			LoadDrives();

			AddTabCommand = new RelayCommand(param => AddTab(param as string));
			CloseTabCommand = new RelayCommand(param => CloseTab(param as TabViewModel));
			NavigateToThisPCCommand = new RelayCommand(_ => {
				SelectedTab?.NavigateTo("This PC");
				ClearSelection(QuickAccess);
				ClearSelection(Drives);
			});
			OpenOptionsCommand = new RelayCommand(_ => {
				var options = new OptionsWindow { Owner = Application.Current.MainWindow };
				_ = options.ShowDialog();
			});
			PinToQuickAccessCommand = new RelayCommand(param => {
				if (param is string path) PinToQuickAccess(path);
				else if (param is DirectoryItemViewModel dir) PinToQuickAccess(dir.FullPath);
				else if (param is FolderItemViewModel folder) PinToQuickAccess(folder.FullPath);
			});
			UnpinFromQuickAccessCommand = new RelayCommand(param => {
				if (param is string path) UnpinFromQuickAccess(path);
				else if (param is DirectoryItemViewModel dir) UnpinFromQuickAccess(dir.FullPath);
				else if (param is FolderItemViewModel folder) UnpinFromQuickAccess(folder.FullPath);
			});
			MovePinUpCommand = new RelayCommand(param => MovePin(param, -1));
			MovePinDownCommand = new RelayCommand(param => MovePin(param, 1));

			AddTab();
		}

		private void UpdateSystemStatus(object? sender, EventArgs e) {
			try {
				var now = DateTime.Now;
				var currentProcessorTime = _currentProcess.TotalProcessorTime;
				var cpuUsage = (currentProcessorTime - _lastProcessorTime).TotalMilliseconds / (now - _lastTimerTick).TotalMilliseconds / Environment.ProcessorCount * 100;
				_lastProcessorTime = currentProcessorTime;
				_lastTimerTick = now;

				long memory = _currentProcess.WorkingSet64;
				SystemStatus = $"CPU: {cpuUsage:0}%   Mem: {FileItemViewModel.FormatSize(memory)}";
			}
			catch { }
		}

		private void MovePin(object? param, int direction) {
			string? path = null;
			if (param is DirectoryItemViewModel dir) path = dir.FullPath;

			if (path != null) {
				var list = AppSettings.Current.PinnedFolders;
				int index = list.FindIndex(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
				if (index >= 0) {
					int newIndex = index + direction;
					if (newIndex >= 0 && newIndex < list.Count) {
						var item = list[index];
						list.RemoveAt(index);
						list.Insert(newIndex, item);
						AppSettings.Save();
						LoadQuickAccess();
					}
				}
			}
		}

		public void ClearSelection(IEnumerable<DirectoryItemViewModel> items) {
			foreach (var item in items) {
				if (item.IsSelected) item.IsSelected = false;
				ClearSelection(item.SubDirectories);
			}
		}

		public ObservableCollection<DirectoryItemViewModel> Drives {
			get => _drives;
			set => SetProperty(ref _drives, value);
		}

		public ObservableCollection<DirectoryItemViewModel> QuickAccess {
			get => _quickAccess;
			set => SetProperty(ref _quickAccess, value);
		}

		public ObservableCollection<TabViewModel> Tabs {
			get => _tabs;
			set => SetProperty(ref _tabs, value);
		}

		public TabViewModel? SelectedTab {
			get => _selectedTab;
			set => SetProperty(ref _selectedTab, value);
		}

		public void ReorderQuickAccess(DirectoryItemViewModel source, DirectoryItemViewModel target) {
			var list = AppSettings.Current.PinnedFolders;
			int oldIndex = list.FindIndex(p => p.Equals(source.FullPath, StringComparison.OrdinalIgnoreCase));
			int newIndex = list.FindIndex(p => p.Equals(target.FullPath, StringComparison.OrdinalIgnoreCase));

			if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex) {
				var item = list[oldIndex];
				list.RemoveAt(oldIndex);
				list.Insert(newIndex, item);
				AppSettings.Save();
				LoadQuickAccess();
			}
		}

		private void AddTab(string? path = null) {
			var newTab = new TabViewModel(this);
			Tabs.Add(newTab);
			SelectedTab = newTab;
			newTab.NavigateTo(path ?? "This PC");
		}

		private void CloseTab(TabViewModel? tab) {
			if (tab == null) return;

			if (tab == SelectedTab) {
				int index = Tabs.IndexOf(tab);
				if (index > 0) {
					SelectedTab = Tabs[index - 1];
				}
				else if (Tabs.Count > 1) {
					SelectedTab = Tabs[index + 1];
				}
			}

			_ = Tabs.Remove(tab);
			if (Tabs.Count == 0) Application.Current.Shutdown();
		}

		private void LoadQuickAccess() {
			QuickAccess.Clear();

			if (AppSettings.Current.PinnedFolders.Count == 0) {
				var defaults = new[] {
					Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
					Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
					Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
					Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
					Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
					Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads"
				};

				foreach (var path in defaults) {
					if (Directory.Exists(path) && !AppSettings.Current.PinnedFolders.Any(p => p.Equals(path, StringComparison.OrdinalIgnoreCase))) {
						AppSettings.Current.PinnedFolders.Add(path);
					}
				}
				AppSettings.Save();
			}

			foreach (var path in AppSettings.Current.PinnedFolders) {
				if (Directory.Exists(path)) {
					QuickAccess.Add(new DirectoryItemViewModel(path));
				}
			}
		}

		public void PinToQuickAccess(string path) {
			if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
			if (!AppSettings.Current.PinnedFolders.Any(p => p.Equals(path, StringComparison.OrdinalIgnoreCase))) {
				AppSettings.Current.PinnedFolders.Add(path);
				AppSettings.Save();
				LoadQuickAccess();
			}
		}

		public void UnpinFromQuickAccess(string path) {
			var item = AppSettings.Current.PinnedFolders.FirstOrDefault(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
			if (item != null) {
				_ = AppSettings.Current.PinnedFolders.Remove(item);
				AppSettings.Save();
				LoadQuickAccess();
			}
		}

		private void LoadDrives() {
			foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady)) {
				Drives.Add(new DirectoryItemViewModel(drive.RootDirectory.FullName, drive.Name, drive.VolumeLabel, true));
			}
		}
	}

	public class PathSegmentViewModel {
		public string Name { get; }
		public string Path { get; }
		public ICommand NavigateCommand { get; }

		public PathSegmentViewModel(string name, string path, ICommand navigateCommand) {
			Name = name;
			Path = path;
			NavigateCommand = navigateCommand;
		}
	}

	public class TabViewModel : ViewModelBase {
		private MainViewModel _mainViewModel;
		private string _currentPath = string.Empty;
		private string _tabName = "Home";
		private ImageSource? _icon;
		private ObservableCollection<FileItemViewModel> _files;
		private List<FileItemViewModel> _allFiles;
		private DirectoryItemViewModel? _selectedDirectory;
		private string _statusText = "Ready";
		private int _itemCount;
		private string _searchText = string.Empty;
		private Stack<string> _backHistory = new();
		private Stack<string> _forwardHistory = new();
		private bool _isNavigating;
		private CancellationTokenSource? _searchCts;
		private string _sortProperty = AppSettings.Current.SortProperty;
		private ListSortDirection _sortDirection = AppSettings.Current.SortDirection == "Ascending" ? ListSortDirection.Ascending : ListSortDirection.Descending;
		private double _itemSize = AppSettings.Current.ItemSize;
		private bool _isSearching;
		private ObservableCollection<PathSegmentViewModel> _pathSegments = [];
		private bool _isPathEditing;
		private string _addressBarText = string.Empty;
		private ObservableCollection<string> _suggestions = [];
		private List<FileItemViewModel> _selectedItems = [];

		public ICommand NavigateCommand { get; }
		public ICommand BackCommand { get; }
		public ICommand ForwardCommand { get; }
		public ICommand OpenCommand { get; }
		public ICommand CopyPathCommand { get; }
		public ICommand ShowInExplorerCommand { get; }
		public ICommand SetViewCommand { get; }
		public ICommand SortFilesCommand { get; }
		public ICommand CutCommand { get; }
		public ICommand CopyCommand { get; }
		public ICommand PasteCommand { get; }
		public ICommand RenameCommand { get; }
		public ICommand CommitRenameCommand { get; }
		public ICommand CancelRenameCommand { get; }
		public ICommand ShareCommand { get; }
		public ICommand DeleteCommand { get; }
		public ICommand PropertiesCommand { get; }
		public ICommand RefreshCommand { get; }
		public ICommand TogglePathEditCommand { get; }

		private bool _isRefreshing;
		public bool IsRefreshing {
			get => _isRefreshing;
			set => SetProperty(ref _isRefreshing, value);
		}

		public TabViewModel(MainViewModel mainViewModel) {
			_mainViewModel = mainViewModel;
			_files = [];
			_allFiles = [];

			NavigateCommand = new RelayCommand(param => NavigateTo(param as string));
			BackCommand = new RelayCommand(_ => GoBack(), _ => _backHistory.Count > 0);
			ForwardCommand = new RelayCommand(_ => GoForward(), _ => _forwardHistory.Count > 0);
			OpenCommand = new RelayCommand(OpenItem);
			CopyPathCommand = new RelayCommand(param => {
				if (param is FileItemViewModel item) {
					try { Clipboard.SetText(item.FullPath); } catch { }
				}
			});
			ShowInExplorerCommand = new RelayCommand(param => {
				if (param is FileItemViewModel item) {
					try { _ = Process.Start("explorer.exe", $"/select,\"{item.FullPath}\""); } catch { }
				}
			});
			SetViewCommand = new RelayCommand(param => {
				if (double.TryParse(param?.ToString(), out double size)) {
					ItemSize = size;
				}
			});
			SortFilesCommand = new RelayCommand(param => SortFiles(param as string));

			CutCommand = new RelayCommand(param => {
				var itemsToCut = new List<string>();
				if (param is FileItemViewModel item) {
					if (_selectedItems.Contains(item)) itemsToCut.AddRange(_selectedItems.Select(x => x.FullPath));
					else itemsToCut.Add(item.FullPath);
				}
				else if (_selectedItems.Count > 0) {
					itemsToCut.AddRange(_selectedItems.Select(x => x.FullPath));
				}

				if (itemsToCut.Count > 0) {
					try {
						var list = new System.Collections.Specialized.StringCollection();
						list.AddRange(itemsToCut.ToArray());
						var data = new DataObject();
						data.SetFileDropList(list);
						data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(5)));
						Clipboard.SetDataObject(data, true);
					}
					catch { }
				}
			});
			CopyCommand = new RelayCommand(param => {
				var itemsToCopy = new List<string>();
				if (param is FileItemViewModel item) {
					if (_selectedItems.Contains(item)) itemsToCopy.AddRange(_selectedItems.Select(x => x.FullPath));
					else itemsToCopy.Add(item.FullPath);
				}
				else if (_selectedItems.Count > 0) {
					itemsToCopy.AddRange(_selectedItems.Select(x => x.FullPath));
				}

				if (itemsToCopy.Count > 0) {
					try {
						var list = new System.Collections.Specialized.StringCollection();
						list.AddRange(itemsToCopy.ToArray());
						Clipboard.SetFileDropList(list);
						
						// Also set with DropEffect for "Copy" (2)
						var data = new DataObject();
						data.SetFileDropList(list);
						data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(2)));
						Clipboard.SetDataObject(data, true);
					}
					catch { }
				}
			});
			PasteCommand = new RelayCommand(async _ => {
				if (Clipboard.ContainsFileDropList()) {
					var files = Clipboard.GetFileDropList();
					bool isMove = false;
					try {
						var data = Clipboard.GetDataObject();
						if (data != null && data.GetDataPresent("Preferred DropEffect")) {
							var stream = data.GetData("Preferred DropEffect") as MemoryStream;
							if (stream != null) {
								byte[] bytes = stream.ToArray();
								if (bytes.Length > 0 && bytes[0] == 5) isMove = true;
							}
						}
					} catch {}

					foreach (string? sourcePath in files) {
						if (sourcePath == null) continue;
						if (isMove) ShellHelper.MoveFile(sourcePath, CurrentPath);
						else ShellHelper.CopyFile(sourcePath, CurrentPath);
					}
					await LoadFilesAsync(CurrentPath);
				}
			}, _ => Clipboard.ContainsFileDropList() && !string.IsNullOrEmpty(CurrentPath) && CurrentPath != "This PC");
			RenameCommand = new RelayCommand(param => {
				var itemToRename = param as FileItemViewModel;
				if (itemToRename == null && _selectedItems.Count == 1) itemToRename = _selectedItems[0];

				if (itemToRename != null) {
					itemToRename.RenameText = itemToRename.Name;
					itemToRename.IsRenaming = true;
				}
			});
			CommitRenameCommand = new RelayCommand(async param => {
				if (param is FileItemViewModel item) {
					item.IsRenaming = false;
					if (item.RenameText != item.Name && !string.IsNullOrWhiteSpace(item.RenameText)) {
						string newName = item.RenameText;
						string oldExt = Path.GetExtension(item.Name);
						string newExt = Path.GetExtension(newName);

						if (!string.Equals(oldExt, newExt, StringComparison.OrdinalIgnoreCase)) {
							var result = MessageBox.Show(
								"If you change a file name extension, the file might become unusable.\nAre you sure you want to change it?",
								"Rename",
								MessageBoxButton.YesNo,
								MessageBoxImage.Warning);
							
							if (result != MessageBoxResult.Yes) {
								item.RenameText = item.Name;
								return;
							}
						}

						string newPath = Path.Combine(Path.GetDirectoryName(item.FullPath)!, newName);
						try {
							if (item.IsFolder) Directory.Move(item.FullPath, newPath);
							else File.Move(item.FullPath, newPath);
							await LoadFilesAsync(CurrentPath);
						}
						catch (Exception ex) {
							MessageBox.Show($"Error renaming: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
						}
					}
				}
			});
			CancelRenameCommand = new RelayCommand(param => {
				if (param is FileItemViewModel item) {
					item.IsRenaming = false;
					item.RenameText = item.Name;
				}
			});
			ShareCommand = new RelayCommand(param => { });
			DeleteCommand = new RelayCommand(param => {
				var itemsToDelete = new List<FileItemViewModel>();
				if (param is FileItemViewModel item) {
					if (_selectedItems.Contains(item)) itemsToDelete.AddRange(_selectedItems);
					else itemsToDelete.Add(item);
				}
				else if (_selectedItems.Count > 0) {
					itemsToDelete.AddRange(_selectedItems);
				}

				if (itemsToDelete.Count > 0) {
					string message = itemsToDelete.Count == 1
						? $"Are you sure you want to move '{itemsToDelete[0].Name}' to the Recycle Bin?"
						: $"Are you sure you want to move these {itemsToDelete.Count} items to the Recycle Bin?";

					if (MessageBox.Show(message, "Delete File", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) {
						try {
							foreach (var i in itemsToDelete) {
								ShellHelper.DeleteToRecycleBin(i.FullPath);
							}
							_ = LoadFilesAsync(CurrentPath);
						}
						catch (Exception ex) {
							_ = MessageBox.Show($"Error deleting: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
						}
					}
				}
			});
			PropertiesCommand = new RelayCommand(param => {
				if (param is FileItemViewModel item) {
					_ = ShellHelper.ShowFileProperties(item.FullPath);
				}
			});

			RefreshCommand = new RelayCommand(async _ => {
				IsRefreshing = true;
				await LoadFilesAsync(CurrentPath);
				await Task.Delay(500);
				IsRefreshing = false;
			}, _ => !IsSearchActive);
			TogglePathEditCommand = new RelayCommand(param => IsPathEditing = !IsPathEditing);
		}

		public string TabName {
			get => _tabName;
			set => SetProperty(ref _tabName, value);
		}

		public ImageSource? Icon {
			get => _icon;
			set => SetProperty(ref _icon, value);
		}

		public ObservableCollection<FileItemViewModel> Files {
			get => _files;
			set => SetProperty(ref _files, value);
		}

		public DirectoryItemViewModel? SelectedDirectory {
			get => _selectedDirectory;
			set => SetProperty(ref _selectedDirectory, value);
		}

		public string CurrentPath {
			get => _currentPath;
			set {
				if (SetProperty(ref _currentPath, value)) {
					if (!_isNavigating) {
						NavigateTo(value);
					}
					OnPropertyChanged(nameof(CanSortOrView));
				}
			}
		}

		public string SearchText {
			get => _searchText;
			set {
				if (SetProperty(ref _searchText, value)) {
					OnPropertyChanged(nameof(IsSearchActive));
					CommandManager.InvalidateRequerySuggested();
					
					_searchCts?.Cancel();
					if (string.IsNullOrWhiteSpace(value)) {
						FilterFiles();
					}
					else {
						_searchCts = new CancellationTokenSource();
						var token = _searchCts.Token;
						_ = Task.Delay(300, token).ContinueWith(t => {
							if (t.IsCanceled) return;
							Application.Current.Dispatcher.Invoke(() => {
								if (SearchText == value) _ = PerformSearchAsync(value);
							});
						}, token);
					}
				}
			}
		}

		public bool IsSearchActive => !string.IsNullOrWhiteSpace(SearchText);

		public string StatusText {
			get => _statusText;
			set => SetProperty(ref _statusText, value);
		}

		public int ItemCount {
			get => _itemCount;
			set => SetProperty(ref _itemCount, value);
		}

		public double ItemSize {
			get => _itemSize;
			set {
				if (SetProperty(ref _itemSize, value)) {
					AppSettings.Current.ItemSize = value;
					if (!string.IsNullOrEmpty(CurrentPath)) {
						if (!AppSettings.Current.FolderViewStates.TryGetValue(CurrentPath, out var state)) {
							state = new FolderViewState {
								SortProperty = _sortProperty,
								SortDirection = _sortDirection == ListSortDirection.Ascending ? "Ascending" : "Descending"
							};
							AppSettings.Current.FolderViewStates[CurrentPath] = state;
						}
						state.ItemSize = value;
					}
					OnPropertyChanged(nameof(IsDetailsView));
					foreach (var file in Files) {
						file.RefreshIcon((int)value);
					}
				}
			}
		}

		public bool IsDetailsView => ItemSize <= 20;

		public bool IsSearching {
			get => _isSearching;
			set {
				if (SetProperty(ref _isSearching, value)) {
					OnPropertyChanged(nameof(CanSortOrView));
					CommandManager.InvalidateRequerySuggested();
				}
			}
		}

		public bool CanSortOrView => !IsSearching && CurrentPath != "This PC";

		public ObservableCollection<PathSegmentViewModel> PathSegments {
			get => _pathSegments;
			set => SetProperty(ref _pathSegments, value);
		}

		public bool IsPathEditing {
			get => _isPathEditing;
			set {
				if (SetProperty(ref _isPathEditing, value)) {
					if (value) {
						AddressBarText = CurrentPath;
					}
				}
			}
		}

		public string AddressBarText {
			get => _addressBarText;
			set {
				if (SetProperty(ref _addressBarText, value)) {
					UpdateSuggestions();
				}
			}
		}

		public ObservableCollection<string> Suggestions {
			get => _suggestions;
			set {
				if (SetProperty(ref _suggestions, value)) {
					OnPropertyChanged(nameof(HasSuggestions));
				}
			}
		}

		public bool HasSuggestions => Suggestions.Count > 0;

		private void UpdateSuggestions() {
			if (!IsPathEditing || string.IsNullOrEmpty(AddressBarText)) {
				Suggestions.Clear();
				return;
			}

			try {
				string path = AddressBarText;
				string dir;
				string filter;

				if (path.EndsWith("\\")) {
					dir = path;
					filter = "";
				}
				else if (path.Length == 2 && path[1] == ':') {
					dir = path + "\\";
					filter = "";
				}
				else {
					dir = Path.GetDirectoryName(path) ?? "";
					filter = Path.GetFileName(path) ?? "";
				}

				if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) {
					var opts = new EnumerationOptions { IgnoreInaccessible = true };
					var dirs = Directory.EnumerateDirectories(dir, filter + "*", opts).Take(10);
					Suggestions = new ObservableCollection<string>(dirs);
				}
				else {
					Suggestions.Clear();
				}
			}
			catch {
				Suggestions.Clear();
			}
		}

		public void SortFiles(string? property) {
			if (string.IsNullOrEmpty(property)) return;

			if (_sortProperty == property) {
				_sortDirection = _sortDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
			}
			else {
				_sortProperty = property;
				_sortDirection = ListSortDirection.Ascending;
			}

			AppSettings.Current.SortProperty = _sortProperty;
			AppSettings.Current.SortDirection = _sortDirection == ListSortDirection.Ascending ? "Ascending" : "Descending";

			if (!string.IsNullOrEmpty(CurrentPath)) {
				if (!AppSettings.Current.FolderViewStates.TryGetValue(CurrentPath, out var state)) {
					state = new FolderViewState { ItemSize = _itemSize };
					AppSettings.Current.FolderViewStates[CurrentPath] = state;
				}
				state.SortProperty = _sortProperty;
				state.SortDirection = AppSettings.Current.SortDirection;
			}

			ApplySort();
		}

		private void ApplySort() {
			if (Files.Count == 0) return;

			Application.Current.Dispatcher.Invoke(() => {
				var view = CollectionViewSource.GetDefaultView(Files);
				view.SortDescriptions.Clear();

				if (IsDownloadsFolder(CurrentPath)) {
					if (view.GroupDescriptions.Count == 0) {
						view.GroupDescriptions.Add(new PropertyGroupDescription("DateGroupKey"));
					}
					view.SortDescriptions.Add(new SortDescription("DateGroupKey", ListSortDirection.Descending));
					view.SortDescriptions.Add(new SortDescription("IsFolder", ListSortDirection.Descending));

					if (_sortProperty != "Date modified") {
						view.SortDescriptions.Add(new SortDescription(_sortProperty, _sortDirection));
					}
					else {
						view.SortDescriptions.Add(new SortDescription("DateModified", _sortDirection));
					}
				}
				else {
					view.GroupDescriptions.Clear();
					view.SortDescriptions.Add(new SortDescription("IsFolder", ListSortDirection.Descending));
					view.SortDescriptions.Add(new SortDescription(_sortProperty, _sortDirection));
				}
			});
		}

		public void NavigateTo(string? path) {
			if (string.IsNullOrEmpty(path)) return;

			if (!string.IsNullOrEmpty(SearchText)) {
				SearchText = string.Empty;
			}

			if (path.Length == 2 && path[1] == ':') {
				path += "\\";
			}

			bool isThisPC = path == "This PC";
			if (!isThisPC && !Directory.Exists(path)) return;

			if (_currentPath == path && Files.Count > 0) return;

			if (!_isNavigating && !string.IsNullOrEmpty(_currentPath)) {
				_backHistory.Push(_currentPath);
				_forwardHistory.Clear();
			}

			_isNavigating = true;
			CurrentPath = path;
			AddressBarText = path;

			if (AppSettings.Current.FolderViewStates.TryGetValue(path, out var state)) {
				if (_itemSize != state.ItemSize) {
					_itemSize = state.ItemSize;
					OnPropertyChanged(nameof(ItemSize));
					OnPropertyChanged(nameof(IsDetailsView));
				}
				_sortProperty = state.SortProperty;
				_sortDirection = state.SortDirection == "Ascending" ? ListSortDirection.Ascending : ListSortDirection.Descending;
			}

			if (isThisPC) {
				TabName = "This PC";
				Icon = IconHelper.GetFolderIcon(Environment.GetFolderPath(Environment.SpecialFolder.MyComputer), false, 16);
			}
			else {
				TabName = new DirectoryInfo(path).Name;
				if (string.IsNullOrEmpty(TabName)) TabName = path;
				Icon = IconHelper.GetFolderIcon(path, false, 16);
			}

			UpdatePathSegments(path);
			IsPathEditing = false;
			_isNavigating = false;

			_ = LoadFilesAsync(path);
		}

		private void UpdatePathSegments(string path) {
			var segments = new List<PathSegmentViewModel>();

			if (path == "This PC") {
				segments.Add(new PathSegmentViewModel("This PC", "This PC", NavigateCommand));
			}
			else {
				var stack = new Stack<PathSegmentViewModel>();
				var dir = new DirectoryInfo(path);

				while (dir != null) {
					stack.Push(new PathSegmentViewModel(dir.Name.TrimEnd('\\'), dir.FullName, NavigateCommand));
					dir = dir.Parent;
				}

				segments.Add(new PathSegmentViewModel("This PC", "This PC", NavigateCommand));
				while (stack.Count > 0) {
					segments.Add(stack.Pop());
				}
			}

			PathSegments = new ObservableCollection<PathSegmentViewModel>(segments);
		}

		private void GoBack() {
			if (_backHistory.Count > 0) {
				_forwardHistory.Push(_currentPath);
				var path = _backHistory.Pop();
				_isNavigating = true;
				CurrentPath = path;
				TabName = path == "This PC" ? "This PC" : new DirectoryInfo(path).Name;
				if (string.IsNullOrEmpty(TabName)) TabName = path;
				Icon = IconHelper.GetFolderIcon(path == "This PC" ? Environment.GetFolderPath(Environment.SpecialFolder.MyComputer) : path, false, 16);
				_isNavigating = false;
				_ = LoadFilesAsync(path);
			}
		}

		private void GoForward() {
			if (_forwardHistory.Count > 0) {
				_backHistory.Push(_currentPath);
				var path = _forwardHistory.Pop();
				_isNavigating = true;
				CurrentPath = path;
				TabName = path == "This PC" ? "This PC" : new DirectoryInfo(path).Name;
				if (string.IsNullOrEmpty(TabName)) TabName = path;
				Icon = IconHelper.GetFolderIcon(path == "This PC" ? Environment.GetFolderPath(Environment.SpecialFolder.MyComputer) : path, false, 16);
				_isNavigating = false;
				_ = LoadFilesAsync(path);
			}
		}

		private void OpenItem(object? parameter) {
			if (parameter is FileItemViewModel fileItem) {
				if (fileItem.IsFolder) {
					NavigateTo(fileItem.FullPath);
				}
				else {
					try {
						_ = Process.Start(new ProcessStartInfo(fileItem.FullPath) { UseShellExecute = true });
					}
					catch (Exception ex) {
						_ = MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					}
				}
			}
		}

		private async Task LoadFilesAsync(string path) {
			if (path != CurrentPath) {
				Files.Clear();
				_allFiles.Clear();
			}
			StatusText = "Loading...";

			if (path == "This PC") {
				TabName = "This PC";
				await Task.Run(() => {
					var list = new List<FileItemViewModel>();

					foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady)) {
						list.Add(new DriveItemViewModel(drive));
					}

					Application.Current.Dispatcher.Invoke(() => {
						_allFiles = list;
						Files = new ObservableCollection<FileItemViewModel>(list);
						StatusText = $"{Files.Count} items";
						ItemCount = Files.Count;
					});
				});
				return;
			}

			try {
				var directoryInfo = new DirectoryInfo(path);
				var fileList = await Task.Run(() => {
					var list = new List<FileItemViewModel>();
					try {
						foreach (var dir in directoryInfo.GetDirectories()) {
							list.Add(new FolderItemViewModel(dir));
						}
						foreach (var file in directoryInfo.GetFiles()) {
							list.Add(new FileItemViewModel(file));
						}
					}
					catch { }
					return list;
				});

				var currentSize = (int)ItemSize;
				foreach (var item in fileList) {
					item.RefreshIcon(currentSize);
				}

				_allFiles = fileList;

				Application.Current.Dispatcher.Invoke(() => {
					var toRemove = Files.Where(x => !_allFiles.Contains(x)).ToList();
					foreach (var item in toRemove) _ = Files.Remove(item);

					var toAdd = _allFiles.Where(x => !Files.Contains(x)).ToList();
					foreach (var item in toAdd) Files.Add(item);

					ApplySort();

					ItemCount = Files.Count;
					StatusText = $"{ItemCount} items";
				});

				Application.Current.Dispatcher.Invoke(() => {
					foreach (var file in Files) {
						file.RefreshIcon((int)ItemSize);
					}
					ApplySort();
				});
			}
			catch (UnauthorizedAccessException) {
				StatusText = "Access Denied";
			}
			catch (Exception ex) {
				StatusText = $"Error: {ex.Message}";
			}
		}

		private bool IsDownloadsFolder(string path) {
			return path.Equals(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads", StringComparison.OrdinalIgnoreCase);
		}

		private async Task PerformSearchAsync(string query) {
			_searchCts?.Cancel();
			_searchCts = new CancellationTokenSource();
			var token = _searchCts.Token;

			StatusText = "Searching...";
			IsSearching = true;
			Files.Clear();

			try {
				await Task.Run(() => {
					var options = new EnumerationOptions {
						IgnoreInaccessible = true,
						RecurseSubdirectories = false,
						AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
						ReturnSpecialDirectories = false
					};

					var queue = new Queue<string>();
					if (_currentPath == "This PC") {
						foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady)) {
							queue.Enqueue(drive.RootDirectory.FullName);
						}
					}
					else {
						queue.Enqueue(_currentPath);
					}

					var batch = new List<FileItemViewModel>();
					int foundCount = 0;
					var lastFlush = DateTime.Now;

					while (queue.Count > 0) {
						if (token.IsCancellationRequested) return;
						if (AppSettings.Current.MaxSearchResults > 0 && foundCount >= AppSettings.Current.MaxSearchResults) break;

						string currentPath = queue.Dequeue();

						try {
							var dirInfo = new DirectoryInfo(currentPath);
							foreach (var item in dirInfo.EnumerateFileSystemInfos("*", options)) {
								if (token.IsCancellationRequested) return;

								if (item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) {
									if (item is DirectoryInfo d) batch.Add(new FolderItemViewModel(d));
									else if (item is FileInfo f) batch.Add(new FileItemViewModel(f));
									foundCount++;

									bool shouldFlush = false;
									if (foundCount <= 20) shouldFlush = true;
									else if ((DateTime.Now - lastFlush).TotalMilliseconds > 100 && batch.Count > 0) shouldFlush = true;
									else if (batch.Count >= 50) shouldFlush = true;

									if (shouldFlush) {
										var batchCopy = batch.ToList();
										batch.Clear();
										lastFlush = DateTime.Now;
										_ = (Application.Current?.Dispatcher.BeginInvoke(() => {
											foreach (var i in batchCopy) Files.Add(i);
											StatusText = $"Searching... Found {Files.Count} items";
											ItemCount = Files.Count;
										}));
									}

									if (AppSettings.Current.MaxSearchResults > 0 && foundCount >= AppSettings.Current.MaxSearchResults) break;
								}

								if (item is DirectoryInfo subDir) {
									if ((subDir.Attributes & FileAttributes.ReparsePoint) != FileAttributes.ReparsePoint) {
										queue.Enqueue(subDir.FullName);
									}
								}
							}
						}
						catch { }
					}

					if (batch.Count > 0) {
						_ = (Application.Current?.Dispatcher.BeginInvoke(() => {
							foreach (var i in batch) Files.Add(i);
							StatusText = $"Found {Files.Count} items";
							ItemCount = Files.Count;
						}));
					}
				}, token);
			}
			catch (TaskCanceledException) { }
			finally {
				if (!token.IsCancellationRequested) {
					IsSearching = false;
					Application.Current?.Dispatcher.Invoke(ApplySort);
					StatusText = $"Found {Files.Count} items";
					ItemCount = Files.Count;
				}
			}
		}

		private void FilterFiles() {
			Files = new ObservableCollection<FileItemViewModel>(_allFiles);
			ApplySort();
			ItemCount = Files.Count;
			StatusText = $"{ItemCount} items";
		}

		public void UpdateSelectionStatus(IList<FileItemViewModel> selectedItems) {
			_selectedItems = selectedItems.ToList();
			if (selectedItems.Count == 0) {
				StatusText = $"{ItemCount} items";
			}
			else {
				long totalSize = selectedItems.Sum(x => x.Size);
				StatusText = $"{ItemCount} items   |   {selectedItems.Count} item{(selectedItems.Count > 1 ? "s" : "")} selected   {FileItemViewModel.FormatSize(totalSize)}";
			}
		}
	}

	public class DirectoryItemViewModel : ViewModelBase {
		private bool _isExpanded;
		private bool _isSelected;
		private ObservableCollection<DirectoryItemViewModel> _subDirectories;
		private bool _hasDummyChild;

		public string FullPath { get; }
		public string Name { get; }
		public ImageSource? Icon { get; }
		public bool IsDrive { get; }
		public double PercentUsed { get; }

		public DirectoryItemViewModel(string fullPath, string? name = null, string? label = null, bool isDrive = false) {
			FullPath = fullPath;
			if (!string.IsNullOrEmpty(label)) {
				Name = !string.IsNullOrEmpty(name) ? $"{label} ({name})" : label;
			}
			else {
				if (fullPath == "This PC") Name = "This PC";
				else Name = name ?? new DirectoryInfo(fullPath).Name;
			}

			if (fullPath == "This PC") {
				Icon = IconHelper.GetFolderIcon(Environment.GetFolderPath(Environment.SpecialFolder.MyComputer), false, 16);
			}
			else {
				Icon = IconHelper.GetFolderIcon(fullPath, false, 16);
			}

			IsDrive = isDrive;

			if (IsDrive) {
				try {
					var di = new DriveInfo(fullPath);
					if (di.IsReady) {
						long total = di.TotalSize;
						long free = di.TotalFreeSpace;
						PercentUsed = (double)(total - free) / total * 100.0;
					}
				}
				catch { }
			}

			_subDirectories = [];

			if (fullPath != "This PC" && HasSubDirectories(fullPath)) {
				_subDirectories.Add(new DirectoryItemViewModel("dummy"));
				_hasDummyChild = true;
			}
		}

		public ObservableCollection<DirectoryItemViewModel> SubDirectories {
			get => _subDirectories;
			set => SetProperty(ref _subDirectories, value);
		}

		public bool IsExpanded {
			get => _isExpanded;
			set {
				if (SetProperty(ref _isExpanded, value)) {
					if (value && _hasDummyChild) {
						SubDirectories.Clear();
						_hasDummyChild = false;
						_ = LoadSubDirectoriesAsync();
					}
				}
			}
		}

		public bool IsSelected {
			get => _isSelected;
			set => SetProperty(ref _isSelected, value);
		}

		private bool HasSubDirectories(string path) {
			try {
				return Directory.EnumerateDirectories(path).Any();
			}
			catch {
				return false;
			}
		}

		private async Task LoadSubDirectoriesAsync() {
			try {
				var dirs = await Task.Run(() => {
					var list = new List<DirectoryItemViewModel>();
					var info = new DirectoryInfo(FullPath);
					foreach (var dir in info.GetDirectories()) {
						if ((dir.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden) {
							list.Add(new DirectoryItemViewModel(dir.FullName));
						}
					}
					return list;
				});

				SubDirectories = new ObservableCollection<DirectoryItemViewModel>(dirs);
			}
			catch { }
		}
	}

	public class FileItemViewModel : ViewModelBase {
		private static readonly Dictionary<string, string> _typeCache = new(StringComparer.OrdinalIgnoreCase);
		private ImageSource? _icon;
		private bool _iconLoaded;
		private ObservableCollection<ShellContextMenu.ShellMenuItem>? _shellMenuItems;
		private bool _isRenaming;
		private string _renameText = string.Empty;

		public string Name { get; }
		public string FullPath { get; }
		public long Size { get; protected set; }
		public DateTime DateModified { get; }

		public bool IsRenaming {
			get => _isRenaming;
			set => SetProperty(ref _isRenaming, value);
		}

		public string RenameText {
			get => _renameText;
			set => SetProperty(ref _renameText, value);
		}

		public ObservableCollection<ShellContextMenu.ShellMenuItem> ShellMenuItems {
			get {
				if (_shellMenuItems == null) {
					_shellMenuItems = [];
				}
				return _shellMenuItems;
			}
		}

		public ImageSource? Icon {
			get {
				if (!_iconLoaded) {
					_icon = LoadIcon(32);
					_iconLoaded = true;
				}
				return _icon;
			}
		}

		public void RefreshIcon(int size) {
			_icon = LoadIcon(size);
			OnPropertyChanged(nameof(Icon));
		}

		public virtual bool IsFolder => false;
		public virtual bool IsDrive => false;
		public virtual double PercentUsed => 0;
		public virtual string FreeSpaceText => "";

		public virtual string DisplaySize => FormatSize(Size);
		public string DateModifiedString => DateModified.ToString("dd/MM/yyyy HH:mm");

		public DateTime DateGroupKey {
			get {
				var now = DateTime.Now;
				var date = DateModified.Date;
				var today = now.Date;
				var yesterday = today.AddDays(-1);

				if (date == today) return today;
				if (date == yesterday) return yesterday;

				var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
				if (date >= startOfWeek) return startOfWeek;

				var startOfLastWeek = startOfWeek.AddDays(-7);
				if (date >= startOfLastWeek) return startOfLastWeek;

				var startOfMonth = new DateTime(today.Year, today.Month, 1);
				if (date >= startOfMonth) return startOfMonth;

				var startOfLastMonth = startOfMonth.AddMonths(-1);
				if (date >= startOfLastMonth) return startOfLastMonth;

				var startOfYear = new DateTime(today.Year, 1, 1);
				if (date >= startOfYear) return startOfYear;

				return DateTime.MinValue;
			}
		}

		public virtual string Type {
			get {
				try {
					string ext = Path.GetExtension(FullPath);
					if (string.IsNullOrEmpty(ext)) return "File";

					string key = ext.ToLower();
					lock (_typeCache) {
						if (!_typeCache.TryGetValue(key, out var typeStr)) {
							typeStr = IconHelper.GetFileType(ext);
							if (string.IsNullOrEmpty(typeStr)) {
								typeStr = ext.TrimStart('.').ToUpper() + " File";
							}
							_typeCache[key] = typeStr;
						}
						return typeStr;
					}
				}
				catch { return "File"; }
			}
		}

		public override bool Equals(object? obj) {
			if (obj is FileItemViewModel other) {
				return FullPath.Equals(other.FullPath, StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}

		public override int GetHashCode() {
			return FullPath.GetHashCode(StringComparison.OrdinalIgnoreCase);
		}

		public FileItemViewModel(FileInfo info) {
			Name = info.Name;
			FullPath = info.FullName;
			Size = info.Length;
			DateModified = info.LastWriteTime;
		}

		protected FileItemViewModel(string name, string fullPath, DateTime dateModified) {
			Name = name;
			FullPath = fullPath;
			DateModified = dateModified;
			Size = 0;
		}

		public void LoadShellMenu() {
			if (_shellMenuItems != null && _shellMenuItems.Count > 0) return;

			try {
				var scm = new ShellContextMenu();
				var items = scm.GetContextMenuItems([new FileInfo(FullPath)]);
				foreach (var item in items) {
					ShellMenuItems.Add(item);
				}
			}
			catch { }
		}

		protected virtual ImageSource? LoadIcon(int size) {
			if (size > 20) {
				string ext = Path.GetExtension(FullPath).ToLower();
				string[] mediaExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webp"];
				if (mediaExtensions.Contains(ext)) {
					var thumb = IconHelper.GetThumbnail(FullPath, size);
					if (thumb != null) return thumb;
				}
			}
			return IconHelper.GetFileIcon(FullPath, size);
		}

		public static string FormatSize(long bytes) {
			string[] sizes = ["B", "KB", "MB", "GB", "TB"];
			double len = bytes;
			int order = 0;
			while (len >= 1024 && order < sizes.Length - 1) {
				order++;
				len /= 1024;
			}
			return $"{len:0.##} {sizes[order]}";
		}
	}

	public class FolderItemViewModel : FileItemViewModel {
		public override bool IsFolder => true;
		public override string Type => "File folder";
		public override string DisplaySize => "";

		public FolderItemViewModel(DirectoryInfo info)
			: base(info.Name, info.FullName, info.LastWriteTime) {
		}

		protected override ImageSource? LoadIcon(int size) {
			return IconHelper.GetFolderIcon(FullPath, false, size);
		}
	}

	public class DriveItemViewModel : FileItemViewModel {
		public override bool IsFolder => true;
		public override bool IsDrive => true;
		public override string Type => "Local Disk";

		public override double PercentUsed { get; }
		public override string FreeSpaceText { get; }

		public DriveItemViewModel(DriveInfo info)
			: base($"{info.VolumeLabel} ({info.Name})", info.RootDirectory.FullName, DateTime.Now) {

			if (info.IsReady) {
				long total = info.TotalSize;
				long free = info.TotalFreeSpace;
				Size = total;
				double used = total - free;
				PercentUsed = used / total * 100.0;
				FreeSpaceText = $"{FormatSize(free)} free of {FormatSize(total)}";
			}
			else {
				FreeSpaceText = "";
			}
		}

		protected override ImageSource? LoadIcon(int size) {
			return IconHelper.GetFolderIcon(FullPath, false, size);
		}
	}

	public abstract class ViewModelBase : INotifyPropertyChanged {
		public event PropertyChangedEventHandler? PropertyChanged;

		protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		protected bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null) {
			if (EqualityComparer<T>.Default.Equals(field, value)) return false;
			field = value;
			OnPropertyChanged(propertyName);
			return true;
		}
	}

	public class RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null) : ICommand {
		private readonly Action<object?> _execute = execute;
		private readonly Predicate<object?>? _canExecute = canExecute;

		public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
		public void Execute(object? parameter) => _execute(parameter);
		public event EventHandler? CanExecuteChanged {
			add { CommandManager.RequerySuggested += value; }
			remove { CommandManager.RequerySuggested -= value; }
		}
	}

	#endregion

	#region Interop / Helpers

	public static class WindowAccentCompositor {
		[DllImport("user32.dll")]
		internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

		[StructLayout(LayoutKind.Sequential)]
		internal struct WindowCompositionAttributeData {
			public WindowCompositionAttribute Attribute;
			public IntPtr Data;
			public int SizeOfData;
		}

		internal enum WindowCompositionAttribute {
			WCA_ACCENT_POLICY = 19
		}

		internal enum AccentState {
			ACCENT_DISABLED = 0,
			ACCENT_ENABLE_GRADIENT = 1,
			ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
			ACCENT_ENABLE_BLURBEHIND = 3,
			ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
			ACCENT_INVALID_STATE = 5
		}

		[StructLayout(LayoutKind.Sequential)]
		internal struct AccentPolicy {
			public AccentState AccentState;
			public int AccentFlags;
			public int GradientColor;
			public int AnimationId;
		}

		public static void EnableBlur(IntPtr hwnd, int opacity = 64, uint color = 0x202020) {
			var accent = new AccentPolicy {
				AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
				GradientColor = unchecked(0x40202020)
			};
			SetAccentPolicy(hwnd, accent);
		}

		public static void DisableBlur(IntPtr hwnd) {
			var accent = new AccentPolicy {
				AccentState = AccentState.ACCENT_DISABLED
			};
			SetAccentPolicy(hwnd, accent);
		}

		private static void SetAccentPolicy(IntPtr hwnd, AccentPolicy accent) {
			var accentStructSize = Marshal.SizeOf(accent);
			var accentPtr = Marshal.AllocHGlobal(accentStructSize);
			Marshal.StructureToPtr(accent, accentPtr, false);

			var data = new WindowCompositionAttributeData {
				Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
				SizeOfData = accentStructSize,
				Data = accentPtr
			};

			_ = SetWindowCompositionAttribute(hwnd, ref data);
			Marshal.FreeHGlobal(accentPtr);
		}
	}

	public static class IconHelper {
		private static readonly Dictionary<string, ImageSource> _iconCache = [];
		private static readonly Lock _cacheLock = new();
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
				return GetIcon(path, false, false, false);
			}

			lock (_cacheLock) {
				if (_iconCache.TryGetValue(ext, out var cachedIcon)) return cachedIcon;
			}

			var icon = GetIcon(ext, false, false, true);

			if (icon != null) {
				lock (_cacheLock) {
					if (!_iconCache.ContainsKey(ext)) _iconCache[ext] = icon;
				}
			}
			return icon;
		}

		public static ImageSource? GetFolderIcon(string path, bool open) {
			if (path.Length <= 3 || _specialPaths.Contains(path)) {
				return GetIcon(path, true, open, false);
			}

			string key = open ? "folder_open" : "folder_closed";

			lock (_cacheLock) {
				if (_iconCache.TryGetValue(key, out var cachedIcon)) return cachedIcon;
			}

			var icon = GetIcon(path, true, open, true);

			if (icon != null) {
				lock (_cacheLock) {
					if (!_iconCache.ContainsKey(key)) _iconCache[key] = icon;
				}
			}
			return icon;
		}

		private static BitmapSource? GetIcon(string path, bool isFolder, bool isOpen, bool useFileAttributes) {
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

		public static ImageSource? GetFileIcon(string path, int size) {
			string ext = Path.GetExtension(path).ToLower();
			if (string.IsNullOrEmpty(ext)) ext = ".file";

			if (ext == ".exe" || ext == ".lnk" || ext == ".ico") {
				return GetIcon(path, false, false, false, size);
			}

			string cacheKey = $"{ext}_{size}";
			lock (_cacheLock) {
				if (_iconCache.TryGetValue(cacheKey, out var cachedIcon)) return cachedIcon;
			}

			var icon = GetIcon(ext, false, false, true, size);

			if (icon != null) {
				lock (_cacheLock) {
					if (!_iconCache.ContainsKey(cacheKey)) _iconCache[cacheKey] = icon;
				}
			}
			return icon;
		}

		public static ImageSource? GetFolderIcon(string path, bool open, int size) {
			string key = (open ? "folder_open" : "folder_closed") + $"_{size}_{path.GetHashCode()}";

			lock (_cacheLock) {
				if (_iconCache.TryGetValue(key, out var cachedIcon)) return cachedIcon;
			}

			var icon = GetIcon(path, true, open, false, size);

			if (icon != null) {
				lock (_cacheLock) {
					if (!_iconCache.ContainsKey(key)) _iconCache[key] = icon;
				}
			}
			return icon;
		}

		private static BitmapSource? GetIcon(string path, bool isFolder, bool isOpen, bool useFileAttributes, int size) {
			if (size <= 16) {
				var flags = SHGFI_ICON | SHGFI_SMALLICON;
				if (useFileAttributes) flags |= SHGFI_USEFILEATTRIBUTES;
				var attributes = isFolder ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
				SHFILEINFO shinfo = new();
				IntPtr hImg = SHGetFileInfo(path, attributes, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
				if (hImg == IntPtr.Zero) return null;
				var icon = Imaging.CreateBitmapSourceFromHIcon(shinfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
				_ = DestroyIcon(shinfo.hIcon);
				icon.Freeze();
				return icon;
			}

			int imageListType = size > 48 ? SHIL_JUMBO : SHIL_EXTRALARGE;

			var fileInfo = new SHFILEINFO();
			uint flags2 = SHGFI_SYSICONINDEX | SHGFI_USEFILEATTRIBUTES;
			if (!useFileAttributes) flags2 &= ~SHGFI_USEFILEATTRIBUTES;

			uint attr = isFolder ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;

			IntPtr result = SHGetFileInfo(path, attr, ref fileInfo, (uint)Marshal.SizeOf(fileInfo), flags2);

			if (result == IntPtr.Zero) return null;

			var iidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
			int hres = SHGetImageList(imageListType, ref iidImageList, out IImageList? iml);

			if (hres == 0 && iml != null) {
				IntPtr hIcon = IntPtr.Zero;
				hres = iml.GetIcon(fileInfo.iIcon, ILD_TRANSPARENT, out hIcon);
				if (hres == 0 && hIcon != IntPtr.Zero) {
					var icon = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
					_ = DestroyIcon(hIcon);
					icon.Freeze();
					return icon;
				}
			}

			var flags3 = SHGFI_ICON | SHGFI_LARGEICON;
			if (useFileAttributes) flags3 |= SHGFI_USEFILEATTRIBUTES;
			SHFILEINFO shinfo3 = new();
			IntPtr hImg3 = SHGetFileInfo(path, attr, ref shinfo3, (uint)Marshal.SizeOf(shinfo3), flags3);
			if (hImg3 != IntPtr.Zero) {
				var icon = Imaging.CreateBitmapSourceFromHIcon(shinfo3.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
				_ = DestroyIcon(shinfo3.hIcon);
				icon.Freeze();
				return icon;
			}

			return null;
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

	#endregion

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
									list.Add((path, name ?? new DirectoryInfo(path).Name));
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

	public class BindingProxy : Freezable {
		protected override Freezable CreateInstanceCore() => new BindingProxy();

		public object Data {
			get => GetValue(DataProperty);
			set => SetValue(DataProperty, value);
		}

		public static readonly DependencyProperty DataProperty =
			DependencyProperty.Register("Data", typeof(object), typeof(BindingProxy), new PropertyMetadata(null));
	}

	public class PinnedToVisibilityConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			if (value is string path) {
				bool isPinned = AppSettings.Current.PinnedFolders.Any(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
				bool invert = parameter?.ToString() == "Invert";
				return (isPinned ^ invert) ? Visibility.Visible : Visibility.Collapsed;
			}
			return Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			throw new NotImplementedException();
		}
	}

	public class NullToVisibilityConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			return value == null ? Visibility.Collapsed : Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			throw new NotImplementedException();
		}
	}

	public class CountToBoolConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			if (value is int count) return count > 0;
			return false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			throw new NotImplementedException();
		}
	}

	public class DateToGroupHeaderConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			if (value is DateTime date) {
				var now = DateTime.Now;
				var today = now.Date;
				var yesterday = today.AddDays(-1);

				if (date.Date == today) return "Today";
				if (date.Date == yesterday) return "Yesterday";

				var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
				if (date.Date >= startOfWeek) return "Earlier this week";

				var startOfLastWeek = startOfWeek.AddDays(-7);
				if (date.Date >= startOfLastWeek) return "Last week";

				var startOfMonth = new DateTime(today.Year, today.Month, 1);
				if (date.Date >= startOfMonth) return "Earlier this month";

				var startOfLastMonth = startOfMonth.AddMonths(-1);
				if (date.Date >= startOfLastMonth) return "Last month";

				var startOfYear = new DateTime(today.Year, 1, 1);
				if (date.Date >= startOfYear) return "Earlier this year";

				return "A long time ago";
			}
			return value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			throw new NotImplementedException();
		}
	}

	public class DragAdorner : Adorner {
		private readonly Brush _visualBrush;
		private Point _currentPosition;
		private readonly Point _offset;
		private readonly Size _renderSize;

		public DragAdorner(UIElement adornedElement, UIElement dragElement, Point offset) : base(adornedElement) {
			_visualBrush = new VisualBrush(dragElement) { Opacity = 0.7, Stretch = Stretch.None };
			_offset = offset;
			_renderSize = dragElement.RenderSize;
			IsHitTestVisible = false;
		}

		public void UpdatePosition(Point position) {
			_currentPosition = position;
			if (Parent is AdornerLayer layer) {
				layer.Update(this.AdornedElement);
			}
		}

		protected override void OnRender(DrawingContext drawingContext) {
			drawingContext.DrawRectangle(_visualBrush, null, new Rect(_currentPosition.X - _offset.X, _currentPosition.Y - _offset.Y, _renderSize.Width, _renderSize.Height));
		}
	}

	public class UniversalBoolToVisibilityConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			bool bVal = false;
			if (value is bool b) bVal = b;

			if (parameter?.ToString() == "Invert") bVal = !bVal;

			return bVal ? Visibility.Visible : Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			if (value is Visibility v) {
				bool bVal = v == Visibility.Visible;
				if (parameter?.ToString() == "Invert") bVal = !bVal;
				return bVal;
			}
			return false;
		}
	}
}
