using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using cztApp1.Models;
using cztApp1.Services;
using Esri.ArcGISRuntime.Data;

namespace cztApp1.Views.Tools
{
    public partial class AttributeManageView : UserControl
    {
        private MapLayerService? _layerService;
        private DataTable? _currentTable;
        private ShapefileFeatureTable? _featureTable;
        private List<Esri.ArcGISRuntime.Data.Feature>? _features;
        private bool _hasChanges;

        public AttributeManageView()
        {
            InitializeComponent();
        }

        public void SetLayerService(MapLayerService service)
        {
            _layerService = service;
            RefreshLayerList();
        }

        public void RefreshLayerList()
        {
            LayerCombo.Items.Clear();
            LayerCombo.Items.Add("-- 选择图层 --");
            if (_layerService == null) { LayerCombo.SelectedIndex = 0; return; }
            foreach (var layer in _layerService.Layers)
                if (layer.Type == SpatialDataType.Vector)
                    LayerCombo.Items.Add($"{layer.Name} [{Path.GetFileName(layer.FilePath)}]");
            LayerCombo.SelectedIndex = 0;
        }

        /// <summary>根据文件路径自动选中图层（供右键菜单调用）</summary>
        public void SelectLayerByFilePath(string filePath)
        {
            if (_layerService == null) return;
            for (int i = 0; i < LayerCombo.Items.Count; i++)
            {
                var item = LayerCombo.Items[i]?.ToString() ?? "";
                if (item.Contains(filePath)) { LayerCombo.SelectedIndex = i; return; }
            }
            RefreshLayerList();
            for (int i = 0; i < LayerCombo.Items.Count; i++)
            {
                var item = LayerCombo.Items[i]?.ToString() ?? "";
                if (item.Contains(filePath)) { LayerCombo.SelectedIndex = i; return; }
            }
        }

        private async void LayerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int idx = LayerCombo.SelectedIndex - 1;
            if (idx < 0 || _layerService == null) { AttrGrid.ItemsSource = null; return; }

            var vectorLayers = _layerService.Layers.Where(l => l.Type == SpatialDataType.Vector).ToList();
            if (idx >= vectorLayers.Count) return;

            var layer = vectorLayers[idx];
            _hasChanges = false;
            StatusText.Text = "⏳ 读取中...";

            try
            {
                _featureTable = await ShapefileFeatureTable.OpenAsync(layer.FilePath);
                _features = (await _featureTable.QueryFeaturesAsync(new QueryParameters { WhereClause = "1=1" })).ToList();
                var fields = _featureTable.Fields;

                var dt = new DataTable();
                foreach (var f in fields)
                    dt.Columns.Add(f.Name, typeof(string));

                foreach (var feat in _features)
                {
                    var row = dt.NewRow();
                    foreach (var f in fields)
                        row[f.Name] = feat.Attributes.ContainsKey(f.Name) ? feat.Attributes[f.Name]?.ToString() ?? "" : "";
                    dt.Rows.Add(row);
                }

                dt.AcceptChanges(); // 标记所有行为未修改
                _currentTable = dt;
                AttrGrid.ItemsSource = dt.DefaultView;
                InfoText.Text = $"共 {_features.Count} 条记录  |  {fields.Count} 个字段  |  双击单元格编辑";
                StatusText.Text = "✅ 已加载（可编辑）";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ {ex.Message}";
            }
        }

        private void AttrGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            _hasChanges = true;
            StatusText.Text = "📝 有未保存的修改";
        }

        private void AttrGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            _hasChanges = true;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!_hasChanges || _currentTable == null || _featureTable == null || _features == null)
            {
                StatusText.Text = "没有需要保存的修改";
                return;
            }

            StatusText.Text = "⏳ 正在保存...";
            try
            {
                var fields = _featureTable.Fields;
                int updated = 0;

                for (int i = 0; i < _currentTable.Rows.Count && i < _features.Count; i++)
                {
                    var row = _currentTable.Rows[i];
                    if (row.RowState == DataRowState.Unchanged) continue;

                    var feature = _features[i];
                    foreach (var f in fields)
                    {
                        var newVal = row[f.Name]?.ToString() ?? "";
                        var oldVal = feature.Attributes.ContainsKey(f.Name) ? feature.Attributes[f.Name]?.ToString() ?? "" : "";
                        if (newVal != oldVal)
                            feature.Attributes[f.Name] = newVal;
                    }
                    await _featureTable.UpdateFeatureAsync(feature);
                    updated++;
                }

                _currentTable.AcceptChanges();
                _hasChanges = false;
                StatusText.Text = $"✅ 已保存 {updated} 条记录";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ 保存失败: {ex.Message}";
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTable == null) return;
            _currentTable.RejectChanges();
            _hasChanges = false;
            StatusText.Text = "↩ 已撤销所有未保存修改";
        }

        private async void AddField_Click(object sender, RoutedEventArgs e)
        {
            if (_featureTable == null || _currentTable == null) return;

            var dialog = new InputDialog("添加字段", "请输入新字段名:");
            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FieldName)) return;

            string fieldName = dialog.FieldName.Trim();
            if (_currentTable.Columns.Contains(fieldName))
            {
                StatusText.Text = $"字段 '{fieldName}' 已存在";
                return;
            }

            _currentTable.Columns.Add(fieldName, typeof(string));
            // 初始化所有行为空字符串
            foreach (DataRow row in _currentTable.Rows)
                row[fieldName] = "";
            _hasChanges = true;
            StatusText.Text = $"✅ 已添加字段 '{fieldName}'，请编辑后保存";
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            int prevIdx = LayerCombo.SelectedIndex;
            RefreshLayerList();
            if (prevIdx > 0 && prevIdx < LayerCombo.Items.Count)
                LayerCombo.SelectedIndex = prevIdx;
        }
    }

    /// <summary>
    /// 简单的输入对话框
    /// </summary>
    public class InputDialog : Window
    {
        public string FieldName { get; private set; } = "";

        public InputDialog(string title, string prompt)
        {
            Title = title;
            Width = 320; Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.ToolWindow;
            ResizeMode = ResizeMode.NoResize;
            FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI");

            var grid = new Grid { Margin = new Thickness(12) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

            var label = new TextBlock { Text = prompt, FontSize = 12, Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(label, 0); grid.Children.Add(label);

            var textBox = new TextBox { FontSize = 12, Height = 26 };
            Grid.SetRow(textBox, 1); grid.Children.Add(textBox);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            var okBtn = new Button
            {
                Content = "确定", Width = 70, Height = 26, FontSize = 12,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x15, 0x65, 0xC0)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand
            };
            okBtn.Click += (_, _) => { FieldName = textBox.Text; DialogResult = true; Close(); };
            var cancelBtn = new Button
            {
                Content = "取消", Width = 70, Height = 26, FontSize = 12,
                Margin = new Thickness(6, 0, 0, 0),
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xE8, 0xE8, 0xE8)),
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand
            };
            cancelBtn.Click += (_, _) => { DialogResult = false; Close(); };
            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);
            Grid.SetRow(btnPanel, 2); grid.Children.Add(btnPanel);

            Content = grid;
            textBox.Focus();
        }
    }
}
