using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using cztApp1.Services;
using Esri.ArcGISRuntime.Data;

namespace cztApp1.Views.Tools
{
    public partial class AttributeTableView : UserControl
    {
        private MapLayerService? _layerService;
        private DataTable? _currentTable;

        public AttributeTableView()
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
                LayerCombo.Items.Add($"{layer.Name} [{Path.GetFileName(layer.FilePath)}]");
            LayerCombo.SelectedIndex = 0;
        }

        private async void LayerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int idx = LayerCombo.SelectedIndex - 1;
            if (idx < 0 || _layerService == null) { AttrGrid.ItemsSource = null; return; }

            var layers = _layerService.Layers.ToList();
            if (idx >= layers.Count) return;

            var layer = layers[idx];
            StatusText.Text = "⏳ 读取中...";

            try
            {
                var table = await ShapefileFeatureTable.OpenAsync(layer.FilePath);
                var features = await table.QueryFeaturesAsync(new QueryParameters { WhereClause = "1=1" });
                var fields = table.Fields;

                // 构建 DataTable
                var dt = new DataTable();
                foreach (var f in fields)
                    dt.Columns.Add(f.Name, typeof(string));

                foreach (var feat in features)
                {
                    var row = dt.NewRow();
                    foreach (var f in fields)
                        row[f.Name] = feat.Attributes.ContainsKey(f.Name) ? feat.Attributes[f.Name]?.ToString() ?? "" : "";
                    dt.Rows.Add(row);
                }

                _currentTable = dt;
                AttrGrid.ItemsSource = dt.DefaultView;
                AttrGrid.AutoGenerateColumns = true;
                InfoText.Text = $"共 {features.Count()} 条记录  |  {fields.Count} 个字段";
                StatusText.Text = "✅ 已加载";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ {ex.Message}";
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshLayerList();

        private void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            if (AttrGrid.SelectedCells.Count == 0) return;
            var sb = new StringBuilder();
            foreach (var cell in AttrGrid.SelectedCells)
                sb.Append(cell.Column?.Header).Append(": ").AppendLine(cell.Item?.ToString());
            Clipboard.SetText(sb.ToString());
            StatusText.Text = $"已复制 {AttrGrid.SelectedCells.Count} 个单元格";
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTable == null || _currentTable.Rows.Count == 0) return;

            var dlg = new Microsoft.Win32.SaveFileDialog
            { Title = "导出CSV", Filter = "CSV文件|*.csv", FileName = "属性表.csv" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                using var sw = new StreamWriter(dlg.FileName, false, Encoding.UTF8);
                var cols = _currentTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
                sw.WriteLine(string.Join(",", cols));
                foreach (DataRow row in _currentTable.Rows)
                    sw.WriteLine(string.Join(",", cols.Select(c => row[c]?.ToString()?.Replace(",", "，") ?? "")));
                StatusText.Text = $"✅ 已导出 {_currentTable.Rows.Count} 行到 {Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex) { StatusText.Text = $"❌ {ex.Message}"; }
        }
    }
}
