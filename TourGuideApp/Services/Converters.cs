using System.Globalization;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;

namespace TourGuideApp.Services;

public class ErrorColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (value is bool isError)
        {
            return isError ? Colors.Red : Colors.Green;
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotImplementedException();
    }
}

public class RotationConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (value is bool isVisible)
        {
            return isVisible ? 180.0 : 0.0;
        }
        return 0.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter giúp dịch và định dạng chuỗi động.
/// Cách dùng: Text="{Binding Value, Converter={StaticResource TranslateConverter}, ConverterParameter='key_name'}"
/// </summary>
public class TranslateConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (parameter is string key)
        {
            string format = LocalizationService.Get(key);
            return string.Format(format, value);
        }
        return value?.ToString() ?? string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotImplementedException();
    }
}
