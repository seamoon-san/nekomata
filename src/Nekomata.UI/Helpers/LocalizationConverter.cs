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
            // Try to find a localized string.
            // The key might be the raw value (e.g. "Show Text"), so we need to map it to a resource key if possible,
            // or assume the caller provides a prefix in the parameter.
            
            string resourceKey = key;
            if (parameter is string prefix)
            {
                // Map spaces to nothing or underscores if needed.
                // Our keys are like "Filter_ShowText" for value "Show Text".
                // So "Show Text" -> "ShowText".
                var cleanKey = key.Replace(" ", "");
                resourceKey = $"{prefix}_{cleanKey}";
            }

            if (Application.Current.TryFindResource(resourceKey) is string localized)
            {
                return localized;
            }
            
            // Fallback: Return the original key if no translation found
            return key;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
