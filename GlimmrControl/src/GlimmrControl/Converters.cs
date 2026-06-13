using System.Globalization;
using GlimmrControl.Models;
using Microsoft.Maui.Controls.Shapes;

namespace GlimmrControl;

/// <summary>Parses an SVG path-data string into a <see cref="Geometry" /> for chip icons.</summary>
public sealed class PathDataConverter : IValueConverter
{
	private static readonly PathGeometryConverter Inner = new();

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is string s && !string.IsNullOrWhiteSpace(s))
		{
			return Inner.ConvertFromInvariantString(s);
		}

		return null;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}

/// <summary>Maps device reachability to a status-dot color.</summary>
public sealed class ReachabilityToColorConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var key = value switch
		{
			DeviceReachability.Online => "GlimmrGreen",
			DeviceReachability.Offline => "GlimmrDanger",
			_ => "GlimmrMuted"
		};
		return Application.Current?.Resources.TryGetValue(key, out var c) == true ? c : Colors.Gray;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}

/// <summary>Inverts a boolean (e.g. show X when not Y).</summary>
public sealed class InvertedBoolConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is bool b && !b;

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is bool b && !b;
}

/// <summary>True when a string is non-empty (drives visibility of optional rows).</summary>
public sealed class StringNotEmptyConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		!string.IsNullOrWhiteSpace(value as string);

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
