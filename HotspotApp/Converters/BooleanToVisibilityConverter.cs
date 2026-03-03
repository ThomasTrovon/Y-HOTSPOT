using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace HotspotApp.Converters;

public partial class BooleanToVisibilityConverter : IValueConverter
{
    // Converts bool to Visibility; supports inversion via ConverterParameter ("invert"/true)
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var flag = value is bool b && b;
        var invert = IsInvert(parameter);
        return (invert ? !flag : flag) ? Visibility.Visible : Visibility.Collapsed;
    }

    // Converts Visibility back to bool; applies same inversion rule
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        var visible = value is Visibility visibility && visibility == Visibility.Visible;
        var invert = IsInvert(parameter);
        return invert ? !visible : visible;
    }

    private static bool IsInvert(object parameter)
    {
        if (parameter is bool b)
            return b;
        if (parameter is string s)
        {
            var normalized = s.Trim().ToLowerInvariant();
            return normalized is "invert" or "true" or "yes" or "1";
        }
        return false;
    }
}
