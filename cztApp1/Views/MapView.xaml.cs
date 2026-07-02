using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;

namespace cztApp1.Views;

/// <summary>
/// Map view control wrapping WebView2 + Leaflet.js.
/// Displays vector (GeoJSON) and raster (image overlay) layers.
/// </summary>
public partial class MapView : UserControl
{
    private bool _mapReady;
    private readonly Queue<string> _pendingScripts = new();
    private int _layerIdCounter;

    public MapView()
    {
        InitializeComponent();
        WebMap.CoreWebView2InitializationCompleted += OnCoreWebView2Ready;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await WebMap.EnsureCoreWebView2Async(null);
    }

    private void OnCoreWebView2Ready(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (!e.IsSuccess) return;
        WebMap.CoreWebView2.Settings.IsScriptEnabled = true;
        WebMap.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
        WebMap.CoreWebView2.Settings.IsWebMessageEnabled = true;

        WebMap.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        var html = BuildLeafletHtml();
        WebMap.NavigateToString(html);
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var msg = e.TryGetWebMessageAsString();
        if (msg == "\"map-ready\"")
        {
            _mapReady = true;
            Dispatcher.Invoke(() =>
            {
                while (_pendingScripts.Count > 0)
                {
                    var script = _pendingScripts.Dequeue();
                    WebMap.CoreWebView2.ExecuteScriptAsync(script);
                }
            });
        }
    }

    /// <summary>
    /// Ensure a script runs either immediately (if map is ready) or as soon as the map is ready.
    /// </summary>
    private async Task RunScriptAsync(string script)
    {
        if (_mapReady && WebMap.CoreWebView2 != null)
        {
            await WebMap.CoreWebView2.ExecuteScriptAsync(script);
        }
        else
        {
            _pendingScripts.Enqueue(script);
        }
    }

    #region Public API

    /// <summary>
    /// Add a vector layer from GeoJSON string.
    /// </summary>
    public async Task<string> AddVectorLayerAsync(string name, string geojson)
    {
        var layerId = $"layer_{Interlocked.Increment(ref _layerIdCounter)}";
        // Escape backslashes and quotes for safe JS embedding
        var escapedJson = geojson
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\r", "")
            .Replace("\n", " ");

        var script = $@"addVectorLayer('{layerId}', '{name}', '{escapedJson}');";
        await RunScriptAsync(script);

        // Also zoom to the layer if it's the first one
        await RunScriptAsync($"zoomToLayer('{layerId}');");

        return layerId;
    }

