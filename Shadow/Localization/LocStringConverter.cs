using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Shadow.Abstractions;

namespace Shadow.Localization;

public sealed class LocStringConverter : IValueConverter
{
    public static LocStringConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string key || string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return ShadowLocalizer.Instance[key];
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
