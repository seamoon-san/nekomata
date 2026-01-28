using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nekomata.Core.Interfaces;
using Nekomata.Core.Services;
using Nekomata.EngineAdapters;
using Nekomata.Infrastructure.Data;
using Nekomata.Infrastructure.Repositories;
using Nekomata.Infrastructure.Services;
using Nekomata.UI.ViewModels;
using Serilog;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Nekomata.UI.Helpers;

namespace Nekomata.UI;

public partial class App : Application
{
    private IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
                .WriteTo.Debug())
            .ConfigureServices((context, services) =>
            {
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlite("Data Source=nekomata.db"));

                services.AddHttpClient();
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<ILocalizationService, Services.LocalizationService>();
                services.AddSingleton<ISnackbarService, SnackbarService>();

                services.AddScoped<ITranslationRepository, TranslationRepository>();
                services.AddScoped<IProjectService, ProjectService>();
                
                // Adapters
                services.AddTransient<IGameEngineAdapter, RPGMakerMVAdapter>();
                services.AddTransient<IGameEngineAdapter, RPGMakerVXAdapter>();
                
                services.AddTransient<MainViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        using (var scope = _host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        // Initialize Localization
        var settingsService = _host.Services.GetRequiredService<ISettingsService>();
        var localizationService = _host.Services.GetRequiredService<ILocalizationService>();
        var settings = await settingsService.LoadSettingsAsync();
        localizationService.SetLanguage(settings.InterfaceLanguage);

        // Apply Theme
        if (settings.Theme == "Auto")
        {
            ThemeDetector.Watch();
            var theme = ThemeDetector.GetSystemTheme();
            ApplicationThemeManager.Apply(theme);
        }
        else
        {
            var theme = settings.Theme == "Dark" ? ApplicationTheme.Dark : ApplicationTheme.Light;
            ApplicationThemeManager.Apply(theme);
        }

        ThemeDetector.ThemeChanged += (s, theme) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ThemeTransitionHelper.ApplyThemeSmoothly(theme);
            });
        };

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        if (e.Args.Length > 0)
        {
            var path = e.Args[0];
            if (System.IO.File.Exists(path) && System.IO.Path.GetExtension(path).Equals(".nkproj", StringComparison.OrdinalIgnoreCase))
            {
                await mainWindow.ViewModel.OpenProjectFromFileAsync(path);
            }
        }

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();

        base.OnExit(e);
    }
}
