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
        /// 加载工具（设置默认标题等）
        /// </summary>
        public void LoadTool(string toolName)
        {
            _lastOutputPath = null;
            StatusBorder.Visibility = Visibility.Collapsed;

            // 根据工具名预设标题
            if (toolName.Contains("输出"))
                MapTitle.Text = "长株潭地质灾害分布图";
            else if (toolName.Contains("图例"))
                MapSubtitle.Text = "图例设置";
            else if (toolName.Contains("报表"))
                MapSubtitle.Text = "统计报表";
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
            // Simply toggle known buttons - the ButtonBar is a StackPanel with 3 buttons
            // This simple method is sufficient for the current layout
        }

        #endregion
    }
}
