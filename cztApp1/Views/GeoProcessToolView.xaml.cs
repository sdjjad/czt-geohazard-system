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
        private readonly GeoAnalysisService _service = new();
        private ModuleInfo _module = null!;
        private List<GeoParameter> _params = new();
        private List<StatResult>? _lastResults;
        private string? _lastSavedBasePath;

        // 柱状图配色
        private static readonly Color[] BarColors =
        {
            Color.FromRgb(0x15, 0x65, 0xC0), // 蓝
            Color.FromRgb(0x43, 0xA0, 0x47), // 绿
            Color.FromRgb(0xE5, 0x39, 0x35), // 红
            Color.FromRgb(0xFF, 0x98, 0x00), // 橙
            Color.FromRgb(0x8E, 0x24, 0xAA), // 紫
            Color.FromRgb(0x00, 0xBC, 0xD4), // 青
            Color.FromRgb(0x79, 0x55, 0x48), // 棕
            Color.FromRgb(0x60, 0x7D, 0x8B), // 灰蓝
        };

        public GeoProcessToolView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 加载分析工具模块到面板
        /// </summary>
        public void LoadTool(ModuleInfo module)
        {
            _module = module;
            _lastResults = null;
            _lastSavedBasePath = null;
            ResultGrid.ItemsSource = null;
            ChartContainer.Visibility = Visibility.Collapsed;
            CfSummaryContainer.Visibility = Visibility.Collapsed;
            OpenReportBtn.IsEnabled = false;
            ResultSummary.Text = "";
            ProgressText.Visibility = Visibility.Collapsed;

            bool isPlaceholder = module.Parameters.Length == 0;

            // 显示/隐藏分析相关UI
            AnalysisSection.Visibility = isPlaceholder ? Visibility.Collapsed : Visibility.Visible;
            ParamSection.Visibility = isPlaceholder ? Visibility.Collapsed : Visibility.Visible;
            ButtonBar.Visibility = isPlaceholder ? Visibility.Collapsed : Visibility.Visible;
            ResultSection.Visibility = isPlaceholder ? Visibility.Collapsed : Visibility.Visible;
            PlaceholderMsg.Visibility = isPlaceholder ? Visibility.Visible : Visibility.Collapsed;

            if (isPlaceholder)
            {
                PlaceholderMsg.Text = $"「{module.Name}」工具开发中...";
                return;
            }

            // 显示工具描述
            if (!string.IsNullOrEmpty(module.Description))
            {
                ToolDesc.Text = module.Description;
                ToolDesc.Visibility = Visibility.Visible;
            }
            else
            {
                ToolDesc.Visibility = Visibility.Collapsed;
            }

            // 填充分析方法
            ModelMethod.Items.Clear();
            foreach (var m in module.Methods)
                ModelMethod.Items.Add(new ComboBoxItem { Content = m, IsSelected = m == "CF值法" });
            if (ModelMethod.Items.Count > 0 && ModelMethod.SelectedItem == null)
                ((ComboBoxItem)ModelMethod.Items[0]).IsSelected = true;

            // 填充分级方法（使用模块定义的分级方法，或使用默认）
            ClassificationMethod.Items.Clear();
            var classMethods = module.Classifications.Length > 0
                ? module.Classifications
                : new[] { "自然断点法", "等间距法", "分位数法", "标准偏差法", "手动分级" };
            foreach (var cm in classMethods)
                ClassificationMethod.Items.Add(new ComboBoxItem
                {
                    Content = cm,
                    IsSelected = cm == "自然断点法"
                });

            // 填充指标参数
            _params = module.Parameters.Select(p => new GeoParameter
            {
                Name = p,
                IsSelected = true,
                ClassCount = 5,
                Classification = "自然断点法"
            }).ToList();
            ParameterList.ItemsSource = _params;
        }

        #region 事件处理

        private void DataRange_Changed(object sender, SelectionChangedEventArgs e)
        {
            // 可扩展：根据范围切换联动其他选项
        }

        private void ClassificationMethod_Changed(object sender, SelectionChangedEventArgs e)
        {
            var method = (ClassificationMethod.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "自然断点法";
            foreach (var p in _params)
                p.Classification = method;
        }

        private async void RunAnalysis_Click(object sender, RoutedEventArgs e)
        {
            // 验证至少选中一个指标
            if (_params.All(p => !p.IsSelected))
            {
                MessageBox.Show("请至少选择一个指标参数", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 构建分析配置
            var config = new AnalysisConfig
            {
                ModuleName = _module.Name,
                DataSource = (DataSource.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "本地文件",
                DataRange = (DataRange.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "长株潭全域",
                DataTime = (DataTime.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "2020年",
                ModelMethod = (ModelMethod.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "CF值法",
                ClassificationMethod = (ClassificationMethod.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "自然断点法",
                OutputFolder = OutputFolder.Text,
                Parameters = _params
            };

            // 显示进度
            ProgressText.Text = "⏳ 正在分析...";
            ProgressText.Visibility = Visibility.Visible;
            ButtonBar.IsEnabled = false;
            ResultGrid.ItemsSource = null;

            // 在后台线程运行分析
            _lastResults = await Task.Run(() =>
                _service.RunAnalysis(config, msg =>
                    Dispatcher.Invoke(() => ProgressText.Text = $"⏳ {msg}")));

            // 更新UI
            ProgressText.Visibility = Visibility.Collapsed;
            ButtonBar.IsEnabled = true;

            if (_lastResults.Count == 0)
            {
                ResultSummary.Text = "无结果";
                return;
            }

            // 绑定结果表格
            var groups = _lastResults.GroupBy(r => r.ParameterName).ToList();
            ResultGrid.ItemsSource = new ObservableCollection<StatResult>(_lastResults);
            ResultLabel.Text = $"📊 分析结果（{groups.Count} 个指标，{_lastResults.Count} 条记录）";

            int totalHazards = _lastResults.Sum(r => r.HazardCount);
            double cfMax = _lastResults.Max(r => r.CF);
            int highRiskCount = _lastResults.Count(r => r.CF >= 0.3);
            ResultSummary.Text = $"灾害总数: {totalHazards} | CF范围: [{_lastResults.Min(r => r.CF):F3}, {cfMax:F3}] | 高易发分类: {highRiskCount}";

            // 绘制柱状图
            DrawBarChart(_lastResults);

            // 显示CF汇总
            ShowCfSummary(groups);

            // 更新打开报告按钮状态（先保存才能打开）
            OpenReportBtn.IsEnabled = false;
        }

        private void SaveResults_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResults == null || _lastResults.Count == 0)
            {
                MessageBox.Show("请先运行分析", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var folder = OutputFolder.Text;
            _service.SaveResults(folder, _module.Name, _lastResults);

            // 记录最后一次保存的基础路径（不含扩展名）
            string safeName = _module.Name.Replace(" ", "").Replace("（", "(").Replace("）", ")");
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _lastSavedBasePath = IoPath.Combine(folder, $"{safeName}_{timestamp}");

            OpenReportBtn.IsEnabled = true;

            MessageBox.Show(
                $"✅ 结果已保存至:\n{folder}\n\n" +
                $"生成文件:\n" +
                $"  • 统计表.csv\n" +
                $"  • CF分级汇总.csv\n" +
                $"  • 元数据.json\n" +
                $"  • 分析报告.html",
                "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenReport_Click(object sender, RoutedEventArgs e)
        {
            if (_lastSavedBasePath == null) return;

            var reportPath = $"{_lastSavedBasePath}_分析报告.html";
            if (File.Exists(reportPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = reportPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法打开报告: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show($"报告文件不存在:\n{reportPath}", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog { Title = "选择输出目录" };
            if (dlg.ShowDialog() == true)
                OutputFolder.Text = dlg.FolderName;
        }

        #endregion

        #region 柱状图绘制

        /// <summary>
        /// 在Canvas上绘制分组柱状图
        /// </summary>
        private void DrawBarChart(List<StatResult> results)
        {
            ChartCanvas.Children.Clear();
            ChartContainer.Visibility = Visibility.Visible;

            var groups = results.GroupBy(r => r.ParameterName).ToList();
            var allResults = results.ToList();

            double canvasW = ChartContainer.ActualWidth > 10 ? ChartContainer.ActualWidth - 10 : 340;
            double canvasH = ChartContainer.ActualHeight > 10 ? ChartContainer.ActualHeight - 10 : 170;

            if (canvasW <= 0 || canvasH <= 0) return;

            double marginLeft = 40;
            double marginRight = 10;
            double marginTop = 15;
            double marginBottom = 25;
            double plotW = canvasW - marginLeft - marginRight;
            double plotH = canvasH - marginTop - marginBottom;

            if (plotW <= 0 || plotH <= 0) return;

            // 按参数分组，每个分类一根柱子
            int totalBars = allResults.Count;
            if (totalBars == 0) return;

            double groupW = plotW / totalBars;
            double barW = Math.Max(6, groupW * 0.7);
            double barGap = groupW * 0.3;

            // 找到最大值（用于缩放）
            double maxVal = allResults.Max(r => (double)r.HazardCount);
            maxVal = Math.Ceiling(maxVal / 50) * 50; // 向上取整到50的倍数
            if (maxVal <= 0) maxVal = 100;

            // Y轴网格线和刻度
            int yTickCount = 5;
            for (int i = 0; i <= yTickCount; i++)
            {
                double yVal = maxVal * i / yTickCount;
                double y = marginTop + plotH - (plotH * i / yTickCount);

                // 网格线
                var gridLine = new Line
                {
                    X1 = marginLeft, Y1 = y,
                    X2 = marginLeft + plotW, Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8)),
                    StrokeThickness = 0.5
                };
                ChartCanvas.Children.Add(gridLine);

                // Y刻度标签
                var yLabel = new TextBlock
                {
                    Text = yVal.ToString("F0"),
                    FontSize = 8,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
                    Tag = "yLabel"
                };
                Canvas.SetLeft(yLabel, 0);
                Canvas.SetTop(yLabel, y - 7);
                ChartCanvas.Children.Add(yLabel);
            }

            // X轴
            var xAxis = new Line
            {
                X1 = marginLeft, Y1 = marginTop + plotH,
                X2 = marginLeft + plotW, Y2 = marginTop + plotH,
                Stroke = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                StrokeThickness = 1
            };
            ChartCanvas.Children.Add(xAxis);

            // 绘制柱子
            int barIdx = 0;
            foreach (var group in groups)
            {
                foreach (var r in group)
                {
                    double barH = (r.HazardCount / maxVal) * plotH;
                    double x = marginLeft + barIdx * groupW + barGap / 2;
                    double y = marginTop + plotH - barH;

                    var color = BarColors[barIdx % BarColors.Length];

                    var bar = new System.Windows.Shapes.Rectangle
                    {
                        Width = barW,
                        Height = Math.Max(1, barH),
                        Fill = new SolidColorBrush(color),
                        RadiusX = 1, RadiusY = 1,
                        Tag = $"bar_{r.ClassName}"
                    };
                    Canvas.SetLeft(bar, x);
                    Canvas.SetTop(bar, y);
                    ChartCanvas.Children.Add(bar);

                    // 柱顶数值标签
                    if (barH > 14)
                    {
                        var valLabel = new TextBlock
                        {
                            Text = r.HazardCount.ToString(),
                            FontSize = 7,
                            Foreground = new SolidColorBrush(Colors.White),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Tag = "valLabel"
                        };
                        Canvas.SetLeft(valLabel, x + barW / 2 - 8);
                        Canvas.SetTop(valLabel, y + 2);
                        ChartCanvas.Children.Add(valLabel);
                    }

                    // X轴分类标签（每隔一个显示，避免重叠）
                    if (totalBars <= 20 || barIdx % 2 == 0)
                    {
                        var xLabel = new TextBlock
                        {
                            Text = r.ClassName.Length > 4 ? r.ClassName[..4] + ".." : r.ClassName,
                            FontSize = 7,
                            Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                            Tag = "xLabel",
                            RenderTransform = new RotateTransform(-30)
                        };
                        Canvas.SetLeft(xLabel, x + barW / 2 - 8);
                        Canvas.SetTop(xLabel, marginTop + plotH + 3);
                        ChartCanvas.Children.Add(xLabel);
                    }

                    barIdx++;
                }
            }
        }

        #endregion

        #region CF汇总

        private void ShowCfSummary(List<IGrouping<string, StatResult>> groups)
        {
            CfSummaryPanel.Children.Clear();
            CfSummaryContainer.Visibility = Visibility.Visible;

            foreach (var group in groups)
            {
                var list = group.ToList();
                double cfMean = Math.Round(list.Average(r => r.CF), 4);
                int vlow = list.Count(r => r.CF < -0.5);
                int low = list.Count(r => r.CF >= -0.5 && r.CF < 0);
                int mid = list.Count(r => r.CF >= 0 && r.CF < 0.3);
                int high = list.Count(r => r.CF >= 0.3 && r.CF < 0.6);
                int vhigh = list.Count(r => r.CF >= 0.6);

                var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 2, 0, 4) };
                sp.Children.Add(new TextBlock
                {
                    Text = $"📌 {group.Key}  |  CF均值: {cfMean:F4}",
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
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

        private static void AddCfBar(StackPanel parent, string label, int count, int total, string hexColor)
        {
            if (count == 0) return;
            var percent = (double)count / total * 100;

            var container = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 8, 0) };
            var pctText = new TextBlock
            {
                Text = $"{percent:F0}%",
                FontSize = 8,
                Foreground = new SolidColorBrush(Colors.DarkGray),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var bar = new Border
            {
                Width = 24,
                Height = Math.Max(4, percent * 0.4),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor)!),
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var lbl = new TextBlock
            {
                Text = $"{label}({count})",
                FontSize = 7,
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            container.Children.Add(pctText);
            container.Children.Add(bar);
            container.Children.Add(lbl);
            parent.Children.Add(container);
        }

        #endregion
    }
}
