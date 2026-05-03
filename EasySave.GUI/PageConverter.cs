using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EasySave.GUI
{
    /// <summary>
    /// Converts the current page name to Visibility.
    /// Usage: ConverterParameter=PageName → Visible if CurrentPage == PageName, else Collapsed.
    /// </summary>
    public class PageConverter : IValueConverter
    {
        public static readonly PageConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? current  = value?.ToString();
            string? expected = parameter?.ToString();
            return string.Equals(current, expected, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
