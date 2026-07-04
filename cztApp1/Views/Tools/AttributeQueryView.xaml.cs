using System.Data;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using cztApp1.Services;
using Esri.ArcGISRuntime.Data;

namespace cztApp1.Views.Tools
{
    public partial class AttributeQueryView : UserControl
    {
        private MapLayerService? _layerService;
        private DataTable? _resultTable;

        public AttributeQueryView() => InitializeComponent();

        public void SetLayerService(MapLayerService s) { _layerService = s; Refresh(); }
        public void Refresh()
        {
            LayerCombo.Items.Clear();
            LayerCombo.Items.Add("-- 选择图层 --");
            if (_layerService != null)
                foreach (var l in _layerService.Layers)
                    LayerCombo.Items.Add($"{l.Name} [{Path.GetFileName(l.FilePath)}]");
            LayerCombo.SelectedIndex = 0;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            int prevIdx = LayerCombo.SelectedIndex;
            Refresh();
            if (prevIdx > 0 && prevIdx < LayerCombo.Items.Count)
                LayerCombo.SelectedIndex = prevIdx;
        }

        private async void LayerCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            FieldCombo.Items.Clear();
            int idx = LayerCombo.SelectedIndex - 1;
            if (idx < 0 || _layerService == null) return;
            var layers = _layerService.Layers.ToList();
            if (idx >= layers.Count) return;

            try
            {
                var table = await ShapefileFeatureTable.OpenAsync(layers[idx].FilePath);
                foreach (var f in table.Fields)
                    FieldCombo.Items.Add(new ComboBoxItem { Content = f.Name });
                if (FieldCombo.Items.Count > 0) FieldCombo.SelectedIndex = 0;
            }
            catch { }
        }

        private async void RunQuery_Click(object sender, RoutedEventArgs e)
        {
            int idx = LayerCombo.SelectedIndex - 1;
            if (idx < 0 || _layerService == null) return;
            var layers = _layerService.Layers.ToList();
            if (idx >= layers.Count) return;

            var field = (FieldCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            var op = (OperatorCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "=";
            var value = ValueBox.Text.Trim();
            if (string.IsNullOrEmpty(field)) { ResultCount.Text = "请选择字段"; return; }

            string where;
            if (op == "LIKE") where = $"{field} LIKE '%{value}%'";
            else if (op == "=" && double.TryParse(value, out _)) where = $"{field} = {value}";
            else where = $"{field} {op} '{value}'";

            try
            {
                var table = await ShapefileFeatureTable.OpenAsync(layers[idx].FilePath);
                var features = await table.QueryFeaturesAsync(new QueryParameters { WhereClause = where });
                var list = features.ToList();

                var dt = new DataTable();
                foreach (var f in table.Fields) dt.Columns.Add(f.Name, typeof(string));
                foreach (var feat in list)
                {
                    var row = dt.NewRow();
                    foreach (var f in table.Fields)
                        row[f.Name] = feat.Attributes.ContainsKey(f.Name) ? feat.Attributes[f.Name]?.ToString() ?? "" : "";
                    dt.Rows.Add(row);
                }
                _resultTable = dt;
                ResultGrid.ItemsSource = dt.DefaultView;
                ResultCount.Text = $"✅ 查询到 {list.Count} 条记录";
            }
            catch (Exception ex) { ResultCount.Text = $"❌ {ex.Message}"; }
        }

        private void ClearQuery_Click(object sender, RoutedEventArgs e)
        {
            ValueBox.Text = ""; ResultGrid.ItemsSource = null; ResultCount.Text = "";
        }

        private void ExportResult_Click(object sender, RoutedEventArgs e)
        {
            if (_resultTable == null || _resultTable.Rows.Count == 0) return;
            var dlg = new Microsoft.Win32.SaveFileDialog { Title = "导出CSV", Filter = "CSV|*.csv", FileName = "查询结果.csv" };
            if (dlg.ShowDialog() != true) return;
            using var sw = new StreamWriter(dlg.FileName, false, Encoding.UTF8);
            var cols = _resultTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
            sw.WriteLine(string.Join(",", cols));
            foreach (DataRow row in _resultTable.Rows)
                sw.WriteLine(string.Join(",", cols.Select(c => row[c]?.ToString()?.Replace(",", "，") ?? "")));
            ResultCount.Text = $"✅ 已导出 {_resultTable.Rows.Count} 行";
        }
    }
}
