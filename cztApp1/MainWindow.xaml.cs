using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using cztApp1.Models;
using cztApp1.Services;
using cztApp1.Views;
using cztApp1.Views.Tools;

namespace cztApp1
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<string> _undoStack = new();
        private readonly ObservableCollection<string> _redoStack = new();
        private const int MaxHistory = 20;

        // 数据目录：从 exe 向上找到解决方案根目录下的 Data 文件夹
        private static readonly string SpatialDataPath = FindDataPath("空间数据");
        private static readonly string AttributeDataPath = FindDataPath("属性数据");
        private string _currentRootPath = SpatialDataPath;

        private static string FindDataPath(string subFolder)
        {
            // 从 exe 目录向上查找 Data/{subFolder}，适配不同开发/部署环境
            var exeDir = AppContext.BaseDirectory;
            for (var dir = exeDir; dir != null; dir = Path.GetDirectoryName(dir))
            {
                var candidate = Path.Combine(dir, "Data", subFolder);
                if (Directory.Exists(candidate)) return candidate;
            }
            // 回退到旧路径（兼容）
            return $@"D:\geomatics_task\地理信息工程及应用\2322050202于景赫-12组-长株潭地质灾害（土壤植被）\数据\{subFolder}";
        }

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

            // 默认只显示数据面板和图层面板，隐藏符号和地理处理面板
            SymbolPanelAnchor.Hide();
            GeoPanelAnchor.Hide();


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

        private void GeoPanel_GotFocus(object sender, MouseButtonEventArgs e) { }
        private void GeoOptions_Click(object sender, RoutedEventArgs e) { }
        private void GeoAutoHide_Click(object sender, RoutedEventArgs e) { }

        private void DockManager_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 不做拦截，让事件自然路由到 Esri MapView
        }

        private void GeoPanelClose_Click(object sender, RoutedEventArgs e)
        {
            GeoPanelAnchor.Hide();
            GeoPanelAnchor.Title = "地理处理";
        }

        private void ViewMenu_Click(object sender, RoutedEventArgs e)
        {
            bool mapVisible = MapViewControl.Visibility == Visibility.Visible;

            string Chk(bool v) => v ? "✓ " : "   ";

            var menu = new ContextMenu();
            var miData = new MenuItem { Header = $"{Chk(DataPanelAnchor.IsVisible)}数据面板", Tag = "Data" };
            var miMap  = new MenuItem { Header = $"{Chk(mapVisible)}地图视图", Tag = "Map" };
            var miLayer= new MenuItem { Header = $"{Chk(LayerPanelAnchor.IsVisible)}图层面板", Tag = "Layer" };
            var miSym  = new MenuItem { Header = $"{Chk(SymbolPanelAnchor.IsVisible)}符号系统", Tag = "Symbol" };
            var miGeo  = new MenuItem { Header = $"{Chk(GeoPanelAnchor.IsVisible)}地理处理", Tag = "Geo" };

            foreach (var mi in new[] { miData, miMap, miLayer, miSym, miGeo })
            {
                mi.Click += ViewMenuItem_Click;
                menu.Items.Add(mi);
            }
            menu.PlacementTarget = ViewMenuBtn;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void ViewMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem mi || mi.Tag is not string tag) return;

            switch (tag)
            {
                case "Data":
                    if (DataPanelAnchor.IsVisible) DataPanelAnchor.Hide(); else DataPanelAnchor.Show();
                    break;
                case "Map":
                    MapViewControl.Visibility = MapViewControl.Visibility == Visibility.Visible
                        ? Visibility.Collapsed : Visibility.Visible;
                    break;
                case "Layer":
                    if (LayerPanelAnchor.IsVisible) LayerPanelAnchor.Hide(); else LayerPanelAnchor.Show();
                    break;
                case "Symbol":
                    if (SymbolPanelAnchor.IsVisible) SymbolPanelAnchor.Hide(); else SymbolPanelAnchor.Show();
                    break;
                case "Geo":
                    if (GeoPanelAnchor.IsVisible) GeoPanelAnchor.Hide(); else GeoPanelAnchor.Show();
                    break;
            }
        }

        private void SymbolAutoHide_Click(object sender, RoutedEventArgs e) { }

        private void SymbolOptions_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            var close = new MenuItem { Header = "关闭符号系统" };
            close.Click += (_, _) => SymbolPanelAnchor.Hide();
            menu.Items.Add(close);
            menu.IsOpen = true;
        }

        private void SymbolPanelClose_Click(object sender, RoutedEventArgs e)
        {
            SymbolPanelAnchor.Hide();
            SymbolPanelAnchor.Title = "符号系统";
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
            LayerPanelAnchor.Hide();
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

            // 拖拽排序（排除 CheckBox 点击）
            LayerTreeView.PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (IsClickOn<CheckBox>(e.OriginalSource)) return;
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

            // 属性浏览（仅矢量图层）
            if (layer.Type == SpatialDataType.Vector)
            {
                var browse = new MenuItem { Header = "📋 属性浏览" };
                browse.Click += (_, _) => OpenAttributeTableForFile(layer.FilePath);
                ctx.Items.Add(browse);
            }

            ctx.Items.Add(new Separator());
            ctx.Items.Add(up);
            ctx.Items.Add(down);
            ctx.Items.Add(new Separator());
            ctx.Items.Add(remove);
            ctx.IsOpen = true;
        }

        private void SymbolItem_Click(object sender, MouseButtonEventArgs e)
        {
            // 直接从 DataContext 取数据，避免 FindParent 在 TreeViewItem 上自匹配的 bug
            if (sender is FrameworkElement fe && fe.DataContext is SymbolItem sym && sym.Layer is MapLayer layer)
                ShowSymbolEditor(layer);
        }

        private void LayerName_Click(object sender, MouseButtonEventArgs e)
        {
            // 点击图层名称整行 → 打开符号系统
            if (sender is FrameworkElement fe && fe.DataContext is MapLayer layer)
                ShowSymbolEditor(layer);
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

        private static bool IsClickOn<T>(object source) where T : DependencyObject
        {
            if (source is T) return true;
            if (source is DependencyObject d)
                return FindParent<T>(d) != null;
            return false;
        }

        #region 符号系统面板

        // 预设色板（ArcGIS Pro 风格常用色）
        private static readonly string[] ColorPalette =
        {
            "#E81123", "#FF8C00", "#FFD700", "#228B22", "#0078D7",
            "#1565C0", "#64B5F6", "#00BCD4", "#009688", "#4CAF50",
            "#8BC34A", "#CDDC39", "#FFC107", "#FF9800", "#FF5722",
            "#795548", "#9E9E9E", "#607D8B", "#000000", "#FFFFFF"
        };

        private MapLayer? _currentSymbolLayer;
        private PropertyChangedEventHandler? _symbolPreviewHandler;

        private void ShowSymbolEditor(MapLayer layer)
        {
            _currentSymbolLayer = layer;
            SymbolPanelAnchor.Show();
            SymbolPanelAnchor.IsActive = true;
            SymbolPanelAnchor.IsSelected = true;
            SymbolPanelAnchor.Title = $"符号系统 — {layer.Name}";
            var isVector = layer.Type == SpatialDataType.Vector;
            var geom = layer.Symbols.Count > 0 ? layer.Symbols[0].Geometry : SymbolGeometry.Polygon;

            // 取消旧订阅
            if (_symbolPreviewHandler != null && _currentSymbolLayer?.VectorSymbol != null)
                _currentSymbolLayer.VectorSymbol.PropertyChanged -= _symbolPreviewHandler;

            SymbolEditorHost.Children.Clear();

            if (isVector && layer.VectorSymbol != null)
            {
                BuildVectorSymbolEditor(layer, geom);
            }
            else if (layer.RasterSymbol != null)
            {
                BuildRasterSymbolEditor(layer, layer.RasterSymbol);
            }
        }

        private void OnSymbolEdited(MapLayer layer)
        {
            _ = MapViewControl.UpdateLayerStyleAsync(layer);
            if (layer.VectorSymbol != null && _currentSymbolLayer == layer)
                UpdateSymbolPreview(layer.VectorSymbol, layer.Symbols[0].Geometry);
        }

        private void UpdateSymbolPreview(VectorSymbol vs, SymbolGeometry geom)
        {
            // 预览更新由 PropertyChanged 事件驱动，在编辑器面板内找到预览元素替换
            if (SymbolEditorHost.Children.Count > 0 && SymbolEditorHost.Children[0] is StackPanel sp)
            {
                var previewContainer = sp.Children.OfType<Border>().FirstOrDefault(b => b.Name == "PreviewBorder");
                if (previewContainer?.Child is Grid g && g.Children.Count > 0)
                {
                    g.Children.Clear();
                    g.Children.Add(BuildShape(vs, geom));
                }
            }
        }

        private static FrameworkElement BuildShape(VectorSymbol vs, SymbolGeometry geom)
        {
            FrameworkElement shape;
            if (geom == SymbolGeometry.Line)
            {
                var c = (Color)ColorConverter.ConvertFromString(vs.StrokeColor);
                shape = new System.Windows.Shapes.Line
                {
                    X1 = 6, Y1 = 24, X2 = 50, Y2 = 24,
                    Stroke = new SolidColorBrush(c),
                    StrokeThickness = Math.Max(2, vs.StrokeWidth),
                    StrokeEndLineCap = PenLineCap.Round, StrokeStartLineCap = PenLineCap.Round
                };
            }
            else if (geom == SymbolGeometry.Point)
            {
                var c = (Color)ColorConverter.ConvertFromString(vs.PointColor);
                double sz = Math.Max(8, Math.Min(40, vs.PointSize * 2.5));
                shape = new System.Windows.Shapes.Ellipse
                {
                    Width = sz, Height = sz, Fill = new SolidColorBrush(c),
                    Stroke = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)), StrokeThickness = 1
                };
            }
            else
            {
                var f = (Color)ColorConverter.ConvertFromString(vs.FillColor);
                f.A = (byte)(vs.FillOpacity * 255);
                var s = (Color)ColorConverter.ConvertFromString(vs.StrokeColor);
                shape = new Border
                {
                    Width = 44, Height = 32, Background = new SolidColorBrush(f),
                    BorderBrush = new SolidColorBrush(s),
                    BorderThickness = new Thickness(Math.Max(1, vs.StrokeWidth))
                };
            }
            shape.HorizontalAlignment = HorizontalAlignment.Center;
            shape.VerticalAlignment = VerticalAlignment.Center;
            return shape;
        }

        // ========== 矢量符号编辑器 ==========

        private void BuildVectorSymbolEditor(MapLayer layer, SymbolGeometry geom)
        {
            var vs = layer.VectorSymbol!;
            int row = 0;
            Action onChanged = () => OnSymbolEdited(layer);

            // 订阅 PropertyChanged 实时更新预览
            _symbolPreviewHandler = (_, _) => UpdateSymbolPreview(vs, geom);
            vs.PropertyChanged += _symbolPreviewHandler;

            var sp = new StackPanel();

            // 符号预览区
            var previewBorder = new Border
            {
                Name = "PreviewBorder",
                Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0)),
                BorderThickness = new Thickness(1), Height = 60,
                Margin = new Thickness(0, 4, 0, 8), CornerRadius = new CornerRadius(3)
            };
            var previewGrid = new Grid();
            previewGrid.Children.Add(BuildShape(vs, geom));
            previewBorder.Child = previewGrid;
            sp.Children.Add(previewBorder);

            // 属性编辑区
            var propGrid = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            propGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            propGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            propGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            if (geom == SymbolGeometry.Polygon)
            {
                AddColorPicker(propGrid, ref row, "填充", vs.FillColor, c => { vs.FillColor = c; onChanged(); });
                AddSliderRow(propGrid, ref row, "透明度", vs.FillOpacity, 0, 1, v => { vs.FillOpacity = v; onChanged(); });
                AddColorPicker(propGrid, ref row, "轮廓", vs.StrokeColor, c => { vs.StrokeColor = c; onChanged(); });
                AddNumRow(propGrid, ref row, "线宽", vs.StrokeWidth, v => { vs.StrokeWidth = v; onChanged(); });
            }
            else if (geom == SymbolGeometry.Line)
            {
                AddColorPicker(propGrid, ref row, "颜色", vs.StrokeColor, c => { vs.StrokeColor = c; onChanged(); });
                AddNumRow(propGrid, ref row, "线宽", vs.StrokeWidth, v => { vs.StrokeWidth = v; onChanged(); });
            }
            else if (geom == SymbolGeometry.Point)
            {
                AddColorPicker(propGrid, ref row, "颜色", vs.PointColor, c => { vs.PointColor = c; onChanged(); });
                AddNumRow(propGrid, ref row, "大小", vs.PointSize, v => { vs.PointSize = v; onChanged(); });
            }

            sp.Children.Add(propGrid);

            // ====== 按字段配色 ======
            sp.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)), Margin = new Thickness(0, 10, 0, 6) });
            sp.Children.Add(new TextBlock { Text = "🎨 按字段配色", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)), Margin = new Thickness(0, 0, 0, 4) });

            var fieldCombo = new ComboBox { Height = 24, FontSize = 10, Margin = new Thickness(0, 0, 0, 4) };
            fieldCombo.Items.Add("-- 选择字段 --");
            _ = Task.Run(async () =>
            {
                try
                {
                    var table = await Esri.ArcGISRuntime.Data.ShapefileFeatureTable.OpenAsync(layer.FilePath);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        foreach (var f in table.Fields)
                            fieldCombo.Items.Add(f.Name);
                    });
                }
                catch { }
            });
            sp.Children.Add(fieldCombo);

            // 色带选择
            var rampNames = new[] { "蓝-白-红", "绿-黄-红", "蓝-青-绿", "紫-蓝-绿-黄-红", "ArcGIS默认" };
            var rampCombo = new ComboBox { Height = 24, FontSize = 10, Margin = new Thickness(0, 0, 0, 4) };
            int ri = 0;
            foreach (var rn in rampNames) { var item = new ComboBoxItem { Content = rn }; if (ri == 0) item.IsSelected = true; rampCombo.Items.Add(item); ri++; }
            sp.Children.Add(rampCombo);

            var applyBtn = new Button
            {
                Content = "▶ 应用配色", Height = 26, FontSize = 10,
                Background = new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0)),
                Foreground = Brushes.White, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 0, 0)
            };
            var layerRef = layer;
            applyBtn.Click += async (_, _) =>
            {
                if (fieldCombo.SelectedIndex <= 0) return;
                string fn = fieldCombo.SelectedItem?.ToString() ?? "";
                var ramp = GetColorRamp(rampCombo.SelectedIndex);
                applyBtn.IsEnabled = false;
                await _mapLayerService.ApplyFieldSymbologyAsync(layerRef, fn, ramp);
                applyBtn.IsEnabled = true;
                RecordOperation($"按字段配色: {layerRef.Name} → {fn}");
            };
            sp.Children.Add(applyBtn);

            SymbolEditorHost.Children.Add(sp);
        }

        // 预设色带
        private static List<System.Drawing.Color> GetColorRamp(int index) => index switch
        {
            1 => new() { System.Drawing.Color.FromArgb(0, 104, 55), System.Drawing.Color.FromArgb(165, 221, 114), System.Drawing.Color.FromArgb(248, 242, 18), System.Drawing.Color.FromArgb(235, 108, 0), System.Drawing.Color.FromArgb(183, 0, 0) },
            2 => new() { System.Drawing.Color.FromArgb(8, 48, 107), System.Drawing.Color.FromArgb(8, 81, 156), System.Drawing.Color.FromArgb(33, 113, 181), System.Drawing.Color.FromArgb(66, 146, 198), System.Drawing.Color.FromArgb(107, 174, 214), System.Drawing.Color.FromArgb(158, 202, 225), System.Drawing.Color.FromArgb(198, 219, 239), System.Drawing.Color.FromArgb(222, 235, 247) },
            3 => new() { System.Drawing.Color.FromArgb(84, 39, 136), System.Drawing.Color.FromArgb(38, 109, 185), System.Drawing.Color.FromArgb(0, 150, 98), System.Drawing.Color.FromArgb(182, 209, 0), System.Drawing.Color.FromArgb(208, 28, 0) },
            4 => new() { System.Drawing.Color.FromArgb(21, 101, 192), System.Drawing.Color.FromArgb(25, 118, 210), System.Drawing.Color.FromArgb(30, 136, 229), System.Drawing.Color.FromArgb(66, 165, 245), System.Drawing.Color.FromArgb(100, 181, 246), System.Drawing.Color.FromArgb(144, 202, 249), System.Drawing.Color.FromArgb(187, 222, 251) },
            _ => new() { System.Drawing.Color.FromArgb(33, 80, 198), System.Drawing.Color.FromArgb(244, 244, 244), System.Drawing.Color.FromArgb(209, 42, 44) },
        };

        // ========== 栅格符号编辑器 ==========

        private void BuildRasterSymbolEditor(MapLayer layer, RasterSymbol rs)
        {
            Action onChanged = () => OnSymbolEdited(layer);
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = "色带设置", FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                Margin = new Thickness(0, 2, 0, 4)
            });

            var bar = new Border { Height = 18, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 0, 8) };
            var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            foreach (var st in rs.Stops)
                grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(st.Color)!, st.Value / 255.0));
            bar.Background = grad;
            sp.Children.Add(bar);

            for (int i = 0; i < rs.Stops.Count; i++)
            {
                var stop = rs.Stops[i]; var idx = i;
                var r = new Grid { Margin = new Thickness(0, 2, 0, 0) };
                r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
                r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

                var lb = new TextBlock { Text = $"#{idx + 1}", FontSize = 10, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(lb, 0); r.Children.Add(lb);

                // 色块按钮
                var sw = new Button { Width = 22, Height = 16, Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(stop.Color)!), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
                sw.Click += (_, _) => ShowColorPopup(sw, stop.Color, c => { stop.Color = c; sw.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c)!); rs.NotifyChanged(); onChanged(); });
                Grid.SetColumn(sw, 1); r.Children.Add(sw);

                var vb = new TextBox { Text = stop.Value.ToString("F0"), FontSize = 10, Width = 44, VerticalAlignment = VerticalAlignment.Center };
                Action commitStop = () => { if (double.TryParse(vb.Text, out var v)) { stop.Value = v; rs.NotifyChanged(); onChanged(); } else vb.Text = stop.Value.ToString("F0"); };
                vb.LostFocus += (_, _) => commitStop();
                vb.KeyDown += (_, e) => { if (e.Key == Key.Enter) { commitStop(); vb.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); } };
                Grid.SetColumn(vb, 2); r.Children.Add(vb);

                sp.Children.Add(r);
            }
            SymbolEditorHost.Children.Add(sp);
        }

        // ========== 颜色选择器（点击色块弹出） ==========

        private static void AddColorPicker(Grid grid, ref int row, string label, string init, Action<string> setter)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            var tb = new TextBlock { Text = label, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(tb, row); Grid.SetColumn(tb, 0); grid.Children.Add(tb);

            var btn = new Button
            {
                Width = 24, Height = 18,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(init)!),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand, ToolTip = "点击选择颜色"
            };
            btn.Click += (_, _) => ShowColorPopup(btn, init, c =>
            {
                btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c)!);
                setter(c);
            });
            Grid.SetRow(btn, row); Grid.SetColumn(btn, 1); grid.Children.Add(btn);
            row++;
        }

        private static void ShowColorPopup(UIElement anchor, string current, Action<string> setter)
        {
            var popup = new Popup { PlacementTarget = anchor, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true };
            var panel = new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(4) };
            var grid = new UniformGrid { Columns = 5, Margin = new Thickness(2) };

            foreach (var color in ColorPalette)
            {
                var sw = new Border
                {
                    Width = 22, Height = 18, Margin = new Thickness(2),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)!),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0)),
                    BorderThickness = color == current ? new Thickness(2) : new Thickness(1),
                    Cursor = Cursors.Hand, CornerRadius = new CornerRadius(2),
                    Tag = color
                };
                sw.MouseLeftButtonDown += (s, ev) =>
                {
                    var c = (string)((FrameworkElement)s).Tag;
                    setter(c);
                    popup.IsOpen = false;
                    ev.Handled = true;
                };
                grid.Children.Add(sw);
            }

            panel.Child = grid;
            popup.Child = panel;
            popup.IsOpen = true;
        }

        // ========== 辅助行构建 ==========

        private void AddSliderRow(Grid grid, ref int row, string label, double init, double min, double max, Action<double> setter)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            var tb = new TextBlock { Text = label, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(tb, row); Grid.SetColumn(tb, 0); grid.Children.Add(tb);

            var slider = new Slider { Minimum = min, Maximum = max, Value = init, SmallChange = 0.05, Width = 80, VerticalAlignment = VerticalAlignment.Center };
            var vt = new TextBlock { Text = init.ToString("F2"), FontSize = 9, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
            slider.ValueChanged += (_, _) => { vt.Text = slider.Value.ToString("F2"); setter(slider.Value); };
            var hs = new StackPanel { Orientation = Orientation.Horizontal };
            hs.Children.Add(slider); hs.Children.Add(vt);
            Grid.SetRow(hs, row); Grid.SetColumn(hs, 2); grid.Children.Add(hs);
            row++;
        }

        private void AddNumRow(Grid grid, ref int row, string label, double init, Action<double> setter)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            var tb = new TextBlock { Text = label, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(tb, row); Grid.SetColumn(tb, 0); grid.Children.Add(tb);

            var box = new TextBox { Text = init.ToString("F1"), FontSize = 10, Width = 48, VerticalAlignment = VerticalAlignment.Center };
            Action commit = () => { if (double.TryParse(box.Text, out var v)) setter(v); else box.Text = init.ToString("F1"); };
            box.LostFocus += (_, _) => commit();
            box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { commit(); box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); } };
            Grid.SetRow(box, row); Grid.SetColumn(box, 2); grid.Children.Add(box);
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

        private void AutoHide_Click(object sender, RoutedEventArgs e) { }

        private void PanelClose_Click(object sender, RoutedEventArgs e)
        {
            DataPanelAnchor.Hide();
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
            _currentRootPath = _spatialDataPath;
            LoadDirectoryTree(_spatialDataPath);
        }

        private void SwitchToAttribute(object sender, RoutedEventArgs e)
        {
            BtnAttribute.Background = new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0));
            BtnAttribute.Foreground = Brushes.White;
            BtnSpatial.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
            BtnSpatial.Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            _currentRootPath = _attributeDataPath;
            LoadDirectoryTree(_attributeDataPath);
        }

        // 动态数据路径（默认使用常量，可连接自定义文件夹）
        private string _spatialDataPath = SpatialDataPath;
        private string _attributeDataPath = AttributeDataPath;

        private async void ConnectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "选择空间数据根目录" };
            if (dlg.ShowDialog() != true) return;

            _spatialDataPath = dlg.FolderName;
            _currentRootPath = _spatialDataPath;
            FolderPathLabel.Text = _spatialDataPath;

            // 自动推断属性数据文件夹
            var inferred = AttributeTableGenerator.InferAttrRoot(_spatialDataPath);
            if (inferred != null)
            {
                _attributeDataPath = inferred;
                BtnGenAttr.IsEnabled = true;
                BtnGenAttr.ToolTip = $"生成属性表到: {_attributeDataPath}";
            }
            else
            {
                _attributeDataPath = Path.Combine(
                    Directory.GetParent(_spatialDataPath)?.FullName ?? _spatialDataPath,
                    "属性数据");
                BtnGenAttr.IsEnabled = true;
            }

            // 刷新目录树
            SwitchToSpatial(sender, e);

            // 统计文件
            var (shp, tif) = AttributeTableGenerator.CountSpatialFiles(_spatialDataPath);
            StatusBar1.Text = $"已连接: {Path.GetFileName(_spatialDataPath)} — {shp} SHP, {tif} TIF";
            RecordOperation($"连接文件夹: {_spatialDataPath}");
        }

        private async void GenerateAttrTables_Click(object sender, RoutedEventArgs e)
        {
            BtnGenAttr.IsEnabled = false;
            StatusBar1.Text = "⏳ 正在生成属性表...";

            try
            {
                var (generated, cleaned) = await AttributeTableGenerator.GenerateAllAsync(
                    _spatialDataPath, _attributeDataPath,
                    (current, total, msg) =>
                    {
                        Dispatcher.Invoke(() =>
                            StatusBar1.Text = $"⏳ 生成属性表: {current}/{total} — {msg}");
                    });

                string cleanMsg = cleaned > 0 ? $"，清理了 {cleaned} 个孤儿文件" : "";
                StatusBar1.Text = $"✅ 已生成 {generated.Count} 个属性表{cleanMsg}";
                MessageBox.Show($"完成！\n• 生成 {generated.Count} 个属性表\n• 清理 {cleaned} 个孤儿文件\n\n输出目录:\n{_attributeDataPath}",
                    "属性表生成完成", MessageBoxButton.OK, MessageBoxImage.Information);

                // 切换到属性数据视图
                SwitchToAttribute(sender, e);
            }
            catch (Exception ex)
            {
                StatusBar1.Text = $"❌ 生成失败: {ex.Message}";
                MessageBox.Show($"生成属性表失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnGenAttr.IsEnabled = true;
            }
        }

        private void RefreshFolder_Click(object sender, RoutedEventArgs e)
        {
            LoadDirectoryTree(_currentRootPath);
            var (shp, tif) = AttributeTableGenerator.CountSpatialFiles(_spatialDataPath);
            StatusBar1.Text = $"已刷新: {shp} SHP, {tif} TIF";
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
                FontWeight = isRoot ? FontWeights.SemiBold : FontWeights.Normal,
                Style = (Style)FindResource("DataTreeItemStyle")
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

            // 属性浏览（仅对 SHP 矢量数据）
            if (dataType == SpatialDataType.Vector)
            {
                var browseItem = new MenuItem { Header = "📋 属性浏览" };
                browseItem.Click += (s, e) => OpenAttributeTableForFile(path);
                menu.Items.Add(browseItem);
            }

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

        #region 工具面板路由

        // 缓存各工具视图，切换时复用
        private GeoProcessToolView? _geoToolView;
        private ThematicMapToolView? _themeToolView;
        private AttributeTableView? _attrTableView;
        private AttributeQueryView? _attrQueryView;
        private AttributeManageView? _attrManageView;
        private SpatialQueryView? _spatialQueryView;

        /// <summary>
        /// 统一的 Ribbon 按钮 Click 处理器。通过 Tag 区分工具名，路由到对应视图。
        /// </summary>
        private void OpenTool_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string tag) return;
            var module = ResolveTool(tag);
            if (module == null) return;

            switch (tag)
            {
                // === 空间数据管理 ===
                case "ImportData":       ImportDataDialog(); break;
                case "MapOps":           OpenSimpleTool("地图操作", "缩放至全图 | 点击地图拖拽平移 | 滚轮缩放 | 右键旋转\n\n使用 ArcGIS Runtime 内置导航控件操作地图"); break;
                case "SpatialQuery":     OpenSpatialQuery(); break;
                case "SpatialAnalysis":  OpenGeoTool(module); break;
                case "Mapping":          OpenThematicMap(); break;

                // === 属性数据管理 ===
                case "AttributeBrowse":  OpenAttributeTable(); break;
                case "AttributeQuery":   OpenAttributeQuery(); break;
                case "AttributeManage":  OpenAttributeManage(); break;

                // === 土壤植被分析 ===
                case "SoilTypeAnalysis":
                case "SoilMoisture":
                case "VegType":
                case "VegCoverage":
                case "NDVI":             OpenGeoTool(module); break;

                // === 专题制图 ===
                case "StatChart":        OpenStatChartViewer(); break;
                case "StatTable":        OpenStatTableViewer(); break;
                case "ThematicMap":      OpenThematicMap(); break;

                default:                 OpenGeoTool(module); break;
            }
        }

        private static ModuleInfo? ResolveTool(string tag) => tag switch
        {
            "ImportData" => ModuleRegistry.ImportData,
            "SpatialQuery" => ModuleRegistry.SpatialQuery, "SpatialAnalysis" => ModuleRegistry.SpatialAnalysis,
            "Mapping" => ModuleRegistry.Mapping,
            "AttributeBrowse" => ModuleRegistry.AttributeBrowse, "AttributeQuery" => ModuleRegistry.AttributeQuery,
            "AttributeManage" => ModuleRegistry.AttributeManage,
            "SoilTypeAnalysis" => ModuleRegistry.SoilTypeAnalysis, "SoilMoisture" => ModuleRegistry.SoilMoisture,
            "VegType" => ModuleRegistry.VegType, "VegCoverage" => ModuleRegistry.VegCoverage, "NDVI" => ModuleRegistry.NDVI,
            "StatChart" => ModuleRegistry.StatChart, "StatTable" => ModuleRegistry.StatTable,
            "ThematicMap" => ModuleRegistry.ThematicMap,
            _ => null
        };

        // ===== 各工具打开方法 =====

        private void ShowGeoPanel(string title)
        {
            GeoPanelAnchor.Show();
            GeoPanelAnchor.IsActive = true;
            GeoPanelAnchor.IsSelected = true;
            GeoPanelAnchor.Title = title;
        }

        private void ClearPanelContent()
        {
            _geoToolView = null; _themeToolView = null;
            _attrTableView = null; _attrQueryView = null;
            _attrManageView = null; _spatialQueryView = null;
            GeoPanelContent.Content = null;
        }

        /// <summary>导入数据：打开文件对话框，选择文件添加到地图</summary>
        private async void ImportDataDialog()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择要导入的空间数据文件",
                Filter = "Shapefile|*.shp|GeoTIFF|*.tif;*.tiff|所有文件|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() != true) return;

            foreach (var file in dlg.FileNames)
            {
                try
                {
                    var layer = await _mapLayerService.AddLayerAsync(file);
                    if (layer != null)
                    {
                        RecordOperation($"导入数据: {layer.Name}");
                        StatusBar1.Text = $"已导入: {layer.Name}";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败 {Path.GetFileName(file)}:\n{ex.Message}", "错误");
                }
            }
        }

        /// <summary>打开属性浏览视图</summary>
        private void OpenAttributeTable()
        {
            ShowGeoPanel("属性浏览");
            ClearPanelContent();
            _attrTableView = new AttributeTableView();
            _attrTableView.SetLayerService(_mapLayerService);
            GeoPanelContent.Content = _attrTableView;
            RecordOperation("打开属性浏览");
        }

        /// <summary>
        /// 右键菜单调用：自动加载图层并打开属性浏览，选中指定文件
        /// </summary>
        private async void OpenAttributeTableForFile(string filePath)
        {
            // 确保图层已加载到地图
            var layer = _mapLayerService.Layers.FirstOrDefault(l => l.FilePath == filePath);
            if (layer == null)
            {
                // 自动加载图层
                try
                {
                    layer = await _mapLayerService.AddLayerAsync(filePath);
                    if (layer != null)
                        StatusBar1.Text = $"已加载: {layer.Name}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"加载图层失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // 打开属性浏览面板
            ShowGeoPanel("属性浏览");
            ClearPanelContent();
            _attrTableView = new AttributeTableView();
            _attrTableView.SetLayerService(_mapLayerService);
            GeoPanelContent.Content = _attrTableView;

            // 自动选中该图层（延迟等 ComboBox 初始化完成）
            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                _attrTableView?.SelectLayerByFilePath(filePath);
            }), System.Windows.Threading.DispatcherPriority.Loaded);

            RecordOperation($"属性浏览: {Path.GetFileName(filePath)}");
        }

        /// <summary>打开属性查询视图</summary>
        private void OpenAttributeQuery()
        {
            ShowGeoPanel("属性查询");
            ClearPanelContent();
            _attrQueryView = new AttributeQueryView();
            _attrQueryView.SetLayerService(_mapLayerService);
            GeoPanelContent.Content = _attrQueryView;
            RecordOperation("打开属性查询");
        }

        /// <summary>打开属性管理视图（可编辑属性表）</summary>
        private void OpenAttributeManage()
        {
            ShowGeoPanel("属性管理");
            ClearPanelContent();
            _attrManageView = new AttributeManageView();
            _attrManageView.SetLayerService(_mapLayerService);
            GeoPanelContent.Content = _attrManageView;
            RecordOperation("打开属性管理");
        }

        /// <summary>打开统计图查看器</summary>
        private void OpenStatChartViewer()
        {
            ShowGeoPanel("查看统计图");
            ClearPanelContent();
            var viewer = BuildStatChartViewer();
            GeoPanelContent.Content = viewer;
            RecordOperation("查看统计图");
        }

        /// <summary>打开统计表查看器</summary>
        private void OpenStatTableViewer()
        {
            ShowGeoPanel("查看统计表");
            ClearPanelContent();
            var viewer = BuildStatTableViewer();
            GeoPanelContent.Content = viewer;
            RecordOperation("查看统计表");
        }

        /// <summary>打开空间查询视图</summary>
        private void OpenSpatialQuery()
        {
            ShowGeoPanel("空间查询");
            ClearPanelContent();
            _spatialQueryView = new SpatialQueryView();
            _spatialQueryView.SetLayerService(_mapLayerService);
            GeoPanelContent.Content = _spatialQueryView;
            RecordOperation("打开空间查询");
        }

        /// <summary>简单信息工具（地图操作/属性管理）</summary>
        private void OpenSimpleTool(string title, string message)
        {
            ShowGeoPanel(title);
            ClearPanelContent();
            var tb = new TextBlock
            {
                Text = message,
                FontSize = 11,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55)),
                TextWrapping = System.Windows.TextWrapping.Wrap,
                Margin = new System.Windows.Thickness(8)
            };
            GeoPanelContent.Content = tb;
            RecordOperation($"打开工具: {title}");
        }

        /// <summary>打开专题制图视图</summary>
        private void OpenThematicMap()
        {
            ShowGeoPanel("专题制图");
            ClearPanelContent();
            _themeToolView = new ThematicMapToolView();
            _themeToolView.SetMapView(MapViewControl);
            _themeToolView.SetLayerService(_mapLayerService);
            GeoPanelContent.Content = _themeToolView;
            _themeToolView.LoadTool("ThematicMap");
            RecordOperation("打开专题制图");
        }

        /// <summary>打开地理分析工具</summary>
        private void OpenGeoTool(ModuleInfo module)
        {
            ShowGeoPanel($"地理处理 — {module.Name}");
            ClearPanelContent();
            _geoToolView = new GeoProcessToolView();
            _geoToolView.SetLayerService(_mapLayerService);
            GeoPanelContent.Content = _geoToolView;
            _geoToolView.LoadTool(module);
            _geoToolView.RefreshLayerList();
            RecordOperation($"打开工具: {module.Name}");
        }

        #region 统计图/统计表查看器

        /// <summary>构建统计图查看器：列出输出目录中的PNG文件，点击预览</summary>
        private UIElement BuildStatChartViewer()
        {
            var grid = new Grid { Margin = new Thickness(4, 2, 4, 2) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var title = new TextBlock { Text = "📊 查看统计图", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)), Margin = new Thickness(0, 2, 0, 4) };
            Grid.SetRow(title, 0); grid.Children.Add(title);

            var hint = new TextBlock { Text = "选择输出目录中的PNG统计图进行预览", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)), Margin = new Thickness(0, 0, 0, 4) };
            Grid.SetRow(hint, 1); grid.Children.Add(hint);

            var outputFolder = @"D:\GeoHazardOutput";
            var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

            var inner = new StackPanel();
            if (Directory.Exists(outputFolder))
            {
                var pngFiles = Directory.GetFiles(outputFolder, "*.png", SearchOption.TopDirectoryOnly).OrderByDescending(File.GetLastWriteTime).ToList();
                if (pngFiles.Count == 0)
                {
                    inner.Children.Add(new TextBlock { Text = "暂无统计图，请先运行分析生成。", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)), Margin = new Thickness(0, 8, 0, 0) });
                }
                else
                {
                    foreach (var png in pngFiles)
                    {
                        var item = BuildChartItem(png);
                        inner.Children.Add(item);
                    }
                }
            }
            else
            {
                inner.Children.Add(new TextBlock { Text = $"输出目录不存在: {outputFolder}\n请先运行分析生成统计图。", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)), Margin = new Thickness(0, 8, 0, 0) });
            }

            scrollViewer.Content = inner;
            Grid.SetRow(scrollViewer, 2); grid.Children.Add(scrollViewer);
            return grid;
        }

        private static Border BuildChartItem(string pngPath)
        {
            var border = new Border
            {
                Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 0, 4), Padding = new Thickness(6), Cursor = Cursors.Hand
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            // 缩略图
            try
            {
                var img = new Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(pngPath)), Width = 120, Stretch = System.Windows.Media.Stretch.Uniform, Margin = new Thickness(0, 0, 8, 0) };
                sp.Children.Add(img);
            }
            catch { }
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock { Text = Path.GetFileName(pngPath), FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)) });
            try
            {
                var fi = new FileInfo(pngPath);
                info.Children.Add(new TextBlock { Text = $"{fi.LastWriteTime:yyyy-MM-dd HH:mm}  |  {fi.Length / 1024} KB", FontSize = 8, Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)) });
            }
            catch { }
            sp.Children.Add(info);
            border.Child = sp;
            // 点击用系统默认程序打开
            border.MouseLeftButtonDown += (_, _) =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = pngPath, UseShellExecute = true }); } catch { }
            };
            border.MouseEnter += (s, _) => ((Border)s).Background = new SolidColorBrush(Color.FromRgb(0xBB, 0xDE, 0xFB));
            border.MouseLeave += (s, _) => ((Border)s).Background = Brushes.White;
            return border;
        }

        /// <summary>构建统计表查看器：列出输出目录中的CSV文件，点击预览内容</summary>
        private UIElement BuildStatTableViewer()
        {
            var grid = new Grid { Margin = new Thickness(4, 2, 4, 2) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var title = new TextBlock { Text = "📋 查看统计表", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)), Margin = new Thickness(0, 2, 0, 4) };
            Grid.SetRow(title, 0); grid.Children.Add(title);

            var hint = new TextBlock { Text = "选择输出目录中的CSV统计表进行预览", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)), Margin = new Thickness(0, 0, 0, 4) };
            Grid.SetRow(hint, 1); grid.Children.Add(hint);

            var outputFolder = @"D:\GeoHazardOutput";

            // CSV 文件列表
            var fileList = new ListBox
            {
                FontSize = 10, Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                MaxHeight = 120, Margin = new Thickness(0, 0, 0, 6)
            };
            if (Directory.Exists(outputFolder))
            {
                var csvFiles = Directory.GetFiles(outputFolder, "*.csv", SearchOption.TopDirectoryOnly).OrderByDescending(File.GetLastWriteTime).ToList();
                foreach (var csv in csvFiles)
                    fileList.Items.Add(Path.GetFileName(csv));
            }
            Grid.SetRow(fileList, 2); grid.Children.Add(fileList);

            // 预览 DataGrid
            var previewGrid = new DataGrid
            {
                AutoGenerateColumns = true, IsReadOnly = true, FontSize = 9,
                Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                HeadersVisibility = DataGridHeadersVisibility.Column,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            Grid.SetRow(previewGrid, 3); grid.Children.Add(previewGrid);

            fileList.SelectionChanged += (_, _) =>
            {
                if (fileList.SelectedItem is string fileName)
                {
                    var csvPath = Path.Combine(outputFolder, fileName);
                    try
                    {
                        var dt = new System.Data.DataTable();
                        var lines = File.ReadAllLines(csvPath, System.Text.Encoding.UTF8);
                        if (lines.Length > 0)
                        {
                            var headers = lines[0].Split(',');
                            foreach (var h in headers) dt.Columns.Add(h, typeof(string));
                            for (int i = 1; i < Math.Min(lines.Length, 501); i++)
                            {
                                var row = dt.NewRow();
                                var vals = lines[i].Split(',');
                                for (int j = 0; j < Math.Min(headers.Length, vals.Length); j++)
                                    row[j] = vals[j];
                                dt.Rows.Add(row);
                            }
                        }
                        previewGrid.ItemsSource = dt.DefaultView;
                    }
                    catch { previewGrid.ItemsSource = null; }
                }
            };

            if (fileList.Items.Count > 0) fileList.SelectedIndex = 0;
            return grid;
        }

        #endregion

        #endregion

        #endregion
    }
}
