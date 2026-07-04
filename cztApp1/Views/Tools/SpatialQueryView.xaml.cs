using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using cztApp1.Services;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;

namespace cztApp1.Views.Tools
{
    public partial class SpatialQueryView : UserControl
    {
        private MapLayerService? _layerService;

        public SpatialQueryView() => InitializeComponent();
        public void SetLayerService(MapLayerService s) { _layerService = s; Refresh(); }

        public void Refresh()
        {
            TargetLayerCombo.Items.Clear(); SourceLayerCombo.Items.Clear();
            TargetLayerCombo.Items.Add("-- 选择目标图层 --");
            SourceLayerCombo.Items.Add("-- 选择源图层 --");
            if (_layerService == null) { TargetLayerCombo.SelectedIndex = 0; SourceLayerCombo.SelectedIndex = 0; return; }
            foreach (var l in _layerService.Layers)
            {
                TargetLayerCombo.Items.Add($"{l.Name} [{Path.GetFileName(l.FilePath)}]");
                SourceLayerCombo.Items.Add($"{l.Name} [{Path.GetFileName(l.FilePath)}]");
            }
            TargetLayerCombo.SelectedIndex = 0; SourceLayerCombo.SelectedIndex = 0;
        }

        private async void RunSpatialQuery_Click(object sender, RoutedEventArgs e)
        {
            int tIdx = TargetLayerCombo.SelectedIndex - 1, sIdx = SourceLayerCombo.SelectedIndex - 1;
            if (tIdx < 0 || sIdx < 0 || _layerService == null) { SpatialResultText.Text = "请选择图层"; return; }
            var layers = _layerService.Layers.ToList();
            if (tIdx >= layers.Count || sIdx >= layers.Count) return;
            if (tIdx == sIdx) { SpatialResultText.Text = "目标和源图层不能相同"; return; }

            SpatialResultText.Text = "⏳ 空间查询中...";
            try
            {
                var targetTable = await ShapefileFeatureTable.OpenAsync(layers[tIdx].FilePath);
                var sourceTable = await ShapefileFeatureTable.OpenAsync(layers[sIdx].FilePath);
                var sourceFeatures = (await sourceTable.QueryFeaturesAsync(new QueryParameters { WhereClause = "1=1" })).ToList();
                var targetFeatures = (await targetTable.QueryFeaturesAsync(new QueryParameters { WhereClause = "1=1" })).ToList();

                var matched = new List<Esri.ArcGISRuntime.Data.Feature>();
                foreach (var tf in targetFeatures)
                {
                    if (tf.Geometry == null) continue;
                    foreach (var sf in sourceFeatures)
                    {
                        if (sf.Geometry == null) continue;
                        try
                        {
                            bool hit = RbContain.IsChecked == true
                                ? GeometryEngine.Contains(sf.Geometry, tf.Geometry)
                                : GeometryEngine.Intersects(sf.Geometry, tf.Geometry);
                            if (hit) { matched.Add(tf); break; }
                        }
                        catch { }
                    }
                }

                var dt = new DataTable();
                foreach (var f in targetTable.Fields) dt.Columns.Add(f.Name, typeof(string));
                foreach (var feat in matched)
                {
                    var row = dt.NewRow();
                    foreach (var f in targetTable.Fields)
                        row[f.Name] = feat.Attributes.ContainsKey(f.Name) ? feat.Attributes[f.Name]?.ToString() ?? "" : "";
                    dt.Rows.Add(row);
                }
                SpatialResultGrid.ItemsSource = dt.DefaultView;
                SpatialResultText.Text = $"✅ 空间查询完成：{matched.Count}/{targetFeatures.Count} 条匹配";
            }
            catch (Exception ex) { SpatialResultText.Text = $"❌ {ex.Message}"; }
        }
    }
}
