using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace cztApp1.Views;

/// <summary>
/// 将 "#1565C0" 这样的十六进制颜色字符串转换为 SolidColorBrush
/// </summary>
public class HexToBrushConverter : IValueConverter
{
    public static readonly HexToBrushConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && hex.Length >= 7)
        {
            try
            {
                return new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(hex));
            }
            catch { }
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
