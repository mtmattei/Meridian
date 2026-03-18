using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Meridian.Presentation;

public sealed class GainLossBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Gain = new(Color.FromArgb(0xFF, 0x2D, 0x6A, 0x4F));
    private static readonly SolidColorBrush Loss = new(Color.FromArgb(0xFF, 0xB5, 0x34, 0x2B));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            bool b => b ? Gain : Loss,
            decimal d => d >= 0 ? Gain : Loss,
            double d => d >= 0 ? Gain : Loss,
            _ => Gain
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
