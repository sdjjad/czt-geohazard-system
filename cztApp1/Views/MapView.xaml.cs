using System.Windows.Controls;
using System.Windows.Media;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI.Controls;

namespace cztApp1.Views;

public partial class MapView : UserControl
{
    /// <summary>暴露 Esri 原生 MapView，供外部转发输入事件。</summary>
    public Esri.ArcGISRuntime.UI.Controls.MapView EsriControl => EsriMapView;

    private readonly Esri.ArcGISRuntime.Mapping.Map _map;
    private readonly Dictionary<string, Layer> _layerLookup = new();

    public MapView()
    {
        InitializeComponent();

        // 强制硬件渲染（禁用软件渲染）
        RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;

        // 设置开发者许可证，去除 "Licensed for Developer" 水印
        try
        {
            Esri.ArcGISRuntime.ArcGISRuntimeEnvironment.SetLicense(
                "runtimelite,1000,rud9999999999,none,XXXXXXXXXXXXXXXX");
        }
        catch { /* 许可证无效时忽略，保留默认 */ }

        _map = new Esri.ArcGISRuntime.Mapping.Map(SpatialReferences.Wgs84)
        {
            BackgroundColor = System.Drawing.Color.White
        };
        EsriMapView.Map = _map;
        if (EsriMapView.BackgroundGrid != null)
            EsriMapView.BackgroundGrid.IsVisible = false;
        EsriMapView.RenderTransform = null; // 禁用可能触发软件渲染的变换

        // 比例尺：ViewpointChanged 触发（不频繁，无需节流）
        EsriMapView.ViewpointChanged += (_, _) =>
        {
            ScaleChanged?.Invoke(EsriMapView.MapScale);
        };

        // 坐标：MouseMove 节流到 100ms 一次，避免 ScreenToLocation 拖死地图
        var lastCoordUpdate = DateTime.MinValue;
        EsriMapView.MouseMove += (_, e) =>
        {
            var now = DateTime.UtcNow;
            if ((now - lastCoordUpdate).TotalMilliseconds < 100) return;
            lastCoordUpdate = now;
            try
            {
                var pt = EsriMapView.ScreenToLocation(e.GetPosition(EsriMapView));
                if (pt != null)
                    CoordinateChanged?.Invoke(pt.X, pt.Y);
            }
            catch { }
        };
    }

    /// <summary>比例尺变化事件（参数：比例尺分母）</summary>
    public event Action<double>? ScaleChanged;
    /// <summary>鼠标坐标变化事件（参数：经度, 纬度）</summary>
    public event Action<double, double>? CoordinateChanged;

    #region Public API

    public async Task<string> AddVectorLayerAsync(string name, string filePath, Models.VectorSymbol? style = null)
    {
        var layerId = $"layer_{Guid.NewGuid():N}";
        try
        {
            var table = await ShapefileFeatureTable.OpenAsync(filePath);
            var layer = new FeatureLayer(table)
            {
                Name = name,
                RenderingMode = FeatureRenderingMode.Static
            };

            // Apply initial symbology
            if (style != null)
                ApplyVectorStyle(layer, style);

            _map.OperationalLayers.Add(layer);
            _layerLookup[layerId] = layer;

            if (layer.FullExtent != null)
                await EsriMapView.SetViewpointGeometryAsync(layer.FullExtent);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AddVectorLayer failed: {ex.Message}");
            return "";
        }
        return layerId;
    }

    public async Task<string> AddRasterLayerAsync(string name, string filePath)
    {
        var layerId = $"layer_{Guid.NewGuid():N}";
        try
        {
            var raster = new Esri.ArcGISRuntime.Rasters.Raster(filePath);
            await raster.LoadAsync();
            var layer = new RasterLayer(raster) { Name = name };

            _map.OperationalLayers.Add(layer);
            _layerLookup[layerId] = layer;

            if (layer.FullExtent != null)
                if (layer.FullExtent != null)
                await EsriMapView.SetViewpointGeometryAsync(layer.FullExtent);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AddRasterLayer failed: {ex.Message}");
            return "";
        }
        return layerId;
    }

