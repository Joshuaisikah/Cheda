using System.Globalization;

namespace Cheda.App.Converters;

// True when value is not null / not empty string
public sealed class NullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s ? !string.IsNullOrEmpty(s) : value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Inverts a bool
public sealed class InvertBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;
}

// True when int > 0
public sealed class GreaterThanZeroConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int n && n > 0 || value is decimal d && d > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// True when value == ConverterParameter
public sealed class EqualConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int n && int.TryParse(parameter?.ToString(), out var p))
            return n == p;
        return Equals(value?.ToString(), parameter?.ToString());
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// PIN dot: returns filled color if the dot index (1-based) <= entered digits length
public sealed class PinDotConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var dots  = (value as string)?.Length ?? 0;
        var index = int.TryParse(parameter?.ToString(), out var p) ? p : 0;
        return dots >= index
            ? Application.Current!.Resources["PinDotFilled"]
            : Application.Current!.Resources["PinDotEmpty"];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
