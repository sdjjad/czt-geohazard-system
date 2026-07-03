using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using cztApp1.Models;

namespace cztApp1.Views;

/// <summary>
/// 把 SymbolItem 转为真实的符号预览图形（矩形 / 线 / 圆 / 色带）
/// </summary>
public class SymbolPreviewConverter : IValueConverter
{
    public static readonly SymbolPreviewConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not SymbolItem sym)
            return new Border { Width = 20, Height = 14 };

        return sym.Geometry switch
        {
            SymbolGeometry.Polygon => BuildPolygon(sym),
            SymbolGeometry.Line => BuildLine(sym),
            SymbolGeometry.Point => BuildPoint(sym),
            SymbolGeometry.Raster => BuildRaster(sym),
            _ => new Border { Width = 20, Height = 14 }
        };
    }

    private static FrameworkElement BuildPolygon(SymbolItem sym)
    {
        var fill = ParseColor(sym.FillColor, Colors.CornflowerBlue);
        fill.A = (byte)(sym.FillOpacity * 255);
        var stroke = ParseColor(sym.StrokeColor, Colors.SteelBlue);
        return new Rectangle
        {
            Width = 28, Height = 16,
            Fill = new SolidColorBrush(fill),
            Stroke = new SolidColorBrush(stroke),
            StrokeThickness = Math.Max(1, sym.StrokeWidth),
            SnapsToDevicePixels = true
        };
    }

    private static FrameworkElement BuildLine(SymbolItem sym)
    {
        var stroke = ParseColor(sym.StrokeColor, Colors.SteelBlue);
        // 一条水平线 + 一个小方块显示颜色
        var grid = new Grid { Width = 32, Height = 16 };
        var line = new System.Windows.Shapes.Line
        {
            X1 = 2, Y1 = 8, X2 = 30, Y2 = 8,
            Stroke = new SolidColorBrush(stroke),
            StrokeThickness = Math.Max(1.5, sym.StrokeWidth),
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        grid.Children.Add(line);
        return grid;
    }

    private static FrameworkElement BuildPoint(SymbolItem sym)
    {
        var fill = ParseColor(sym.PointColor, Colors.Red);
        return new Ellipse
        {
            Width = 14, Height = 14,
            Fill = new SolidColorBrush(fill),
            Stroke = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
            StrokeThickness = 1
        };
    }

    private static FrameworkElement BuildRaster(SymbolItem sym)
    {
        var from = ParseColor(sym.RampFrom, Colors.Black);
        var to = ParseColor(sym.RampTo, Colors.White);
        var gradient = new LinearGradientBrush(from, to, 0);
        return new Rectangle
        {
            Width = 32, Height = 12,
            Fill = gradient,
            Stroke = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0)),
            StrokeThickness = 0.5
        };
    }

    private static Color ParseColor(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
