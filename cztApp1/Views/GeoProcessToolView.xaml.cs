using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using cztApp1.Models;
using cztApp1.Services;
using Microsoft.Win32;
using IoPath = System.IO.Path;

namespace cztApp1.Views
{
    public partial class GeoProcessToolView : UserControl
    {
        private GeoAnalysisService _service = new();
        private MapLayerService? _layerService;
        private ModuleInfo _module = null!;
        private List<StatResult>? _lastResults;

        private static readonly Color[] BarColors =
        {
            Color.FromRgb(0x15, 0x65, 0xC0), Color.FromRgb(0x43, 0xA0, 0x47),
            Color.FromRgb(0xE5, 0x39, 0x35), Color.FromRgb(0xFF, 0x98, 0x00),
            Color.FromRgb(0x8E, 0x24, 0xAA), Color.FromRgb(0x00, 0xBC, 0xD4),
            Color.FromRgb(0x79, 0x55, 0x48), Color.FromRgb(0x60, 0x7D, 0x8B),
        };

        public GeoProcessToolView()
        {
            InitializeComponent();
        }

        /// <summary>设置图层服务引用，用于获取已加载图层列表</summary>
        public void SetLayerService(MapLayerService layerService)
        {
            _layerService = layerService;
        }

        /// <summary>加载分析工具模块</summary>
        public void LoadTool(ModuleInfo module)
        {
            _module = module;
            _lastResults = null;
            ResultGrid.ItemsSource = null;
            ChartContainer.Visibility = Visibility.Collapsed;
            CfSummaryContainer.Visibility = Visibility.Collapsed;
            ProgressText.Visibility = Visibility.Collapsed;

            bool isPlaceholder = module.Parameters.Length == 0;

            if (isPlaceholder)
            {
                ProgressText.Text = $"「{module.Name}」工具开发中...";
                ProgressText.Visibility = Visibility.Visible;
                return;
            }

            if (!string.IsNullOrEmpty(module.Description))
            {
                ToolDesc.Text = module.Description;
                ToolDesc.Visibility = Visibility.Visible;
            }

            // 刷新图层列表
            RefreshLayerList();

            // 填充分析方法
            ModelMethod.Items.Clear();
            foreach (var m in module.Methods)
                ModelMethod.Items.Add(new ComboBoxItem { Content = m, IsSelected = m.Contains("CF") });
        }

        /// <summary>刷新图层下拉列表</summary>
        public void RefreshLayerList()
        {
            if (_layerService == null) return;

            int prevClassIdx = ClassLayerCombo.SelectedIndex;
            int prevHazardIdx = HazardLayerCombo.SelectedIndex;

            ClassLayerCombo.Items.Clear();
            HazardLayerCombo.Items.Clear();

            ClassLayerCombo.Items.Add("-- 选择分类面图层 --");
            HazardLayerCombo.Items.Add("-- 选择灾害点图层 --");

            foreach (var layer in _layerService.Layers)
            {
                if (layer.Type == SpatialDataType.Vector)
                {
                    string label = $"{layer.Name} [{layer.FilePath}]";
                    // 面图层添加到分类列表，点图层添加到灾害列表
                    ClassLayerCombo.Items.Add(label);
                    HazardLayerCombo.Items.Add(label);
                }
            }

            if (prevClassIdx > 0 && prevClassIdx < ClassLayerCombo.Items.Count)
                ClassLayerCombo.SelectedIndex = prevClassIdx;
            else
                ClassLayerCombo.SelectedIndex = 0;

            if (prevHazardIdx > 0 && prevHazardIdx < HazardLayerCombo.Items.Count)
                HazardLayerCombo.SelectedIndex = prevHazardIdx;
            else
                HazardLayerCombo.SelectedIndex = 0;
        }