    /// <summary>
    /// Add a raster layer from a local image file path. The file is read, converted to base64 PNG,
    /// and displayed as a Leaflet ImageOverlay using bounds read from the world file.
    /// </summary>
    public async Task<string> AddRasterLayerAsync(string name, string filePath)
    {
        try
        {
            // Read bounds from world file (.tfw)
            var (north, south, east, west) = ReadRasterBounds(filePath);

            // Convert raster to base64 PNG for Leaflet
            var base64Png = ConvertRasterToBase64Png(filePath);
            if (base64Png == null) return "";

            var layerId = $"layer_{Interlocked.Increment(ref _layerIdCounter)}";
            var script = $@"addRasterLayer('{layerId}', '{name}', '{base64Png}', {south.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {west.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {north.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {east.ToString(System.Globalization.CultureInfo.InvariantCulture)});";
            await RunScriptAsync(script);
            await RunScriptAsync($"zoomToLayer('{layerId}');");

            return layerId;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to add raster layer: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// Remove a layer by its ID.
    /// </summary>
    public async Task RemoveLayerAsync(string layerId)
    {
        await RunScriptAsync($"removeLayer('{layerId}');");
    }

    /// <summary>
    /// Zoom the map to fit a specific layer.
    /// </summary>
    public async Task ZoomToLayerAsync(string layerId)
    {
        await RunScriptAsync($"zoomToLayer('{layerId}');");
    }

    /// <summary>
    /// Clear all layers from the map.
    /// </summary>
    public async Task ClearAllLayersAsync()
    {
        await RunScriptAsync("clearAllLayers();");
    }

    #endregion

    #region Raster helpers

    /// <summary>
    /// Read geographic bounds from a world file (.tfw) associated with the raster.
    /// Returns (north, south, east, west).
    /// If no .tfw exists, returns approximate bounds around CZT region.
    /// </summary>
    private static (double north, double south, double east, double west) ReadRasterBounds(string rasterPath)
    {
        var dir = Path.GetDirectoryName(rasterPath) ?? "";
        var baseName = Path.GetFileNameWithoutExtension(rasterPath);

        // Try .tfw (World file for GeoTIFF)
        var tfwPath = Path.Combine(dir, baseName + ".tfw");
        if (!File.Exists(tfwPath))
        {
            // Try .tif + .tfw
            tfwPath = rasterPath + ".tfw";
        }
        if (!File.Exists(tfwPath))
        {
            // Try alternate naming: some files have extension.tfw (e.g. .tif.tfw)
            tfwPath = rasterPath.Substring(0, rasterPath.LastIndexOf('.')) + ".tfw";
        }

        if (File.Exists(tfwPath))
        {
            try
            {
                var lines = File.ReadAllLines(tfwPath);
                if (lines.Length >= 6)
                {
                    double pixelX = double.Parse(lines[0], System.Globalization.CultureInfo.InvariantCulture);
                    double rotationY = double.Parse(lines[1], System.Globalization.CultureInfo.InvariantCulture);
                    double rotationX = double.Parse(lines[2], System.Globalization.CultureInfo.InvariantCulture);
                    double pixelY = double.Parse(lines[3], System.Globalization.CultureInfo.InvariantCulture);
                    double upperLeftX = double.Parse(lines[4], System.Globalization.CultureInfo.InvariantCulture);
                    double upperLeftY = double.Parse(lines[5], System.Globalization.CultureInfo.InvariantCulture);

                    // Estimate image dimensions
                    int imgWidth = 1000, imgHeight = 1000;
                    try
                    {
                        var decoder = new TiffBitmapDecoder(
                            new Uri(rasterPath), BitmapCreateOptions.None, BitmapCacheOption.OnDemand);
                        if (decoder.Frames.Count > 0)
                        {
                            imgWidth = decoder.Frames[0].PixelWidth;
                            imgHeight = decoder.Frames[0].PixelHeight;
                        }
                    }
                    catch { /* use defaults */ }

                    double west = upperLeftX;
                    double north = upperLeftY;
                    double east = upperLeftX + imgWidth * pixelX;
                    double south = upperLeftY + imgHeight * pixelY;

                    return (north, south, east, west);
                }
            }
            catch { /* fall through to default */ }
        }

        // Default: CZT region approximate bounds
        return (28.5, 27.5, 113.5, 112.5);
    }

    /// <summary>
    /// Convert a raster file (GeoTIFF) to a base64-encoded PNG string for Leaflet display.
    /// </summary>
    private static string? ConvertRasterToBase64Png(string filePath)
    {
        try
        {
            // For .png files, read directly
            if (filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = File.ReadAllBytes(filePath);
                return Convert.ToBase64String(bytes);
            }

            // For .tif/.tiff, use WPF TiffBitmapDecoder
            var decoder = new TiffBitmapDecoder(
                new Uri(filePath), BitmapCreateOptions.None, BitmapCacheOption.OnDemand);

            if (decoder.Frames.Count == 0) return null;

            var frame = decoder.Frames[0];

            // Resize if the image is too large (>2000px) for browser performance
            int maxDim = 2000;
            int targetW = frame.PixelWidth;
            int targetH = frame.PixelHeight;
            if (targetW > maxDim || targetH > maxDim)
            {
                double scale = Math.Min((double)maxDim / targetW, (double)maxDim / targetH);
                targetW = (int)(targetW * scale);
                targetH = (int)(targetH * scale);
            }

            // Render to a RenderTargetBitmap
            var visual = new DrawingVisual();
            using (var ctx = visual.RenderOpen())
            {
                ctx.DrawImage(frame, new Rect(0, 0, targetW, targetH));
            }

            var rtb = new RenderTargetBitmap(targetW, targetH, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(visual);

            // Encode to PNG
            var pngEncoder = new PngBitmapEncoder();
            pngEncoder.Frames.Add(BitmapFrame.Create(rtb));

            using var ms = new MemoryStream();
            pngEncoder.Save(ms);
            return Convert.ToBase64String(ms.ToArray());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Raster conversion failed: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region HTML generation

    /// <summary>
    /// Build self-contained Leaflet HTML with layer management JS.
    /// </summary>
    private static string BuildLeafletHtml()
    {
        var sb = new StringBuilder();
        sb.Append(@"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<title>Map</title>
<link rel=""stylesheet"" href=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"" />
<style>
  html, body { margin:0; padding:0; width:100%; height:100%; overflow:hidden; }
  #map { width:100%; height:100%; background:#E8E8E8; }
  .layer-control {
    position:absolute; top:10px; right:10px; background:white;
    border-radius:4px; box-shadow:0 1px 5px rgba(0,0,0,0.3);
    padding:8px 12px; font-family:'Microsoft YaHei UI',sans-serif;
    font-size:12px; max-height:60%; overflow-y:auto; min-width:160px; z-index:1000;
  }
  .layer-control h4 { margin:0 0 6px; font-size:13px; color:#333; }
  .layer-item { display:flex; align-items:center; margin:4px 0; gap:6px; }
  .layer-item input[type=""checkbox""] { cursor:pointer; }
  .layer-item label { flex:1; cursor:pointer; font-size:11px; color:#444; }
  .layer-item .remove-btn { cursor:pointer; color:#999; font-size:14px; border:none; background:none; padding:0 2px; }
  .layer-item .remove-btn:hover { color:#E81123; }
  .no-layers { color:#999; font-size:11px; font-style:italic; }
</style>
</head>
<body>
<div id=""map""></div>
<div class=""layer-control"" id=""layerControl"">
  <h4>图层列表</h4>
  <div id=""layerList""><span class=""no-layers"">暂无图层</span></div>
</div>

<script src=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.js""></script>
<script>
var map = L.map('map', {
  center: [27.9, 113.0],
  zoom: 9,
  zoomControl: true,
  attributionControl: false
});

// Base tile layer — OpenStreetMap
L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
  maxZoom: 19,
  opacity: 0.4
}).addTo(map);

// Layer registry
var layers = {};

function updateLayerControl() {
  var list = document.getElementById('layerList');
  var keys = Object.keys(layers);
  if (keys.length === 0) {
    list.innerHTML = '<span class=""no-layers"">暂无图层</span>';
    return;
  }
  var html = '';
  keys.forEach(function(id) {
    var l = layers[id];
    html += '<div class=""layer-item"">' +
      '<input type=""checkbox"" checked onchange=""toggleLayer(\'' + id + '\', this.checked)"">' +
      '<label title=""' + l.name + '"">' + l.name + '</label>' +
      '<button class=""remove-btn"" onclick=""removeLayer(\'' + id + '\')"" title=""移除图层"">✕</button>' +
      '</div>';
  });
  list.innerHTML = html;
}

function addVectorLayer(id, name, geojsonStr) {
  // Remove existing layer with same ID
  if (layers[id]) { map.removeLayer(layers[id].leafletLayer); }

  var geojson = JSON.parse(geojsonStr);
  var lyr = L.geoJSON(geojson, {
    style: function(feature) {
      return { color: '#1565C0', weight: 2, fillColor: '#64B5F6', fillOpacity: 0.3 };
    },
    pointToLayer: function(feature, latlng) {
      return L.circleMarker(latlng, { radius: 6, color: '#E81123', fillColor: '#E81123', fillOpacity: 0.7, weight: 2 });
    }
  }).addTo(map);

  layers[id] = { name: name, leafletLayer: lyr, type: 'vector' };
  updateLayerControl();
}

function addRasterLayer(id, name, base64data, south, west, north, east) {
  if (layers[id]) { map.removeLayer(layers[id].leafletLayer); }

  var imgUrl = 'data:image/png;base64,' + base64data;
  var bounds = [[south, west], [north, east]];
  var lyr = L.imageOverlay(imgUrl, bounds, { opacity: 0.8 }).addTo(map);

  layers[id] = { name: name, leafletLayer: lyr, type: 'raster' };
  updateLayerControl();
}

function removeLayer(id) {
  if (layers[id]) {
    map.removeLayer(layers[id].leafletLayer);
    delete layers[id];
  }
  updateLayerControl();
}

function zoomToLayer(id) {
  if (layers[id] && layers[id].leafletLayer.getBounds) {
    map.fitBounds(layers[id].leafletLayer.getBounds(), { padding: [30, 30] });
  }
}

function toggleLayer(id, visible) {
  if (layers[id]) {
    if (visible) {
      layers[id].leafletLayer.addTo(map);
    } else {
      map.removeLayer(layers[id].leafletLayer);
    }
  }
}

function clearAllLayers() {
  Object.keys(layers).forEach(function(id) {
    map.removeLayer(layers[id].leafletLayer);
  });
  layers = {};
  updateLayerControl();
}

// Notify C# that the map is ready
try { window.chrome.webview.postMessage('""map-ready""'); } catch(e) {}
</script>
</body>
</html>");
        return sb.ToString();
    }

    #endregion
}
