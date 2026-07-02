using System.Collections.ObjectModel;
using System.IO;
using cztApp1.Models;
using cztApp1.Views;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace cztApp1.Services;

/// <summary>
/// Represents a layer currently displayed on the map.
/// </summary>
public class MapLayer
{
    public string LayerId { get; init; } = "";
    public string Name { get; init; } = "";
    public string FilePath { get; init; } = "";
    public SpatialDataType Type { get; init; }
    public bool IsVisible { get; set; } = true;
}

/// <summary>
/// Service that reads spatial data files and manages the map layer collection.
/// </summary>
public class MapLayerService
{
    private readonly MapView _mapView;
    private readonly Dictionary<string, MapLayer> _layerLookup = new();

    /// <summary>
    /// Observable list of layers for UI binding.
    /// </summary>
    public ObservableCollection<MapLayer> Layers { get; } = new();

    /// <summary>
    /// Fired when a layer is added or removed.
    /// </summary>
    public event Action? LayersChanged;

    public MapLayerService(MapView mapView)
    {
        _mapView = mapView;
    }

    /// <summary>
    /// Add a spatial data file to the map. Determines type and delegates accordingly.
    /// </summary>
    public async Task<MapLayer?> AddLayerAsync(string filePath)
    {
        var type = SpatialDataHelper.ClassifyFile(filePath);
        if (type == SpatialDataType.Other) return null;

        var name = Path.GetFileNameWithoutExtension(filePath);

        // Avoid duplicate layers
        var existing = Layers.FirstOrDefault(l => l.FilePath == filePath);
        if (existing != null)
        {
            await _mapView.ZoomToLayerAsync(existing.LayerId);
            return existing;
        }

        string? layerId = null;

        if (type == SpatialDataType.Vector)
        {
            layerId = await AddVectorLayerAsync(name, filePath);
        }
        else if (type == SpatialDataType.Raster)
        {
            layerId = await AddRasterLayerAsync(name, filePath);
        }

        if (string.IsNullOrEmpty(layerId)) return null;

        var layer = new MapLayer
        {
            LayerId = layerId,
            Name = name,
            FilePath = filePath,
            Type = type
        };

        _layerLookup[layerId] = layer;
        Layers.Add(layer);
        LayersChanged?.Invoke();

        return layer;
    }

    /// <summary>
    /// Remove a layer from the map.
    /// </summary>
    public async Task RemoveLayerAsync(MapLayer layer)
    {
        await _mapView.RemoveLayerAsync(layer.LayerId);
        _layerLookup.Remove(layer.LayerId);
        Layers.Remove(layer);
        LayersChanged?.Invoke();
    }

    /// <summary>
    /// Zoom to a specific layer.
    /// </summary>
    public async Task ZoomToLayerAsync(MapLayer layer)
    {
        await _mapView.ZoomToLayerAsync(layer.LayerId);
    }

    /// <summary>
    /// Clear all layers from the map.
    /// </summary>
    public async Task ClearAllAsync()
    {
        await _mapView.ClearAllLayersAsync();
        _layerLookup.Clear();
        Layers.Clear();
        LayersChanged?.Invoke();
    }

    /// <summary>
    /// Read a shapefile, convert to GeoJSON, and add as a vector layer.
    /// </summary>
    private async Task<string> AddVectorLayerAsync(string name, string filePath)
    {
        try
        {
            var geojson = await Task.Run(() => ShapefileToGeoJson(filePath));
            if (string.IsNullOrEmpty(geojson)) return "";

            var layerId = await _mapView.AddVectorLayerAsync(name, geojson);
            return layerId;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Vector layer add failed: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// Read raster bounds and add as an image overlay.
    /// </summary>
    private async Task<string> AddRasterLayerAsync(string name, string filePath)
    {
        try
        {
            var layerId = await _mapView.AddRasterLayerAsync(name, filePath);
            return layerId;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Raster layer add failed: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// Convert a shapefile to a GeoJSON string using NetTopologySuite.
    /// </summary>
    public static string? ShapefileToGeoJson(string shpPath)
    {
        if (!File.Exists(shpPath)) return null;

        try
        {
            var factory = new GeometryFactory(new PrecisionModel(), 4326);
            var features = new List<IFeature>();

            using var shpReader = new ShapefileDataReader(shpPath, factory);
            var dbfHeaders = shpReader.DbaseHeader;
            var fieldNames = dbfHeaders.Fields.Select(f => f.Name).ToArray();

            while (shpReader.Read())
            {
                var geom = shpReader.Geometry;
                if (geom == null) continue;

                var attributes = new AttributesTable();
                for (int i = 0; i < fieldNames.Length; i++)
                {
                    var val = shpReader.GetValue(i);
                    attributes.Add(fieldNames[i], val ?? DBNull.Value);
                }

                var feature = new NetTopologySuite.Features.Feature(geom, attributes);
                features.Add(feature);
            }

            var collection = new FeatureCollection();
            foreach (var f in features)
                collection.Add(f);

            var serializer = GeoJsonSerializer.Create();
            using var sw = new StringWriter();
            serializer.Serialize(sw, collection);
            return sw.ToString();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Shapefile read failed: {ex.Message}");
            return null;
        }
    }
}
