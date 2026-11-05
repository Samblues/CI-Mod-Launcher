using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CILauncher;

public sealed class SoftWrapConverter : IValueConverter
{
    public static SoftWrapConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string text || text.Length == 0)
            return value;
        return text.Replace("\\", "\\\u200b").Replace("/", "/\u200b");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class OnTagConverter : IValueConverter
{
    public static OnTagConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "on" : "off";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolVisibilityConverter : IValueConverter
{
    public static BoolVisibilityConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = value is true;
        if (parameter is string flag && flag == "invert")
            visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
