using Microsoft.UI.Xaml.Data;

namespace Meridian.Converters;

/// <summary>
/// Converts a Stock to a GridLength ratio for the day-range bar.
/// Returns the filled portion as a star value (0.0 to 1.0).
/// Parameter: "fill" for the filled side, "remainder" for the unfilled side.
/// </summary>
public sealed class DayRangePositionConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, string language)
	{
		var side = parameter as string ?? "fill";

		// Extract Price, Low, High from the DataContext (Stock or MVUX proxy)
		decimal price = 0, low = 0, high = 0;
		if (value != null)
		{
			var type = value.GetType();
			price = GetDecimal(type, value, "Price");
			low = GetDecimal(type, value, "Low");
			high = GetDecimal(type, value, "High");
		}

		var range = high - low;
		var ratio = range > 0 ? (double)((price - low) / range) : 0.5;
		ratio = Math.Clamp(ratio, 0.05, 0.95); // Prevent invisible columns

		return side == "fill"
			? new GridLength(ratio, GridUnitType.Star)
			: new GridLength(1.0 - ratio, GridUnitType.Star);
	}

	private static decimal GetDecimal(Type type, object obj, string prop)
	{
		var pi = type.GetProperty(prop);
		return pi?.GetValue(obj) switch
		{
			decimal d => d,
			double d => (decimal)d,
			_ => 0
		};
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language)
		=> throw new NotSupportedException();
}
