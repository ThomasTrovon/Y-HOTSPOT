using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace HotspotApp.Converters;

public class BooleanToBrushConverter : IValueConverter
{
    public Brush TrueBrush { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.LightGreen);
    public Brush FalseBrush { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return b ? TrueBrush : FalseBrush;
        }
        return FalseBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