        /// <summary>从ComboBox获取实际选中的矢量图层（跳过占位符和栅格图层索引偏差）</summary>
        private MapLayer? GetSelectedVectorLayer(ComboBox combo)
        {
            int idx = combo.SelectedIndex - 1; // 减去"-- 选择 --"占位符
            if (idx < 0 || _layerService == null) return null;
            var vectorLayers = _layerService.Layers.Where(l => l.Type == SpatialDataType.Vector).ToList();
            if (idx >= vectorLayers.Count) return null;
            return vectorLayers[idx];
        }

        private void LayerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 当分类图层切换时，实时检测可用字段
            _ = RefreshFieldListAsync();
        }

        private async Task RefreshFieldListAsync()
        {
            ClassFieldCombo.Items.Clear();
            ClassFieldCombo.Items.Add("（自动检测）");

            var layer = GetSelectedVectorLayer(ClassLayerCombo);
            if (layer == null) { ClassFieldCombo.SelectedIndex = 0; return; }

            try
            {
                var table = await Esri.ArcGISRuntime.Data.ShapefileFeatureTable.OpenAsync(layer.FilePath);
                var fields = table.Fields;
                foreach (var f in fields)
                {
                    // 实时识别所有可用字段类型：文本、整数、浮点、日期等
                    var ft = f.FieldType;
                    if (ft == Esri.ArcGISRuntime.Data.FieldType.Text ||
                        ft == Esri.ArcGISRuntime.Data.FieldType.Int16 ||
                        ft == Esri.ArcGISRuntime.Data.FieldType.Int32 ||
                        ft == Esri.ArcGISRuntime.Data.FieldType.Float32 ||
                        ft == Esri.ArcGISRuntime.Data.FieldType.Float64 ||
                        ft == Esri.ArcGISRuntime.Data.FieldType.Date ||
                        ft == Esri.ArcGISRuntime.Data.FieldType.OID)
                    {
                        ClassFieldCombo.Items.Add(f.Name);
                    }
                }
            }
            catch { }
            ClassFieldCombo.SelectedIndex = 0;
        }

        #region 事件

