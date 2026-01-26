using System;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace Nekomata.UI.Helpers;

public static class ThemeDetector
{
    public static event EventHandler<ApplicationTheme>? ThemeChanged;

    private static bool _isWatching;

    public static void Watch()
    {
        if (_isWatching) return;
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        _isWatching = true;
    }

    public static void UnWatch()
    {
        if (!_isWatching) return;
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
        _isWatching = false;
    }

    private static void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            ThemeChanged?.Invoke(null, GetSystemTheme());
        }
    }

    public static ApplicationTheme GetSystemTheme()
    {
        const string registryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        const string registryValueName = "AppsUseLightTheme";

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(registryKeyPath);
            var registryValueObject = key?.GetValue(registryValueName);
            if (registryValueObject == null)
            {
                return ApplicationTheme.Light;
            }

            int registryValue = (int)registryValueObject;
            return registryValue > 0 ? ApplicationTheme.Light : ApplicationTheme.Dark;
        }
        catch
        {
            return ApplicationTheme.Light;
        }
    }
}
