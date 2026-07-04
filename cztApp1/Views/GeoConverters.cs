using System.Globalization;
using System.Windows.Data;

namespace cztApp1.Views
{
    /// <summary>
    /// ClassCount (int 3-8) → ComboBox SelectedIndex (0-5)
    /// </summary>
    public class CountToIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
                return Math.Clamp(count - 3, 0, 5);
            return 2; // default: 5 → index 2
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // ComboBoxItem content is a string like "5", parse it
            if (value is int index)
                return index + 3;
            return 5;
        }
    }

    /// <summary>
    /// CF值 → 易发性评价文本
    /// </summary>
    public class CfToRiskConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double cf)
            {
                return cf switch
                {
                    < -0.5 => "极低易发",
                    < 0 => "低易发",
                    < 0.3 => "中等易发",
                    < 0.6 => "高易发",
                    _ => "极高易发"
                };
            }
            return "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
