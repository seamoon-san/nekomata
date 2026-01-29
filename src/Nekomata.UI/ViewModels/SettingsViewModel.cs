using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nekomata.Core.Interfaces;
using Nekomata.Models;
using Nekomata.UI.Helpers;
using Wpf.Ui.Appearance;

namespace Nekomata.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;

    private bool _isInitializing;

    [ObservableProperty]
    private AppSettings _settings = new();
    
    public System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> AvailableLanguages { get; } = new()
    {
        new("中文", "zh-CN"),
        new("English", "en-US")
    };

    private System.Collections.Generic.KeyValuePair<string, string> _selectedLanguage;
    public System.Collections.Generic.KeyValuePair<string, string> SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
            {
                Settings.InterfaceLanguage = value.Value;
                _localizationService.SetLanguage(value.Value);
            }
        }
    }

    public System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> AvailableThemes { get; } = new();

    private string _selectedTheme = "Light";
    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                Settings.Theme = value;
                if (value == "Auto")
                {
                    ThemeDetector.Watch();
                    var theme = ThemeDetector.GetSystemTheme();
                    ThemeTransitionHelper.ApplyThemeSmoothly(theme);
                }
                else
                {
                    ThemeDetector.UnWatch();
                    var theme = value == "Dark" ? ApplicationTheme.Dark : ApplicationTheme.Light;
                    ThemeTransitionHelper.ApplyThemeSmoothly(theme);
                }
            }
        }
    }

    public SettingsViewModel(ISettingsService settingsService, ILocalizationService localizationService)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;
        
        AvailableThemes.Add(new(_localizationService.GetString("Theme_Light"), "Light"));
        AvailableThemes.Add(new(_localizationService.GetString("Theme_Dark"), "Dark"));
        AvailableThemes.Add(new(_localizationService.GetString("Theme_Auto"), "Auto"));
        
        LoadSettingsCommand.Execute(null);
    }

    [RelayCommand]
    private async Task LoadSettings()
    {
        _isInitializing = true;
        try
        {
            Settings = await _settingsService.LoadSettingsAsync();
            
            // Initialize SelectedLanguage
            // Priority 1: Current active culture (to prevent reverting unsaved changes when re-opening settings)
            var currentCulture = _localizationService.CurrentCulture.Name;
            var match = AvailableLanguages.Find(x => x.Value == currentCulture);
            
            if (!string.IsNullOrEmpty(match.Value))
            {
                SelectedLanguage = match;
            }
            else
            {
                // Priority 2: Saved setting
                SelectedLanguage = AvailableLanguages.Find(x => x.Value == Settings.InterfaceLanguage);
            }

            // Priority 3: Default to first available
            if (string.IsNullOrEmpty(SelectedLanguage.Value))
            {
                 SelectedLanguage = AvailableLanguages[0];
            }

            SelectedTheme = Settings.Theme ?? "Auto";
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private async void SaveSettings()
    {
        try
        {
            await _settingsService.SaveSettingsAsync(Settings);
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
}
