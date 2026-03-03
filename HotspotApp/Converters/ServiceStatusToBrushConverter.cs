using HotspotApp.Models;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace HotspotApp.Converters;

public class ServiceStatusToBrushConverter : IValueConverter
{
    public Brush UnknownBrush { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.Gray);
    public Brush NotInstalledBrush { get; set; } =
        new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
    public Brush StoppedBrush { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.Orange);
    public Brush RunningBrush { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.LightGreen);
    public Brush ErrorBrush { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.Red);

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is ServiceStatus status)
        {
            return status switch
            {
                ServiceStatus.Running => RunningBrush,
                ServiceStatus.Stopped => StoppedBrush,
                ServiceStatus.NotInstalled => NotInstalledBrush,
                ServiceStatus.Error => ErrorBrush,
                _ => UnknownBrush,
            };
        }
        return UnknownBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
