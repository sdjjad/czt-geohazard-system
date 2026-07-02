using System;
using System.Globalization;
using System.Windows.Data;
using cztApp1.Models;

namespace cztApp1.Views;

/// <summary>
/// Converts SpatialDataType enum to Chinese display string for the layer list.
/// </summary>
public class MapLayerTypeConverter : IValueConverter
{
    public static readonly MapLayerTypeConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SpatialDataType type)
        {
            return type switch
            {
                SpatialDataType.Vector => "矢量",
                SpatialDataType.Raster => "栅格",
                _ => ""
            };
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
