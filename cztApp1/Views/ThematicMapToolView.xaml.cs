using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using cztApp1.Services;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using IoPath = System.IO.Path;

namespace cztApp1.Views
{
    public partial class ThematicMapToolView : UserControl
    {
        private MapView? _mapView;
        private MapLayerService? _layerService;
        private string? _lastMapImagePath;
        private Image? _mapImage;
        private Border? _legendPanel;
        private FrameworkElement? _scaleBar;
        private FrameworkElement? _northArrow;
        private bool _isDragging;
        private FrameworkElement? _dragTarget;
        private Point _dragOffset;

        public ThematicMapToolView()
        {
            InitializeComponent();
        }

        public void SetMapView(MapView mapView) => _mapView = mapView;
        public void SetLayerService(MapLayerService s) { _layerService = s; RefreshLayerList(); }

        public void LoadTool(string tool) => MapTitle.Text = tool switch
        {
            "StatChart" => "地质灾害统计图",
            "StatTable" => "地质灾害统计表",
            _ => "长株潭地质灾害专题图"
        };

        private void RefreshLayerList()
        {
            LayerCombo.Items.Clear();
            LayerCombo.Items.Add("-- 选择图层 --");
            if (_layerService != null)
                foreach (var l in _layerService.Layers)
                    LayerCombo.Items.Add($"{l.Name} [{IoPath.GetFileName(l.FilePath)}]");
            LayerCombo.SelectedIndex = 0;
        }

        private MapLayer? SelectedLayer
        {
            get
            {
                int idx = LayerCombo.SelectedIndex - 1;
                if (idx < 0 || _layerService == null) return null;
                var all = _layerService.Layers.ToList();
                return idx < all.Count ? all[idx] : null;
            }
        }

        private async void LayerCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            var layer = SelectedLayer;
            if (layer != null)
            {
                await _layerService!.ZoomToLayerAsync(layer);
                await RefreshMapAsync();
            }
        }

