using System.Windows.Controls;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI.Controls;

namespace cztApp1.Views;

public partial class MapView : UserControl
{
    private readonly Esri.ArcGISRuntime.Mapping.Map _map;
    private readonly Dictionary<string, Layer> _layerLookup = new();

    public MapView()
    {
        InitializeComponent();

        _map = new Esri.ArcGISRuntime.Mapping.Map(SpatialReferences.Wgs84);
        EsriMapView.Map = _map;
    }

    #region Public API

    public async Task<string> AddVectorLayerAsync(string name, string filePath, Models.VectorSymbol? style = null)
    {
        var layerId = $"layer_{Guid.NewGuid():N}";
        try
        {
            var table = await ShapefileFeatureTable.OpenAsync(filePath);
            var layer = new FeatureLayer(table) { Name = name };

            // Apply initial symbology
            if (style != null)
                ApplyVectorStyle(layer, style);

            _map.OperationalLayers.Add(layer);
            _layerLookup[layerId] = layer;

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
            var layer = new RasterLayer(raster) { Name = name };

            _map.OperationalLayers.Add(layer);
            _layerLookup[layerId] = layer;

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
