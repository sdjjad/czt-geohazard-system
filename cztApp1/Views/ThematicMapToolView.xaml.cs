using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using cztApp1.Services;
using Microsoft.Win32;
using IoPath = System.IO.Path;

namespace cztApp1.Views
{
    public partial class ThematicMapToolView : UserControl
    {
        private ThematicMapService? _service;
        private MapLayerService? _layerService;
        private string? _lastOutputPath;
        private bool _isDrawing;
        private Point _lastPoint;
        private Polyline? _currentLine;

        public ThematicMapToolView()
        {
            InitializeComponent();
        }

        public void SetMapView(MapView mapView)
        {
            _service = new ThematicMapService(mapView);
        }

        public void SetLayerService(MapLayerService layerService)
        {
            _layerService = layerService;
            RefreshLayerList();
        }

        public void LoadTool(string toolName)
        {
            _lastOutputPath = null;
            StatusBorder.Visibility = Visibility.Collapsed;

            if (toolName == "StatChart")
                MapTitle.Text = "地质灾害统计图";
            else if (toolName == "StatTable")
                MapTitle.Text = "地质灾害统计表";
            else
                MapTitle.Text = "长株潭地质灾害专题图";
        }

        private void RefreshLayerList()
        {
            LayerCombo.Items.Clear();
            LayerCombo.Items.Add("-- 选择图层 --");
            if (_layerService == null) { LayerCombo.SelectedIndex = 0; return; }
            foreach (var l in _layerService.Layers)
                LayerCombo.Items.Add($"{l.Name} [{IoPath.GetFileName(l.FilePath)}]");
            LayerCombo.SelectedIndex = 0;
        }

        private async void LayerCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            int idx = LayerCombo.SelectedIndex - 1;
            if (idx < 0 || _layerService == null) return;
            var layers = _layerService.Layers.ToList();
            if (idx >= layers.Count) return;
            await _layerService.ZoomToLayerAsync(layers[idx]);
        }

        #region 画布自由绘制

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            _isDrawing = true;
            _lastPoint = e.GetPosition(PreviewCanvas);
            _currentLine = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0)),
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            _currentLine.Points.Add(_lastPoint);
            PreviewCanvas.Children.Add(_currentLine);
            e.Handled = true;
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawing || _currentLine == null) return;
            var pt = e.GetPosition(PreviewCanvas);
            if ((pt - _lastPoint).Length > 2)
            {
                _currentLine.Points.Add(pt);
                _lastPoint = pt;
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDrawing = false;
            _currentLine = null;
        }

        private void ClearCanvas_Click(object sender, RoutedEventArgs e)
        {
            PreviewCanvas.Children.Clear();
        }

        #endregion

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog { Title = "选择输出目录" };
            if (dlg.ShowDialog() == true)
                OutputFolder.Text = dlg.FolderName;
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = OutputFolder.Text;
            if (!Directory.Exists(folder))
            {
                try { Directory.CreateDirectory(folder); } catch { }
            }
            try
            {
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开目录: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void GenerateMap_Click(object sender, RoutedEventArgs e)
        {
            if (_service == null)
            {
                ShowStatus("⚠ 请先加载图层到地图", "#E53935");
                return;
            }

            ShowStatus("⏳ 正在生成专题图...", "#1565C0");

            try
            {
                var config = new ThematicMapService.ThematicMapConfig
                {
                    Title = MapTitle.Text.Trim(),
                    OutputFolder = OutputFolder.Text.Trim(),
                    ImageWidth = 1200,
                    ImageHeight = 900,
                    IncludeLegend = true,
                    IncludeScaleBar = true,
                    IncludeNorthArrow = true,
                    IncludeGrid = false
                };

                _lastOutputPath = await Task.Run(() =>
                    _service.ExportThematicMapAsync(config, msg =>
                        Dispatcher.Invoke(() => ShowStatus($"⏳ {msg}", "#1565C0"))));

                ShowStatus($"✅ 专题图已生成！\n{_lastOutputPath}", "#43A047");

                if (File.Exists(_lastOutputPath))
                {
                    try { Process.Start(new ProcessStartInfo { FileName = _lastOutputPath, UseShellExecute = true }); }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"❌ 生成失败: {ex.Message}", "#E53935");
            }
        }

        private void ShowStatus(string msg, string colorHex)
        {
            StatusText.Text = msg;
            StatusText.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(colorHex)!);
            StatusBorder.Visibility = Visibility.Visible;
        }
    }
}