        private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 画布大小改变时重绘
        }

        /// <summary>刷新底图——从ArcGIS Runtime导出真实地图</summary>
        private async void RefreshMap_Click(object sender, RoutedEventArgs e) => await RefreshMapAsync();

        private async Task RefreshMapAsync()
        {
            if (_mapView == null) return;
            StatusText.Text = "⏳ 导出地图...";

            try
            {
                var esriMap = _mapView.EsriControl;

                var rtImage = await esriMap.ExportImageAsync();
                var stream = await rtImage.GetEncodedBufferAsync();

                // 保存临时底图
                var tmpDir = IoPath.Combine(IoPath.GetTempPath(), "cztApp");
                Directory.CreateDirectory(tmpDir);
                _lastMapImagePath = IoPath.Combine(tmpDir, $"map_{Guid.NewGuid():N}.png");
                using (var fs = File.Create(_lastMapImagePath))
                    await stream.CopyToAsync(fs);

                // 重建画布
                MainCanvas.Children.Clear();

                // 1. 地图底图（铺满画布）
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(_lastMapImagePath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();

                double cw = MainCanvas.ActualWidth > 10 ? MainCanvas.ActualWidth : CanvasBorder.ActualWidth - 2;
                double ch = MainCanvas.ActualHeight > 10 ? MainCanvas.ActualHeight : 300;
                if (cw < 100) cw = 500;

                _mapImage = new Image { Source = bmp, Width = cw, Height = ch, Stretch = Stretch.UniformToFill };
                Canvas.SetLeft(_mapImage, 0); Canvas.SetTop(_mapImage, 0);
                MainCanvas.Children.Add(_mapImage);

                // 2. 真实图例（读取图层渲染器）
                var layer = SelectedLayer;
                if (layer != null)
                {
                    _legendPanel = BuildRealLegend(layer);
                    Canvas.SetLeft(_legendPanel, cw - 160);
                    Canvas.SetTop(_legendPanel, 10);
                    MainCanvas.Children.Add(_legendPanel);
                    MakeDraggable(_legendPanel);
                }

                // 3. 比例尺
                _scaleBar = BuildScaleBar(esriMap.MapScale);
                Canvas.SetLeft(_scaleBar, 20);
                Canvas.SetTop(_scaleBar, ch - 40);
                MainCanvas.Children.Add(_scaleBar);
                MakeDraggable(_scaleBar);

                // 4. 指北针
                _northArrow = BuildNorthArrow();
                Canvas.SetLeft(_northArrow, cw - 50);
                Canvas.SetTop(_northArrow, ch - 60);
                MainCanvas.Children.Add(_northArrow);
                MakeDraggable(_northArrow);

                StatusText.Text = "✅ 底图已刷新（图例/比例尺/指北针可拖动）";
            }
            catch (Exception ex) { StatusText.Text = $"❌ {ex.Message}"; }
        }

        // ====== 真实图例构建 ======
        private Border BuildRealLegend(MapLayer layer)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "图例", FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)), Margin = new Thickness(0, 0, 0, 4) });

            try
            {
                if (_mapView == null) return WrapLegend(panel);
                var esriMap = _mapView.EsriControl.Map;
                if (esriMap == null) return WrapLegend(panel);

                // 在操作图层中查找对应图层
                foreach (var opLayer in esriMap.OperationalLayers)
                {
                    if (opLayer is FeatureLayer fl && fl.Name == layer.Name)
                    {
                        var renderer = fl.Renderer;
                        if (renderer is UniqueValueRenderer uvr)
                        {
                            foreach (var uv in uvr.UniqueValues)
                            {
                                var color = ExtractColor(uv.Symbol);
                                panel.Children.Add(BuildLegendItem(uv.Label, color));
                            }
                            // 默认符号
                            if (uvr.DefaultSymbol != null)
                                panel.Children.Add(BuildLegendItem("其他", ExtractColor(uvr.DefaultSymbol)));
                        }
                        else if (renderer is ClassBreaksRenderer cbr)
                        {
                            foreach (var cb in cbr.ClassBreaks)
                            {
                                var color = ExtractColor(cb.Symbol);
                                panel.Children.Add(BuildLegendItem(cb.Label, color));
                            }
                        }
                        else if (renderer is SimpleRenderer sr)
                        {
                            var color = ExtractColor(sr.Symbol);
                            panel.Children.Add(BuildLegendItem(layer.Name, color));
                        }
                        break;
                    }
                }
            }
            catch { }

            if ((panel.Children[panel.Children.Count - 1] as StackPanel) == null
                && panel.Children.Count <= 1)
            {
                panel.Children.Add(BuildLegendItem(layer.Name, Color.FromRgb(0x15, 0x65, 0xC0)));
            }

            return WrapLegend(panel);
        }

        private static StackPanel BuildLegendItem(string label, Color color)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            sp.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Width = 16, Height = 12, Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)), StrokeThickness = 0.5,
                Margin = new Thickness(0, 0, 6, 0)
            });
            sp.Children.Add(new TextBlock { Text = label, FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                VerticalAlignment = System.Windows.VerticalAlignment.Center });
            return sp;
        }

        private static Color ExtractColor(Symbol? sym)
        {
            if (sym is SimpleFillSymbol sfs)
                return Color.FromRgb(sfs.Color.R, sfs.Color.G, sfs.Color.B);
            if (sym is SimpleLineSymbol sls)
                return Color.FromRgb(sls.Color.R, sls.Color.G, sls.Color.B);
            if (sym is SimpleMarkerSymbol sms)
                return Color.FromRgb(sms.Color.R, sms.Color.G, sms.Color.B);
            return Color.FromRgb(0x15, 0x65, 0xC0);
        }

        private static Border WrapLegend(StackPanel content)
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 6, 8, 6),
                Child = content, Cursor = Cursors.Hand
            };
        }

        // ====== 真实比例尺 ======
        private static FrameworkElement BuildScaleBar(double mapScale)
        {
            var grid = new Grid { Margin = new Thickness(0), Cursor = Cursors.Hand };
            var panel = new StackPanel();

            // 计算比例尺条长度
            double metersPerPixel = mapScale / 96.0 * 0.0254;
            int barWidth = 100;
            double barMeters = barWidth * metersPerPixel;
            string label;
            if (barMeters > 10000) label = $"{barMeters / 1000:F0} km";
            else if (barMeters > 1000) label = $"{barMeters / 1000:F1} km";
            else label = $"{barMeters:F0} m";

            var bar = new Grid { Width = barWidth, Height = 6 };
            bar.ColumnDefinitions.Add(new ColumnDefinition());
            bar.ColumnDefinitions.Add(new ColumnDefinition());
            bar.Children.Add(new Border { Background = Brushes.Black, Height = 6 });
            var w = new Border { Background = Brushes.White, Height = 6 };
            Grid.SetColumn(w, 1); bar.Children.Add(w);

            panel.Children.Add(bar);
            panel.Children.Add(new Border { Width = barWidth, Height = 2, Background = Brushes.Black, HorizontalAlignment = System.Windows.HorizontalAlignment.Left });
            panel.Children.Add(new TextBlock { Text = label, FontSize = 8, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)) });
            panel.Children.Add(new TextBlock { Text = $"1:{mapScale / 1000:F0}K", FontSize = 7, Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)) });

            return new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                Padding = new Thickness(6, 4, 6, 4),
                CornerRadius = new CornerRadius(2),
                Child = panel
            };
        }

        // ====== 真实指北针 ======
        private static FrameworkElement BuildNorthArrow()
        {
            var canvas = new Canvas { Width = 30, Height = 40, Cursor = Cursors.Hand };
            var arrow = new System.Windows.Shapes.Polygon
            {
                Points = new PointCollection { new(15, 0), new(25, 25), new(15, 20), new(5, 25) },
                Fill = new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23))
            };
            canvas.Children.Add(arrow);
            var n = new TextBlock { Text = "N", FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23)) };
            Canvas.SetLeft(n, 9); Canvas.SetTop(n, 24);
            canvas.Children.Add(n);

            return new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                Padding = new Thickness(4), CornerRadius = new CornerRadius(2),
                Child = canvas
            };
        }

        // ====== 拖动 ======
        private void MakeDraggable(FrameworkElement el)
        {
            el.MouseLeftButtonDown += (s, e) =>
            {
                _isDragging = true;
                _dragTarget = el;
                _dragOffset = e.GetPosition(el);
                el.CaptureMouse();
                e.Handled = true;
            };
            el.MouseMove += (s, e) =>
            {
                if (!_isDragging || _dragTarget != el) return;
                var pos = e.GetPosition(MainCanvas);
                Canvas.SetLeft(el, pos.X - _dragOffset.X);
                Canvas.SetTop(el, pos.Y - _dragOffset.Y);
            };
            el.MouseLeftButtonUp += (s, e) =>
            {
                _isDragging = false; _dragTarget = null;
                el.ReleaseMouseCapture();
            };
        }

        // ====== 生成最终专题图PNG ======
        private async void GenerateMap_Click(object sender, RoutedEventArgs e)
        {
            if (MainCanvas.Children.Count == 0) { StatusText.Text = "请先刷新底图"; return; }

            var dlg = new Microsoft.Win32.SaveFileDialog
            { Title = "保存专题图", Filter = "PNG|*.png", FileName = $"{MapTitle.Text}.png" };
            if (dlg.ShowDialog() != true) return;

            StatusText.Text = "⏳ 生成中...";
            try
            {
                await Task.Run(() =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        var rtb = new RenderTargetBitmap(
                            (int)MainCanvas.ActualWidth, (int)MainCanvas.ActualHeight,
                            96, 96, PixelFormats.Pbgra32);
                        rtb.Render(MainCanvas);

                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(rtb));
                        using var fs = File.Create(dlg.FileName);
                        encoder.Save(fs);
                    });
                });
                StatusText.Text = $"✅ 已保存: {IoPath.GetFileName(dlg.FileName)}";
                if (File.Exists(dlg.FileName))
                    Process.Start(new ProcessStartInfo { FileName = dlg.FileName, UseShellExecute = true });
            }
            catch (Exception ex) { StatusText.Text = $"❌ {ex.Message}"; }
        }
    }
}
