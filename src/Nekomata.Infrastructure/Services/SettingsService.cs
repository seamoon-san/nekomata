using System;
using System.IO;
using System.Threading.Tasks;
using Nekomata.Core.Interfaces;
using Nekomata.Models;
using Newtonsoft.Json;

using System.Globalization;
using System.Linq;

namespace Nekomata.Infrastructure.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private AppSettings? _cachedSettings;

    public SettingsService()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nekomata");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        _settingsPath = Path.Combine(folder, "settings.json");
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        if (_cachedSettings != null)
        {
            return _cachedSettings;
        }

        if (!File.Exists(_settingsPath))
        {
            _cachedSettings = CreateDefaultSettings();
            return _cachedSettings;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_settingsPath);
            _cachedSettings = JsonConvert.DeserializeObject<AppSettings>(json) ?? CreateDefaultSettings();
            return _cachedSettings;
        }
        catch
        {
            _cachedSettings = CreateDefaultSettings();
            return _cachedSettings;
        }
    }

    private AppSettings CreateDefaultSettings()
    {
        var settings = new AppSettings();
        try
        {
            var region = RegionInfo.CurrentRegion.TwoLetterISORegionName;
            var chineseRegions = new[] { "CN", "HK", "MO", "TW" };

            if (chineseRegions.Contains(region))
            {
                settings.InterfaceLanguage = "zh-CN";
            }
            else
            {
                settings.InterfaceLanguage = "en-US";
            }
        }
        catch
        {
            // Fallback to default (zh-CN) if region detection fails
            settings.InterfaceLanguage = "zh-CN"; 
        }
        return settings;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        _cachedSettings = settings;
        var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
        await File.WriteAllTextAsync(_settingsPath, json);
    }
}
