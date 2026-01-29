using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Nekomata.UI.Helpers;

public class LocalizationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string key)
        {
            var resource = Application.Current.TryFindResource(key);
            if (resource is string str)
            {
                return str.Replace("\\n", Environment.NewLine);
            }
            return resource ?? key;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
