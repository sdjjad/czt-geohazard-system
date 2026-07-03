using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using cztApp1.Models;
using cztApp1.Services;

namespace cztApp1.Views;

/// <summary>
/// 把 SymbolItem 转为真实的符号预览图形，从图层读取最新符号值
/// </summary>
public class SymbolPreviewConverter : IValueConverter
{
    public static readonly SymbolPreviewConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not SymbolItem sym || sym.Layer is not MapLayer layer)
            return new Border { Width = 20, Height = 14 };

        return sym.Geometry switch
        {
            SymbolGeometry.Polygon => BuildPolygon(layer.VectorSymbol),
            SymbolGeometry.Line => BuildLine(layer.VectorSymbol),
            SymbolGeometry.Point => BuildPoint(layer.VectorSymbol),
            SymbolGeometry.Raster => BuildRaster(layer.RasterSymbol),
            _ => new Border { Width = 20, Height = 14 }
        };
    }

    private static FrameworkElement BuildPolygon(VectorSymbol? vs)
    {
        var fill = ParseColor(vs?.FillColor ?? "#64B5F6");
        fill.A = (byte)((vs?.FillOpacity ?? 0.3) * 255);
        var stroke = ParseColor(vs?.StrokeColor ?? "#1565C0");
        return new Rectangle
        {
            Width = 28, Height = 16,
            Fill = new SolidColorBrush(fill),
            Stroke = new SolidColorBrush(stroke),
            StrokeThickness = Math.Max(1, vs?.StrokeWidth ?? 2),
            SnapsToDevicePixels = true
        };
    }

    private static FrameworkElement BuildLine(VectorSymbol? vs)
    {
        var color = ParseColor(vs?.StrokeColor ?? "#1565C0");
        return new System.Windows.Shapes.Line
        {
            X1 = 2, Y1 = 8, X2 = 30, Y2 = 8,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = Math.Max(1.5, vs?.StrokeWidth ?? 2),
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Width = 32, Height = 16
        };
    }

    private static FrameworkElement BuildPoint(VectorSymbol? vs)
    {
        var color = ParseColor(vs?.PointColor ?? "#E81123");
        double size = Math.Max(4, Math.Min(32, (vs?.PointSize ?? 8)));
        return new Ellipse
        {
            Width = size, Height = size,
            Fill = new SolidColorBrush(color),
            Stroke = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
            StrokeThickness = 1
        };
    }

    private static FrameworkElement BuildRaster(RasterSymbol? rs)
    {
        var from = ParseColor(rs?.Stops.Count > 0 ? rs.Stops[0].Color : "#000000");
        var to = ParseColor(rs?.Stops.Count > 1 ? rs.Stops[^1].Color : "#FFFFFF");
        return new Rectangle
        {
            Width = 32, Height = 12,
            Fill = new LinearGradientBrush(from, to, 0),
            Stroke = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0)),
            StrokeThickness = 0.5
        };
    }

    private static Color ParseColor(string hex, Color fallback = default)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback.A > 0 ? fallback : Colors.Gray; }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
