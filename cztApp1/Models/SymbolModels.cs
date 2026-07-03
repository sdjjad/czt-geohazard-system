using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace cztApp1.Models;

/// <summary>
/// 符号对应的几何类型
/// </summary>
public enum SymbolGeometry
{
    Polygon,  // 面 → 填充矩形
    Line,     // 线 → 线条
    Point,    // 点 → 圆
    Raster    // 栅格 → 渐变色带
}

/// <summary>
/// 矢量符号：填充、轮廓线、点样式
/// </summary>
public class VectorSymbol : INotifyPropertyChanged
{
    private string _fillColor = "#64B5F6";
    private double _fillOpacity = 0.3;
    private string _strokeColor = "#1565C0";
    private double _strokeWidth = 2;
    private double _pointSize = 8;
    private string _pointColor = "#E81123";

    public string FillColor { get => _fillColor; set { _fillColor = value; OnChanged(); } }
    public double FillOpacity { get => _fillOpacity; set { _fillOpacity = value; OnChanged(); } }
    public string StrokeColor { get => _strokeColor; set { _strokeColor = value; OnChanged(); } }
    public double StrokeWidth { get => _strokeWidth; set { _strokeWidth = value; OnChanged(); } }
    public double PointSize { get => _pointSize; set { _pointSize = value; OnChanged(); } }
    public string PointColor { get => _pointColor; set { _pointColor = value; OnChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// 栅格色带的一个颜色节点
/// </summary>
public class RasterStop
{
    public string Color { get; set; } = "#000000";
    public double Value { get; set; }
    public string Label { get; set; } = "";
}

/// <summary>
/// 栅格符号：色带
/// </summary>
public class RasterSymbol : INotifyPropertyChanged
{
    public List<RasterStop> Stops { get; set; } = new()
    {
        new RasterStop { Color = "#000000", Value = 0, Label = "低" },
        new RasterStop { Color = "#FFFFFF", Value = 255, Label = "高" }
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    public void NotifyChanged() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Stops)));
}

/// <summary>
/// 图层面板中显示的子项（符号条目），包含几何类型和完整符号信息
/// </summary>
public class SymbolItem
{
    public string Label { get; set; } = "";
    public SymbolGeometry Geometry { get; set; } = SymbolGeometry.Polygon;
    public string FillColor { get; set; } = "#64B5F6";
    public double FillOpacity { get; set; } = 0.3;
    public string StrokeColor { get; set; } = "#1565C0";
    public double StrokeWidth { get; set; } = 2;
    public double PointSize { get; set; } = 8;
    public string PointColor { get; set; } = "#E81123";
    /// <summary>栅格渐变色（起点→终点）</summary>
    public string RampFrom { get; set; } = "#000000";
    public string RampTo { get; set; } = "#FFFFFF";
}
