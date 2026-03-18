using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Meridian.Presentation;

public sealed class TagToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush GainTint = new(Color.FromArgb(0x18, 0x2D, 0x6A, 0x4F));
    private static readonly SolidColorBrush LossTint = new(Color.FromArgb(0x18, 0xB5, 0x34, 0x2B));
    private static readonly SolidColorBrush AccentTint = new(Color.FromArgb(0x18, 0xC9, 0xA9, 0x6E));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value as string) switch
        {
            "Earnings" => GainTint,
            "Bonds" => LossTint,
            _ => AccentTint
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
