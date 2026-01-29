using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using Nekomata.Core.Interfaces;

namespace Nekomata.UI.Services;

public class LocalizationService : ILocalizationService
{
    public CultureInfo CurrentCulture { get; private set; } = new CultureInfo("en-US");

    public void SetLanguage(string cultureCode)
    {
        if (string.IsNullOrEmpty(cultureCode)) return;

        var culture = new CultureInfo(cultureCode);
        CurrentCulture = culture;

        var dictionary = new ResourceDictionary();
        try
        {
            // Try specific culture first (e.g., zh-CN)
            dictionary.Source = new Uri($"pack://application:,,,/Nekomata.UI;component/Resources/Languages/{cultureCode}.xaml", UriKind.Absolute);
        }
        catch
        {
            try
            {
                // Fallback to neutral culture (e.g., zh-CN -> zh, though our file is zh-CN)
                // If we strictly follow the file naming, we should be fine.
                // Let's stick to the exact filename match for now.
                // If failed, maybe try en-US
                dictionary.Source = new Uri("pack://application:,,,/Nekomata.UI;component/Resources/Languages/en-US.xaml", UriKind.Absolute);
            }
            catch (Exception ex)
            {
                // Log error or ignore
                System.Diagnostics.Debug.WriteLine($"Failed to load language resource: {ex.Message}");
                return;
            }
        }

        // Find if we already have a language dictionary loaded and replace it
        // We identify it by checking if it contains a known key, e.g., "AppTitle"
        var oldDict = Application.Current.Resources.MergedDictionaries.FirstOrDefault(d => d.Contains("AppTitle"));
        
        if (oldDict != null)
        {
            Application.Current.Resources.MergedDictionaries.Remove(oldDict);
        }
        
        Application.Current.Resources.MergedDictionaries.Add(dictionary);
        
        // Update thread culture
        System.Threading.Thread.CurrentThread.CurrentCulture = culture;
        System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
    }

    public string GetString(string key)
    {
        if (Application.Current.TryFindResource(key) is string resource)
        {
            return resource.Replace("\\n", Environment.NewLine);
        }
        return key;
    }
}