    public Task UpdateLayerStyleAsync(Services.MapLayer layer)
    {
        try
        {
            if (layer.VectorSymbol == null || !_layerLookup.TryGetValue(layer.LayerId, out var l))
                return Task.CompletedTask;

            if (l is FeatureLayer fl)
            {
                ApplyVectorStyle(fl, layer.VectorSymbol);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateLayerStyle failed: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public async Task RemoveLayerAsync(string layerId)
    {
        if (_layerLookup.TryGetValue(layerId, out var l))
        {
            _map.OperationalLayers.Remove(l);
            _layerLookup.Remove(layerId);
        }
    }

    public async Task ZoomToLayerAsync(string layerId)
    {
        if (_layerLookup.TryGetValue(layerId, out var l) && l.FullExtent != null)
            await EsriMapView.SetViewpointGeometryAsync(l.FullExtent);
    }

    public Task ClearAllLayersAsync()
    {
        _map.OperationalLayers.Clear();
        _layerLookup.Clear();
        return Task.CompletedTask;
    }

    public async Task ApplyFieldSymbologyAsync(string layerId, string fieldName, List<System.Drawing.Color> ramp)
    {
        if (!_layerLookup.TryGetValue(layerId, out var l) || l is not FeatureLayer fl) return;
        var table = fl.FeatureTable;
        if (table == null) return;

        // 获取字段类型并收集唯一值
        var field = table.Fields.FirstOrDefault(f => f.Name == fieldName);
        if (field == null) return;

        var features = await table.QueryFeaturesAsync(new Esri.ArcGISRuntime.Data.QueryParameters { WhereClause = "1=1" });
        var values = new List<object>();
        foreach (var f in features)
        {
            if (f.Attributes.ContainsKey(fieldName))
                values.Add(f.Attributes[fieldName] ?? DBNull.Value);
        }
        var uniqueValues = values.Where(v => v != DBNull.Value).Select(v => v.ToString()).Distinct().OrderBy(v => v).ToList();

        if (uniqueValues.Count <= 1) return;

        // 判断字段类型：数值型用 ClassBreaksRenderer，文本型用 UniqueValueRenderer
        bool isNumeric = Services.Reclassifier.IsNumericField(field.FieldType);

        if (isNumeric && uniqueValues.Count > 10)
        {
            // 数值字段 → 分5级用ClassBreaksRenderer
            var nums = uniqueValues.Select(v => double.TryParse(v, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double d) ? d : double.NaN)
                .Where(d => !double.IsNaN(d)).ToList();
            var breaks = Services.Reclassifier.ComputeBreaks(nums, Services.ClassificationMethod.Quantile, Math.Min(5, ramp.Count));
            var classBreaks = new List<Esri.ArcGISRuntime.Symbology.ClassBreak>();
            for (int i = 0; i < breaks.Count - 1; i++)
            {
                var color = ramp[i % ramp.Count];
                var sym = GetSymbolForGeometry(table.GeometryType, color);
                classBreaks.Add(new Esri.ArcGISRuntime.Symbology.ClassBreak(
                    $"{breaks[i]:F2} - {breaks[i + 1]:F2}",
                    $"{breaks[i]:F2} - {breaks[i + 1]:F2}",
                    breaks[i], breaks[i + 1], sym));
            }
            fl.Renderer = new Esri.ArcGISRuntime.Symbology.ClassBreaksRenderer(fieldName, classBreaks);
        }
        else
        {
            // 文本/少量数值 → UniqueValueRenderer
            var uvInfos = new List<Esri.ArcGISRuntime.Symbology.UniqueValue>();
            for (int i = 0; i < Math.Min(uniqueValues.Count, ramp.Count * 5); i++)
            {
                var color = ramp[i % ramp.Count];
                var sym = GetSymbolForGeometry(table.GeometryType, color);
                var val = uniqueValues[i] ?? "";
                uvInfos.Add(new Esri.ArcGISRuntime.Symbology.UniqueValue(
                    val, val, sym, new object[] { val }));
            }
            // 默认符号
            var defaultSym = GetSymbolForGeometry(table.GeometryType, System.Drawing.Color.LightGray);
            fl.Renderer = new Esri.ArcGISRuntime.Symbology.UniqueValueRenderer(new[] { fieldName }, uvInfos, "", defaultSym);
        }
    }

    private static Esri.ArcGISRuntime.Symbology.Symbol GetSymbolForGeometry(
        Esri.ArcGISRuntime.Geometry.GeometryType? geom, System.Drawing.Color color)
    {
        return geom switch
        {
            Esri.ArcGISRuntime.Geometry.GeometryType.Point => new Esri.ArcGISRuntime.Symbology.SimpleMarkerSymbol
            { Color = color, Size = 8, Style = Esri.ArcGISRuntime.Symbology.SimpleMarkerSymbolStyle.Circle },
            Esri.ArcGISRuntime.Geometry.GeometryType.Polyline => new Esri.ArcGISRuntime.Symbology.SimpleLineSymbol
            { Color = color, Width = 2 },
            _ => new Esri.ArcGISRuntime.Symbology.SimpleFillSymbol
            { Color = System.Drawing.Color.FromArgb(128, color), Outline = new Esri.ArcGISRuntime.Symbology.SimpleLineSymbol { Color = color, Width = 1 } }
        };
    }

    #endregion

    #region Symbology

    private static void ApplyVectorStyle(FeatureLayer layer, Models.VectorSymbol vs)
    {
        var geom = layer.FeatureTable?.GeometryType;
        if (geom == null) return;

        var renderer = geom switch
        {
            GeometryType.Point => (Renderer)new SimpleRenderer(
                new SimpleMarkerSymbol
                {
                    Color = ParseEsriColor(vs.PointColor),
                    Size = vs.PointSize,
                    Style = SimpleMarkerSymbolStyle.Circle
                }),
            GeometryType.Polyline => new SimpleRenderer(
                new SimpleLineSymbol
                {
                    Color = ParseEsriColor(vs.StrokeColor),
                    Width = vs.StrokeWidth
                }),
            _ => new SimpleRenderer(
                new SimpleFillSymbol
                {
                    Color = ParseEsriColor(vs.FillColor, (byte)(vs.FillOpacity * 255)),
                    Outline = new SimpleLineSymbol
                    {
                        Color = ParseEsriColor(vs.StrokeColor),
                        Width = vs.StrokeWidth
                    }
                })
        };

        layer.Renderer = renderer;
    }

    private static System.Drawing.Color ParseEsriColor(string hex, byte alpha = 255)
    {
        try
        {
            var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            return System.Drawing.Color.FromArgb(alpha, c.R, c.G, c.B);
        }
        catch { return System.Drawing.Color.Gray; }
    }

    #endregion
}
