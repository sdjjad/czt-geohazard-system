using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using cztApp1.Services;
using Microsoft.Win32;

namespace cztApp1.Views
{
    public partial class ThematicMapToolView : UserControl
    {
        private ThematicMapService? _service;
        private string? _lastOutputPath;

        public ThematicMapToolView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 设置地图服务引用
        /// </summary>
        public void SetMapView(MapView mapView)
        {
            _service = new ThematicMapService(mapView);
        }

        /// <summary>
        /// 加载工具（根据toolName预设标题和模式）
        /// </summary>
        public void LoadTool(string toolName)
        {
            _lastOutputPath = null;
            StatusBorder.Visibility = Visibility.Collapsed;

            if (toolName == "StatChart")
            {
                MapTitle.Text = "地质灾害统计图";
                MapSubtitle.Text = "柱状图 / CF分布图 / 饼图 / 综合图";
            }
            else if (toolName == "StatTable")
            {
                MapTitle.Text = "地质灾害统计表";
                MapSubtitle.Text = "CSV统计表 / CF分级汇总 / 元数据";
            }
            else // ThematicMap or fallback
            {
                MapTitle.Text = "长株潭地质灾害专题图";
                MapSubtitle.Text = "土壤植被指标分析";
            }
        }

        #region 事件处理

        private async void GenerateMap_Click(object sender, RoutedEventArgs e)
        {
            if (_service == null)
            {
                ShowStatus("⚠ 地图未初始化，请先添加图层", "#E53935");
                return;
            }

            ButtonBarEnabled(false);
            ShowStatus("⏳ 正在生成专题图...", "#1565C0");

            try
            {
                var config = BuildConfig();
                _lastOutputPath = await Task.Run(() =>
                    _service.ExportThematicMapAsync(config, msg =>
                        Dispatcher.Invoke(() => ShowStatus($"⏳ {msg}", "#1565C0"))));

                ShowStatus($"✅ 专题图已生成！\n{_lastOutputPath}", "#43A047");

                // 自动打开
                if (File.Exists(_lastOutputPath))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = _lastOutputPath,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"❌ 生成失败: {ex.Message}", "#E53935");
            }
            finally
            {
                ButtonBarEnabled(true);
            }
        }

        private async void ExportLegend_Click(object sender, RoutedEventArgs e)
        {
            if (_service == null)
            {
                ShowStatus("⚠ 地图未初始化", "#E53935");
                return;
            }

            ButtonBarEnabled(false);
            ShowStatus("⏳ 正在导出图例...", "#1565C0");

            try
            {
                var config = BuildConfig();
                string legendPath = await _service.ExportLegendAsync(config, msg =>
                    Dispatcher.Invoke(() => ShowStatus($"⏳ {msg}", "#1565C0")));

                ShowStatus($"✅ 图例已导出！\n{legendPath}", "#43A047");

                if (File.Exists(legendPath))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = legendPath,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"❌ 导出失败: {ex.Message}", "#E53935");
            }
            finally
            {
                ButtonBarEnabled(true);
            }
        }

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
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开目录: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region 辅助方法

        private ThematicMapService.ThematicMapConfig BuildConfig()
        {
            return new ThematicMapService.ThematicMapConfig
            {
                Title = MapTitle.Text.Trim(),
                Subtitle = MapSubtitle.Text.Trim(),
                OutputFolder = OutputFolder.Text.Trim(),
                ImageWidth = int.Parse((ImageWidth.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "1200"),
                ImageHeight = int.Parse((ImageHeight.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "900"),
                IncludeLegend = IncludeLegend.IsChecked == true,
                IncludeScaleBar = IncludeScaleBar.IsChecked == true,
                IncludeNorthArrow = IncludeNorthArrow.IsChecked == true,
                IncludeGrid = IncludeGrid.IsChecked == true
            };
        }

        private void ShowStatus(string msg, string colorHex)
        {
            StatusText.Text = msg;
            StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex)!);
            StatusBorder.Visibility = Visibility.Visible;
        }

        private void ButtonBarEnabled(bool enabled)
        {
            BtnGenerateMap.IsEnabled = enabled;
            BtnExportLegend.IsEnabled = enabled;
        }

        #endregion
    }
}
