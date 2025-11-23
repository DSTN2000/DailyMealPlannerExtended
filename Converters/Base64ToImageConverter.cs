using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using System.Globalization;

namespace DailyMealPlannerExtended.Converters;

public class Base64ToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string base64String || string.IsNullOrEmpty(base64String))
            return null;

        try
        {
            var imageBytes = System.Convert.FromBase64String(base64String);
            using var stream = new MemoryStream(imageBytes);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
