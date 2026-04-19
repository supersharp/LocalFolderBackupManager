using System.Globalization;
using System.Windows.Data;

namespace LocalFolderBackupManager.Converters;

/// <summary>
/// Converts a string to a boolean: true if string is null or empty, false otherwise
/// </summary>
public class StringEmptyToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
