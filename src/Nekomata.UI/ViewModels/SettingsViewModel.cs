using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
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
    
    public List<KeyValuePair<string, string>> AvailableLanguages { get; } = new()
    {
        new("中文", "zh-CN"),
        new("English", "en-US")
    };

    private KeyValuePair<string, string> _selectedLanguage;
    public KeyValuePair<string, string> SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
            {
                Settings.InterfaceLanguage = value.Value;
                
                if (!_isInitializing)
                {
                    Application.Current.Dispatcher.InvokeAsync(async () => 
                    {
                        try
                        {
                            Mouse.OverrideCursor = Cursors.Wait;
                            // Give UI a moment to update (e.g. close combobox)
                            await Task.Delay(50);
                            _localizationService.SetLanguage(value.Value);
                            SaveSettings();
                        }
                        finally
                        {
                            Mouse.OverrideCursor = null;
                        }
                    }, System.Windows.Threading.DispatcherPriority.ContextIdle);
                }
            }
        }
    }

    public ObservableCollection<KeyValuePair<string, string>> AvailableThemes { get; } = new();

    private string _selectedTheme = "Light";
    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                Settings.Theme = value;
                
                if (!_isInitializing)
                {
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
                    
                    SaveSettings();
                }
            }
        }
    }

    public SettingsViewModel(ISettingsService settingsService, ILocalizationService localizationService)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;
        
        _localizationService.LanguageChanged += OnLanguageChanged;
        UpdateAvailableThemes();
        
        LoadSettingsCommand.Execute(null);
    }

    private void OnLanguageChanged(object? sender, System.EventArgs e)
    {
        UpdateAvailableThemes();
    }

    private void UpdateAvailableThemes()
    {
        var currentTheme = SelectedTheme;
        _isInitializing = true;
        try
        {
            AvailableThemes.Clear();
            AvailableThemes.Add(new(_localizationService.GetString("Theme_Light"), "Light"));
            AvailableThemes.Add(new(_localizationService.GetString("Theme_Dark"), "Dark"));
            AvailableThemes.Add(new(_localizationService.GetString("Theme_Auto"), "Auto"));
            
            SelectedTheme = currentTheme;
        }
        finally
        {
            _isInitializing = false;
        }
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
            var match = AvailableLanguages.FirstOrDefault(x => x.Value == currentCulture);
            
            if (!string.IsNullOrEmpty(match.Value))
            {
                SelectedLanguage = match;
            }
            else
            {
                // Priority 2: Saved setting
                SelectedLanguage = AvailableLanguages.FirstOrDefault(x => x.Value == Settings.InterfaceLanguage);
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
