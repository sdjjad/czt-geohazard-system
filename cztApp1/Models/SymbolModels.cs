using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace cztApp1.Models;

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
/// 图层面板中显示的子项（符号条目）
/// </summary>
public class SymbolItem
{
    /// <summary>显示文字：如 "填充 (#64B5F6)"</summary>
    public string Label { get; set; } = "";
    /// <summary>颜色值</summary>
    public string ColorHex { get; set; } = "#1565C0";
    /// <summary>几何图形符号：■ 多边形 / ━ 线 / ● 点</summary>
    public string Shape { get; set; } = "■";
}