        private async void RunAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (_layerService == null || _layerService.Layers.Count == 0)
            {
                MessageBox.Show("请先将数据图层加载到地图中。\n\n" +
                    "步骤：在左侧数据面板中双击 .shp 文件添加图层到地图。",
                    "无可用数据", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var classLayer = GetSelectedVectorLayer(ClassLayerCombo);
            var hazardLayer = GetSelectedVectorLayer(HazardLayerCombo);

            if (classLayer == null || hazardLayer == null)
            {
                MessageBox.Show("请选择分类面图层和灾害点图层。",
                    "未选择图层", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (classLayer == hazardLayer)
            {
                MessageBox.Show("分类图层和灾害点图层不能相同。",
                    "图层相同", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 确定分类字段
            string classField = "";
            if (ClassFieldCombo.SelectedIndex > 0) // 不是"自动检测"
            {
                classField = ClassFieldCombo.SelectedItem?.ToString() ?? "";
            }

            // 读取分级分区设置
            var classMethod = ClassificationMethod.None;
            int classCount = 5;
            if (ClassMethod.SelectedIndex > 0) // 0 = "-- 不分类（按原值） --"
            {
                classMethod = ClassMethod.SelectedIndex switch
                {
                    1 => ClassificationMethod.NaturalBreaks,
                    2 => ClassificationMethod.EqualInterval,
                    3 => ClassificationMethod.Quantile,
                    4 => ClassificationMethod.StandardDeviation,
                    _ => ClassificationMethod.None
                };
                classCount = int.Parse((ClassCountCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "5");
            }

            var config = new AnalysisConfig
            {
                ModuleName = _module.Name,
                OutputFolder = OutputFolder.Text,
                ModelMethod = (ModelMethod.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "CF值法"
            };

            ButtonBar.IsEnabled = false;
            ProgressText.Visibility = Visibility.Visible;
            ProgressText.Text = "⏳ 正在读取数据...";
            ResultGrid.ItemsSource = null;

            try
            {
                _lastResults = await _service.RunAnalysisAsync(
                    classLayer, hazardLayer, classField, config,
                    classMethod, classCount,
                    msg => Dispatcher.Invoke(() => ProgressText.Text = $"⏳ {msg}"));

                if (_lastResults.Count == 0)
                {
                    ProgressText.Text = "⚠ 分析无结果。请检查图层数据是否有效。";
                    ButtonBar.IsEnabled = true;
                    return;
                }

                ResultGrid.ItemsSource = new ObservableCollection<StatResult>(_lastResults);
                ResultLabel.Text = $"📊 分析结果（{_lastResults.Count} 个分类）";
                ProgressText.Text = $"✅ 分析完成：{_lastResults.Sum(r => r.HazardCount)} 个灾害点，CF范围 [{_lastResults.Min(r => r.CF):F3}, {_lastResults.Max(r => r.CF):F3}]";

                DrawBarChart(_lastResults);
                ShowCfSummary(_lastResults);
            }
            catch (Exception ex)
            {
                ProgressText.Text = $"❌ 分析失败: {ex.Message}";
            }
            finally
            {
                ButtonBar.IsEnabled = true;
            }
        }

        private void SaveResults_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResults == null || _lastResults.Count == 0)
            {
                MessageBox.Show("请先运行分析。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var classLayer = GetSelectedVectorLayer(ClassLayerCombo);
            var hazardLayer = GetSelectedVectorLayer(HazardLayerCombo);
            string classPath = classLayer?.FilePath ?? "";
            string hazardPath = hazardLayer?.FilePath ?? "";

            _service.SaveResults(OutputFolder.Text, _module.Name, _lastResults, classPath, hazardPath);
            MessageBox.Show($"✅ 结果已保存至:\n{OutputFolder.Text}", "保存成功",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshLayerList();
            await RefreshFieldListAsync();
            ProgressText.Text = "✅ 图层列表和字段已刷新";
            ProgressText.Visibility = Visibility.Visible;
        }

        private void ClassMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 防护：XAML初始化时ClassCountCombo可能尚未创建
            if (ClassCountCombo == null) return;
            bool enabled = ClassMethod.SelectedIndex > 0;
            ClassCountCombo.IsEnabled = enabled;
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog { Title = "选择输出目录" };
            if (dlg.ShowDialog() == true)
                OutputFolder.Text = dlg.FolderName;
        }

        #endregion

        #region 柱状图

        private void DrawBarChart(List<StatResult> results)
        {
            ChartCanvas.Children.Clear();
            ChartContainer.Visibility = Visibility.Visible;

            double canvasW = 340, canvasH = 150;
            double marginLeft = 40, marginRight = 10, marginTop = 10, marginBottom = 30;
            double plotW = canvasW - marginLeft - marginRight;
            double plotH = canvasH - marginTop - marginBottom;

            int n = Math.Min(results.Count, 20);
            var displayResults = results.Take(n).ToList();
            double maxVal = displayResults.Max(r => (double)r.HazardCount);
            maxVal = Math.Ceiling(maxVal / 10) * 10;
            if (maxVal <= 0) maxVal = 100;

            // Y轴网格
            for (int i = 0; i <= 4; i++)
            {
                double yVal = maxVal * i / 4;
                double y = marginTop + plotH - (plotH * i / 4);
                ChartCanvas.Children.Add(new Line
                {
                    X1 = marginLeft, Y1 = y, X2 = marginLeft + plotW, Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8)), StrokeThickness = 0.5
                });
                var yLabel = new TextBlock { Text = yVal.ToString("F0"), FontSize = 7, Foreground = Brushes.Gray };
                Canvas.SetLeft(yLabel, 0); Canvas.SetTop(yLabel, y - 6);
                ChartCanvas.Children.Add(yLabel);
            }

            // X轴
            ChartCanvas.Children.Add(new Line
            {
                X1 = marginLeft, Y1 = marginTop + plotH,
                X2 = marginLeft + plotW, Y2 = marginTop + plotH,
                Stroke = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)), StrokeThickness = 1
            });

            double groupW = plotW / n;
            double barW = Math.Max(4, groupW * 0.7);

            for (int i = 0; i < n; i++)
            {
                var r = displayResults[i];
                double barH = Math.Max(1, (r.HazardCount / maxVal) * plotH);
                double x = marginLeft + i * groupW + (groupW - barW) / 2;
                double y = marginTop + plotH - barH;

                var color = BarColors[i % BarColors.Length];
                ChartCanvas.Children.Add(new System.Windows.Shapes.Rectangle
                {
                    Width = barW, Height = barH,
                    Fill = new SolidColorBrush(color), RadiusX = 1, RadiusY = 1
                });
                Canvas.SetLeft(ChartCanvas.Children[^1], x);
                Canvas.SetTop(ChartCanvas.Children[^1], y);

                // X标签
                string label = r.ClassName.Length > 5 ? r.ClassName[..5] : r.ClassName;
                var xLabel = new TextBlock { Text = label, FontSize = 6, Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)) };
                Canvas.SetLeft(xLabel, x); Canvas.SetTop(xLabel, marginTop + plotH + 2);
                ChartCanvas.Children.Add(xLabel);
            }
        }

        #endregion

        #region CF汇总

        private void ShowCfSummary(List<StatResult> results)
        {
            CfSummaryPanel.Children.Clear();
            CfSummaryContainer.Visibility = Visibility.Visible;

            var groups = results.GroupBy(r => r.ParameterName).ToList();
            if (groups.Count == 0) groups.Add(results.GroupBy(r => r.ParameterName).First());

            foreach (var group in groups)
            {
                var list = group.ToList();
                double cfMean = Math.Round(list.Average(r => r.CF), 4);
                int vlow = list.Count(r => r.CF < -0.5), low = list.Count(r => r.CF >= -0.5 && r.CF < 0);
                int mid = list.Count(r => r.CF >= 0 && r.CF < 0.3), high = list.Count(r => r.CF >= 0.3 && r.CF < 0.6);
                int vhigh = list.Count(r => r.CF >= 0.6);

                var sp = new StackPanel { Margin = new Thickness(0, 2, 0, 4) };
                sp.Children.Add(new TextBlock
                {
                    Text = $"📌 {group.Key}  |  CF均值: {cfMean:F4}  |  灾害总数: {list.Sum(r => r.HazardCount)}",
                    FontSize = 10, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33))
                });

                var barSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
                AddCfBar(barSp, "极低", vlow, list.Count, "#2E7D32");
                AddCfBar(barSp, "低", low, list.Count, "#66BB6A");
                AddCfBar(barSp, "中等", mid, list.Count, "#FFEB3B");
                AddCfBar(barSp, "高", high, list.Count, "#FF9800");
                AddCfBar(barSp, "极高", vhigh, list.Count, "#E53935");
                sp.Children.Add(barSp);
                CfSummaryPanel.Children.Add(sp);
            }
        }

        private static void AddCfBar(StackPanel parent, string label, int count, int total, string hex)
        {
            if (count == 0) return;
            double pct = (double)count / total * 100;
            var c = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 8, 0) };
            c.Children.Add(new TextBlock { Text = $"{pct:F0}%", FontSize = 8, Foreground = Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Center });
            c.Children.Add(new Border { Width = 22, Height = Math.Max(4, pct * 0.3), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!), CornerRadius = new CornerRadius(2) });
            c.Children.Add(new TextBlock { Text = $"{label}({count})", FontSize = 7, Foreground = Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Center });
            parent.Children.Add(c);
        }

        #endregion
    }
}
