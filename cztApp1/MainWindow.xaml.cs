using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using cztApp1.Models;
using cztApp1.Services;
using cztApp1.Views;

namespace cztApp1
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<string> _undoStack = new();
        private readonly ObservableCollection<string> _redoStack = new();
        private const int MaxHistory = 20;

        private const string SpatialDataPath = @"D:\geomatics_task\地理信息工程及应用\2322050202于景赫-12组-长株潭地质灾害（土壤植被）\数据\空间数据";
        private const string AttributeDataPath = @"D:\geomatics_task\地理信息工程及应用\2322050202于景赫-12组-长株潭地质灾害（土壤植被）\数据\属性数据";
        private string _currentRootPath = SpatialDataPath;

        private MapLayerService _mapLayerService = null!;

        public MainWindow()
        {
            InitializeComponent();
            Resources["SysFolderIcon"] = SystemIconProvider.FolderIcon;
            Resources["SysFileIcon"] = SystemIconProvider.FileIcon;
            UpdateUndoRedoState();
            LoadDirectoryTree(SpatialDataPath);

            // Initialize map layer service
            _mapLayerService = new MapLayerService(MapViewControl);
            _mapLayerService.LayersChanged += OnLayersChanged;
            LayerTreeView.ItemsSource = _mapLayerService.Layers;
            SetupLayerTreeViewEvents();



            // Hook up tree double-click
            DataTree.MouseDoubleClick += DataTree_MouseDoubleClick;

            StateChanged += (s, e) =>
            {
                MaxBtn.Content = WindowState == WindowState.Maximized ? "" : "";
                MaxBtn.ToolTip = WindowState == WindowState.Maximized ? "还原" : "最大化";
            };

            // 最大化时避开任务栏
            SourceInitialized += (_, _) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                var source = HwndSource.FromHwnd(hwnd);
                source?.AddHook(WndProc);
            };
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_GETMINMAXINFO = 0x0024;
            if (msg == WM_GETMINMAXINFO)
            {
                var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                if (monitor != IntPtr.Zero)
                {
                    var monitorInfo = new MONITORINFO();
                    monitorInfo.cbSize = Marshal.SizeOf<MONITORINFO>();
                    GetMonitorInfo(monitor, ref monitorInfo);
                    mmi.ptMaxPosition.X = monitorInfo.rcWork.Left - monitorInfo.rcMonitor.Left;
                    mmi.ptMaxPosition.Y = monitorInfo.rcWork.Top - monitorInfo.rcMonitor.Top;
                    mmi.ptMaxSize.X = monitorInfo.rcWork.Right - monitorInfo.rcWork.Left;
                    mmi.ptMaxSize.Y = monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top;
                    Marshal.StructureToPtr(mmi, lParam, true);
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private void OnLayersChanged()
        {
            // Update status bar
            Dispatcher.Invoke(() =>
            {
                var count = _mapLayerService.Layers.Count;
                if (StatusBar1 != null)
                    StatusBar1.Text = count > 0 ? $"已加载 {count} 个图层" : "就绪";
            });
        }

        private async void ClearAllLayers_Click(object sender, RoutedEventArgs e)
        {
            await _mapLayerService.ClearAllAsync();
            RecordOperation("清空所有图层");
        }

        #region 面板事件

        private void DataPanel_GotFocus(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border)
                border.Focus();
        }

        private void LayerPanel_GotFocus(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border)
                border.Focus();
        }

        private void SymbolPanel_GotFocus(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border)
                border.Focus();
        }

        private void SymbolOptions_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            var close = new MenuItem { Header = "关闭符号系统" };
            close.Click += (_, _) => SymbolPanelHost.Visibility = Visibility.Collapsed;
            menu.Items.Add(close);
            menu.IsOpen = true;
        }

        private void SymbolPanelClose_Click(object sender, RoutedEventArgs e)
        {
            SymbolPanelHost.Visibility = Visibility.Collapsed;
        }

        private void LayerOptions_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            var clear = new MenuItem { Header = "清空所有图层" };
            clear.Click += async (_, _) => await _mapLayerService.ClearAllAsync();
            menu.Items.Add(clear);
            menu.IsOpen = true;
        }

        private void LayerAutoHide_Click(object sender, RoutedEventArgs e)
        {
            // 简单切换：折叠面板（实际可扩展为自动隐藏功能）
        }

        private void LayerPanelClose_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                var border = FindParent<Border>(btn);
                if (border != null) border.Visibility = Visibility.Collapsed;
            }
        }

        #region 图层树事件

        private TreeViewItem? _dragItem;
        private MapLayer? _dragLayer;

        private void SetupLayerTreeViewEvents()
        {
            LayerTreeView.MouseDoubleClick += (_, e) =>
            {
                var item = FindParent<TreeViewItem>((DependencyObject)e.OriginalSource);
                if (item?.DataContext is MapLayer layer)
                    _ = _mapLayerService.ZoomToLayerAsync(layer);
            };

            // 右键菜单
            LayerTreeView.PreviewMouseRightButtonDown += (_, e) =>
            {
                var item = FindParent<TreeViewItem>((DependencyObject)e.OriginalSource);
                if (item?.DataContext is MapLayer layer)
                {
                    item.IsSelected = true;
                    item.Focus();
                    ShowLayerContextMenu(layer);
                }
            };

            // 拖拽排序
            LayerTreeView.PreviewMouseLeftButtonDown += (_, e) =>
            {
                _dragItem = FindParent<TreeViewItem>((DependencyObject)e.OriginalSource);
                _dragLayer = _dragItem?.DataContext as MapLayer;
            };
            LayerTreeView.PreviewMouseMove += (_, e) =>
            {
                if (_dragLayer == null || e.LeftButton != MouseButtonState.Pressed) return;
                DragDrop.DoDragDrop(_dragItem!, _dragLayer, DragDropEffects.Move);
                _dragLayer = null; _dragItem = null;
            };
            LayerTreeView.Drop += (_, e) =>
            {
                if (!e.Data.GetDataPresent(typeof(MapLayer))) return;
                var dragged = e.Data.GetData(typeof(MapLayer)) as MapLayer;
                var targetItem = FindParent<TreeViewItem>((DependencyObject)e.OriginalSource);
                var target = targetItem?.DataContext as MapLayer;
                if (target != null && target != dragged)
                {
                    var idx = _mapLayerService.Layers.IndexOf(target);
                    _mapLayerService.MoveLayerTo(dragged!, idx);
                }
            };
        }

        private void ShowLayerContextMenu(MapLayer layer)
        {
            var ctx = new ContextMenu();
            var zoom = new MenuItem { Header = "缩放至图层" };
            zoom.Click += async (_, _) => await _mapLayerService.ZoomToLayerAsync(layer);
            var symb = new MenuItem { Header = "符号系统" };
            symb.Click += (_, _) => ShowSymbolEditor(layer);
            var up = new MenuItem { Header = "上移" };
            up.Click += (_, _) => _mapLayerService.MoveLayerUp(layer);
            var down = new MenuItem { Header = "下移" };
            down.Click += (_, _) => _mapLayerService.MoveLayerDown(layer);
            var remove = new MenuItem { Header = "移除图层" };
            remove.Click += (_, _) =>
            {
                _ = _mapLayerService.RemoveLayerAsync(layer);
                RecordOperation($"移除图层: {layer.Name}");
            };
            ctx.Items.Add(zoom);
            ctx.Items.Add(symb);
            ctx.Items.Add(new Separator());
            ctx.Items.Add(up);
            ctx.Items.Add(down);
            ctx.Items.Add(new Separator());
            ctx.Items.Add(remove);
            ctx.IsOpen = true;
        }

        private void SymbolItem_Click(object sender, MouseButtonEventArgs e)
        {
            var item = FindParent<TreeViewItem>((DependencyObject)e.OriginalSource);
            if (item?.DataContext is SymbolItem)
            {
                // 找到所属的 MapLayer
                var layerItem = FindParent<TreeViewItem>(item);
                if (layerItem?.DataContext is MapLayer layer)
                    ShowSymbolEditor(layer);
            }
        }

        private void LayerCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is MapLayer layer)
            {
                _mapLayerService.SetLayerVisibility(layer, cb.IsChecked == true);
            }
        }

        #endregion

        private static T? FindParent<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null) { if (d is T t) return t; d = VisualTreeHelper.GetParent(d); }
            return null;
        }

        #region 符号系统面板

        private MapLayer? _currentSymbolLayer;

        private void ShowSymbolEditor(MapLayer layer)
        {
            _currentSymbolLayer = layer;
            SymbolPanelHost.Visibility = Visibility.Visible;
            SymbolEditorHost.Children.Clear();

            var isVector = layer.Type == SpatialDataType.Vector;
            SymbolPanelTitle.Text = $"符号系统 — {layer.Name}";

            if (isVector && layer.VectorSymbol != null)
            {
                BuildVectorSymbolEditor(layer.VectorSymbol);
            }
            else if (!isVector && layer.RasterSymbol != null)
            {
                BuildRasterSymbolEditor(layer.RasterSymbol);
            }
        }

        private void BuildVectorSymbolEditor(VectorSymbol vs)
        {
            var sp = new StackPanel();
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int row = 0;

            // 填充颜色
            AddColorRow(grid, ref row, "填充颜色", vs.FillColor, c => vs.FillColor = c);
            // 填充透明度
            AddSliderRow(grid, ref row, "填充透明度", vs.FillOpacity, 0, 1, v => vs.FillOpacity = v);
            // 轮廓颜色
            AddColorRow(grid, ref row, "轮廓颜色", vs.StrokeColor, c => vs.StrokeColor = c);
            // 轮廓宽度
            AddNumRow(grid, ref row, "轮廓宽度", vs.StrokeWidth, v => vs.StrokeWidth = v);
            // 点大小
            AddNumRow(grid, ref row, "点大小", vs.PointSize, v => vs.PointSize = v);

            sp.Children.Add(grid);

            // 预览
            var preview = new Border
            {
                Width = 60, Height = 40, Margin = new Thickness(0, 6, 0, 0),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(vs.StrokeColor)!),
                BorderThickness = new Thickness(vs.StrokeWidth),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(vs.FillColor)!) { Opacity = vs.FillOpacity }
            };
            vs.PropertyChanged += (_, _) =>
            {
                preview.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(vs.StrokeColor)!);
                preview.BorderThickness = new Thickness(vs.StrokeWidth);
                preview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(vs.FillColor)!) { Opacity = vs.FillOpacity };
            };
            sp.Children.Add(preview);
            SymbolEditorHost.Children.Add(sp);
        }

        private void BuildRasterSymbolEditor(RasterSymbol rs)
        {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = "色带设置（暂用灰度默认）",
                FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                Margin = new Thickness(0, 4, 0, 8)
            });
            // 简单预览：渐变条
            var bar = new Border { Height = 16, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 0, 4) };
            var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            foreach (var stop in rs.Stops)
            {
                gradient.GradientStops.Add(new GradientStop(
                    (Color)ColorConverter.ConvertFromString(stop.Color)!,
                    stop.Value / 255.0));
            }
            bar.Background = gradient;
            sp.Children.Add(bar);
            SymbolEditorHost.Children.Add(sp);
        }

        private void AddColorRow(Grid grid, ref int row, string label, string initialColor, Action<string> setter)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
            var lbl = new TextBlock
            {
                Text = label, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(lbl, row); Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            // 颜色预览块
            var swatch = new Border
            {
                Width = 20, Height = 16, CornerRadius = new CornerRadius(2),
                Margin = new Thickness(4, 0, 4, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(initialColor)!),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0)),
                BorderThickness = new Thickness(1)
            };
            Grid.SetRow(swatch, row); Grid.SetColumn(swatch, 1);
            grid.Children.Add(swatch);

            var tb = new TextBox
            {
                Text = initialColor, FontSize = 10, Width = 66,
                VerticalAlignment = VerticalAlignment.Center
            };
            tb.LostFocus += (_, _) =>
            {
                try
                {
                    var c = (Color)ColorConverter.ConvertFromString(tb.Text);
                    swatch.Background = new SolidColorBrush(c);
                    setter(tb.Text);
                }
                catch { tb.Text = initialColor; }
            };
            Grid.SetRow(tb, row); Grid.SetColumn(tb, 2);
            grid.Children.Add(tb);
            row++;
        }

        private void AddSliderRow(Grid grid, ref int row, string label, double initialValue,
                                   double min, double max, Action<double> setter)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
            var lbl = new TextBlock
            {
                Text = label, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(lbl, row); Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            var slider = new Slider
            {
                Minimum = min, Maximum = max, Value = initialValue,
                SmallChange = 0.05, Width = 50, VerticalAlignment = VerticalAlignment.Center
            };
            var valText = new TextBlock
            {
                Text = initialValue.ToString("F2"), FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0)
            };
            slider.ValueChanged += (_, _) =>
            {
                valText.Text = slider.Value.ToString("F2");
                setter(slider.Value);
            };
            var hStack = new StackPanel { Orientation = Orientation.Horizontal };
            hStack.Children.Add(slider);
            hStack.Children.Add(valText);
            Grid.SetRow(hStack, row); Grid.SetColumn(hStack, 2);
            grid.Children.Add(hStack);
            row++;
        }

        private void AddNumRow(Grid grid, ref int row, string label, double initialValue, Action<double> setter)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
            var lbl = new TextBlock
            {
                Text = label, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(lbl, row); Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            var tb = new TextBox
            {
                Text = initialValue.ToString("F1"), FontSize = 10, Width = 50,
                VerticalAlignment = VerticalAlignment.Center
            };
            tb.LostFocus += (_, _) =>
            {
                if (double.TryParse(tb.Text, out var v)) setter(v);
                else tb.Text = initialValue.ToString("F1");
            };
            Grid.SetRow(tb, row); Grid.SetColumn(tb, 2);
            grid.Children.Add(tb);
            row++;
        }

        #endregion

        #endregion

        private void UpdateUndoRedoState()
        {
            UndoBtn.IsEnabled = _undoStack.Count > 0;
            UndoDropBtn.IsEnabled = _undoStack.Count > 0;
            RedoBtn.IsEnabled = _redoStack.Count > 0;
            RedoDropBtn.IsEnabled = _redoStack.Count > 0;
        }

        public void RecordOperation(string name)
        {
            _undoStack.Insert(0, name);
            if (_undoStack.Count > MaxHistory)
                _undoStack.RemoveAt(_undoStack.Count - 1);
            _redoStack.Clear();
            UpdateUndoRedoState();
        }

        #region 标题栏事件

        private void TitleBar_Drag(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) { Maximize_Click(sender, e); return; }
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal : WindowState.Maximized;

        private void Close_Click(object sender, RoutedEventArgs e)
            => Close();

        #endregion

        #region 撤销 / 重做

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (_undoStack.Count == 0) return;
            var op = _undoStack[0];
            _undoStack.RemoveAt(0);
            _redoStack.Insert(0, op);
            UpdateUndoRedoState();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (_redoStack.Count == 0) return;
            var op = _redoStack[0];
            _redoStack.RemoveAt(0);
            _undoStack.Insert(0, op);
            UpdateUndoRedoState();
        }

        private void UndoHistory_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            if (_undoStack.Count == 0)
            {
                menu.Items.Add(new MenuItem { Header = "（无可撤销操作）", IsEnabled = false });
            }
            else
            {
                foreach (var op in _undoStack)
                {
                    var item = new MenuItem { Header = $"↩ {op}" };
                    item.Click += (s, _) =>
                    {
                        _undoStack.Remove(op);
                        _redoStack.Insert(0, op);
                        UpdateUndoRedoState();
                    };
                    menu.Items.Add(item);
                }
            }
            menu.IsOpen = true;
        }

        private void RedoHistory_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            if (_redoStack.Count == 0)
            {
                menu.Items.Add(new MenuItem { Header = "（无可重做操作）", IsEnabled = false });
            }
            else
            {
                foreach (var op in _redoStack)
                {
                    var item = new MenuItem { Header = $"↪ {op}" };
                    item.Click += (s, _) =>
                    {
                        _redoStack.Remove(op);
                        _undoStack.Insert(0, op);
                        UpdateUndoRedoState();
                    };
                    menu.Items.Add(item);
                }
            }
            menu.IsOpen = true;
        }

        #endregion

        #region 数据面板

        private void ImportData_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择要导入的数据文件",
                Filter = "Shapefile|*.shp|GeoJSON|*.geojson|CSV|*.csv|所有文件|*.*",
                Multiselect = true
            };
            dlg.ShowDialog();
        }

        private void PanelOptions_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            var autoHide = new MenuItem { Header = "自动隐藏" };
            autoHide.Click += (s, _) => AutoHide_Click(s, e);
            var close = new MenuItem { Header = "关闭" };
            close.Click += (s, _) => PanelClose_Click(s, e);
            menu.Items.Add(autoHide);
            menu.Items.Add(close);
            menu.IsOpen = true;
        }

        private void AutoHide_Click(object sender, RoutedEventArgs e)
        {
            AutoHideBtn.Tag = AutoHideBtn.Tag is null ? "pinned" : null;
        }

        private void PanelClose_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe)
            {
                var panel = fe.Parent as UIElement;
                while (panel is not null && panel is not Border)
                    panel = (panel as FrameworkElement)?.Parent as UIElement;
                if (panel is Border b)
                    b.Visibility = b.Visibility == Visibility.Visible
                        ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private static readonly SolidColorBrush SkyBlue = new(Color.FromRgb(0x4F, 0xC3, 0xF7));
        private static readonly SolidColorBrush GrayBorder = new(Color.FromRgb(0xD0, 0xD0, 0xD0));
        private bool _searchFocused;

        private void SearchBorder_MouseEnter(object sender, MouseEventArgs e)
        { if (!_searchFocused) SearchBorder.BorderBrush = SkyBlue; }
        private void SearchBorder_MouseLeave(object sender, MouseEventArgs e)
        { if (!_searchFocused) SearchBorder.BorderBrush = GrayBorder; }
        private void SearchBorder_MouseDown(object sender, MouseButtonEventArgs e)
            => SearchBox.Focus();

        private void SearchBorder_GotFocus(object sender, RoutedEventArgs e)
        { _searchFocused = true; SearchBorder.BorderBrush = SkyBlue; }
        private void SearchBorder_LostFocus(object sender, RoutedEventArgs e)
        { _searchFocused = false; SearchBorder.BorderBrush = GrayBorder; }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Focus();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SwitchToSpatial(object sender, RoutedEventArgs e)
        {
            BtnSpatial.Background = new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0));
            BtnSpatial.Foreground = Brushes.White;
            BtnAttribute.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
            BtnAttribute.Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            LoadDirectoryTree(SpatialDataPath);
        }

        private void SwitchToAttribute(object sender, RoutedEventArgs e)
        {
            BtnAttribute.Background = new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0));
            BtnAttribute.Foreground = Brushes.White;
            BtnSpatial.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
            BtnSpatial.Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            LoadDirectoryTree(AttributeDataPath);
        }

        #region 目录树

        private void LoadDirectoryTree(string rootPath)
        {
            _currentRootPath = rootPath;
            DataTree.Items.Clear();
            if (!Directory.Exists(rootPath)) return;
            var root = CreateTreeItem(rootPath, true);
            DataTree.Items.Add(root);
        }

        private TreeViewItem CreateTreeItem(string path, bool isRoot = false)
        {
            var name = isRoot ? Path.GetFileName(path) : Path.GetFileName(path);

            // Determine display icon and data type
            ImageSource? icon = null;
            var dataType = SpatialDataHelper.ClassifyFile(path);
            var isSpatialFile = SpatialDataHelper.IsSpatialDataFile(path);

            if (isSpatialFile)
            {
                // Use distinct icon for vector vs raster
                icon = dataType == SpatialDataType.Vector
                    ? SystemIconProvider.GetIcon(path)  // .shp has ArcGIS icon in Windows
                    : SystemIconProvider.GetIcon(path); // .tif icon
            }
            else
            {
                icon = SystemIconProvider.GetIcon(path);
            }

            // Build header
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var img = new Image
            {
                Source = icon,
                Width = 16, Height = 16,
                Margin = new Thickness(0, 0, 4, 0),
                Stretch = System.Windows.Media.Stretch.Uniform
            };
            sp.Children.Add(img);

            // 文件名（不加类型标签，跟 ArcGIS Pro 一样的干净显示）
            sp.Children.Add(new TextBlock
            {
                Text = name,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A))
                });

            var item = new TreeViewItem
            {
                Header = sp,
                Tag = path,
                FontWeight = isRoot ? FontWeights.SemiBold : FontWeights.Normal
            };

            // Store data type info for quick access
            if (isSpatialFile)
            {
                item.Tag = new SpatialFileInfo
                {
                    FilePath = path,
                    DisplayName = Path.GetFileNameWithoutExtension(path),
                    DataType = dataType
                };
            }

            // Add context menu for spatial data files
            if (isSpatialFile)
            {
                item.ContextMenu = CreateDataItemContextMenu(path, dataType);
            }

            // Lazy load: add dummy child so expand arrow appears for directories
            if (Directory.Exists(path))
            {
                try
                {
                    bool hasContent = Directory.GetDirectories(path).Length > 0
                                   || Directory.GetFiles(path).Length > 0;
                    if (hasContent)
                    {
                        item.Items.Add(new TreeViewItem { Header = "loading...", Tag = "__dummy__" });
                        item.Expanded += OnDirExpanded;
                    }
                }
                catch { /* skip inaccessible folders */ }
            }

            return item;
        }

        /// <summary>
        /// Create a right-click context menu for spatial data items.
        /// </summary>
        private ContextMenu CreateDataItemContextMenu(string path, SpatialDataType dataType)
        {
            var menu = new ContextMenu();
            var typeLabel = SpatialDataHelper.GetDataTypeLabel(dataType);

            var addItem = new MenuItem { Header = $"添加到地图 ({typeLabel})" };
            addItem.Click += async (s, e) => await AddDataToMap(path);
            menu.Items.Add(addItem);

            var zoomItem = new MenuItem { Header = "缩放至图层" };
            zoomItem.Click += async (s, e) =>
            {
                var layer = _mapLayerService.Layers.FirstOrDefault(l => l.FilePath == path);
                if (layer != null) await _mapLayerService.ZoomToLayerAsync(layer);
            };
            menu.Items.Add(zoomItem);

            menu.Items.Add(new Separator());

            var propsItem = new MenuItem { Header = "属性" };
            propsItem.Click += (s, e) =>
            {
                MessageBox.Show($"文件: {path}\n类型: {typeLabel}",
                    "数据属性", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            menu.Items.Add(propsItem);

            return menu;
        }

        /// <summary>
        /// TreeView double-click handler — add spatial data to map.
        /// </summary>
        private async void DataTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            var clickedItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
            if (clickedItem == null) return;

            string? filePath = null;

            if (clickedItem.Tag is SpatialFileInfo sfInfo)
            {
                filePath = sfInfo.FilePath;
            }
            else if (clickedItem.Tag is string strPath && !strPath.Equals("__dummy__"))
            {
                // Check if it's a spatial file by extension
                if (SpatialDataHelper.IsSpatialDataFile(strPath))
                    filePath = strPath;
            }

            if (!string.IsNullOrEmpty(filePath))
            {
                await AddDataToMap(filePath);
            }
        }

        /// <summary>
        /// Add a spatial data file to the map.
        /// </summary>
        private async Task AddDataToMap(string filePath)
        {
            try
            {
                var layer = await _mapLayerService.AddLayerAsync(filePath);
                if (layer != null)
                {
                    RecordOperation($"添加图层: {layer.Name}");
                    StatusBar1.Text = $"已添加图层: {layer.Name} ({SpatialDataHelper.GetDataTypeLabel(layer.Type)})";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法添加图层: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Helper to walk up the visual tree to find a parent of a given type.
        /// </summary>
        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t) return t;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void OnDirExpanded(object sender, RoutedEventArgs e)
        {
            var item = (TreeViewItem)sender;
            // Only load once: check if first child is the dummy
            if (item.Items.Count == 1 && item.Items[0] is TreeViewItem dummy && "__dummy__".Equals(dummy.Tag))
            {
                item.Items.Clear();
                var path = (item.Tag as SpatialFileInfo)?.FilePath ?? item.Tag as string;
                if (string.IsNullOrEmpty(path)) return;

                try
                {
                    // Sort: folders first, then files
                    foreach (var dir in Directory.GetDirectories(path))
                    {
                        try { item.Items.Add(CreateTreeItem(dir)); } catch { }
                    }
                    foreach (var file in Directory.GetFiles(path))
                    {
                        // Skip shapefile companion files and raster sidecar files
                        if (SpatialDataHelper.IsCompanionFile(file)) continue;
                        try { item.Items.Add(CreateTreeItem(file)); } catch { }
                    }
                }
                catch { /* skip inaccessible folders */ }
            }
        }

        #endregion

        #region 分析面板

        private void ShowAnalysis(ModuleInfo module)
        {
            if (AnalysisHost.Children.Count == 0)
            {
                var panel = new AnalysisPanel();
                panel.Closed += () =>
                {
                    AnalysisHost.Children.Clear();
                    MapViewControl.Visibility = Visibility.Visible;
                };
                AnalysisHost.Children.Add(panel);
            }
            else if (AnalysisHost.Children[0] is AnalysisPanel existing)
            {
                existing.LoadModule(module);
            }
            MapViewControl.Visibility = Visibility.Collapsed;
        }

        private void Geo_Btn_Click(object sender, RoutedEventArgs e) => ShowAnalysis(ModuleRegistry.Geology);
        private void Topo_Btn_Click(object sender, RoutedEventArgs e) => ShowAnalysis(ModuleRegistry.Topography);
        private void Veg_Btn_Click(object sender, RoutedEventArgs e) => ShowAnalysis(ModuleRegistry.Vegetation);

        #endregion

        #endregion
    }
}
