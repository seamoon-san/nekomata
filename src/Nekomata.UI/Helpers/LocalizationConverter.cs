using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Nekomata.UI.Helpers;

public class LocalizationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string text)
        {
            string lookupKey = text;
            if (parameter is string prefix)
            {
                lookupKey = prefix + text;
            }

            var resource = Application.Current.TryFindResource(lookupKey);
            if (resource is string str)
            {
                return str.Replace("\\n", Environment.NewLine);
            }
            return resource ?? text;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
