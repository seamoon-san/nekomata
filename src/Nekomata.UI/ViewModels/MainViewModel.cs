using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Nekomata.Core.Exceptions;
using Nekomata.Core.Interfaces;
using Nekomata.Models;
using Newtonsoft.Json;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace Nekomata.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IProjectService _projectService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILocalizationService _localizationService;
    private readonly ISnackbarService _snackbarService;

    [ObservableProperty]
    private ObservableCollection<TranslationProject> _projects = new();

    [ObservableProperty]
    private TranslationProject? _selectedProject;

    [ObservableProperty]
    private TranslationUnit? _selectedUnit;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private ICollectionView? _translationUnitsView;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedFilterContext = "All";

    [ObservableProperty]
    private string _selectedGroupOption = "None";

    [ObservableProperty]
    private double _translationProgress;

    [ObservableProperty]
    private string _progressDescription = string.Empty;

    private System.Threading.CancellationTokenSource? _searchCts;

    public List<string> FilterContexts { get; } = new() { "All", "Choice", "ShowText", "Name", "CommonEvent" };
    public List<string> GroupOptions { get; } = new() { "None", "Context", "OriginalText", "HumanTranslation" };

    async partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new System.Threading.CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            await Task.Delay(300, token); // Debounce 300ms
            if (token.IsCancellationRequested) return;

            // Run filter on UI thread (CollectionView requires it)
            Application.Current.Dispatcher.Invoke(() =>
            {
                TranslationUnitsView?.Refresh();
            });
        }
        catch (TaskCanceledException) { }
    } 

    partial void OnSelectedFilterContextChanged(string value) => TranslationUnitsView?.Refresh();
    partial void OnSelectedGroupOptionChanged(string value) => UpdateGrouping(value);

    private void UpdateGrouping(string option)
    {
        if (TranslationUnitsView == null) return;

        using (TranslationUnitsView.DeferRefresh())
        {
            TranslationUnitsView.GroupDescriptions.Clear();
            if (option != "None")
            {
                string propertyName = option switch
                {
                    "OriginalText" => nameof(TranslationUnit.OriginalText),
                    "HumanTranslation" => nameof(TranslationUnit.HumanTranslation),
                    _ => nameof(TranslationUnit.Context)
                };
                TranslationUnitsView.GroupDescriptions.Add(new PropertyGroupDescription(propertyName));
            }
        }
    }


    private readonly HashSet<Guid> _loadedProjectIds = new();

    public MainViewModel(
        IProjectService projectService, 
        IServiceProvider serviceProvider, 
        ILocalizationService localizationService,
        ISnackbarService snackbarService)
    {
        _projectService = projectService;
        _serviceProvider = serviceProvider;
        _localizationService = localizationService;
        _snackbarService = snackbarService;
        
        // No longer loading history from DB
        // LoadProjectsCommand.Execute(null);
    }

    partial void OnSelectedProjectChanging(TranslationProject? value)
    {
        if (SelectedProject != null)
        {
            foreach (var unit in SelectedProject.TranslationUnits)
                unit.PropertyChanged -= OnUnitPropertyChanged;
            SelectedProject.TranslationUnits.CollectionChanged -= OnUnitsCollectionChanged;
        }
    }

    async partial void OnSelectedProjectChanged(TranslationProject? value)
    {
        if (value != null)
        {
            foreach (var unit in value.TranslationUnits)
                unit.PropertyChanged += OnUnitPropertyChanged;
            value.TranslationUnits.CollectionChanged += OnUnitsCollectionChanged;

            TranslationUnitsView = CollectionViewSource.GetDefaultView(value.TranslationUnits);
            TranslationUnitsView.Filter = FilterTranslationUnit;
            UpdateGrouping(SelectedGroupOption);
        }
        else
        {
            TranslationUnitsView = null;
        }

        UpdateProgress();

        if (value != null && !_loadedProjectIds.Contains(value.Id))
        {
            if (value.TranslationUnits.Count > 0)
            {
                _loadedProjectIds.Add(value.Id);
            }
            else
            {
                await LoadProjectDetails(value);
            }
        }
    }

    private void OnUnitsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (TranslationUnit item in e.OldItems)
                item.PropertyChanged -= OnUnitPropertyChanged;
        }
        if (e.NewItems != null)
        {
            foreach (TranslationUnit item in e.NewItems)
                item.PropertyChanged += OnUnitPropertyChanged;
        }
        UpdateProgress();
    }

    private void OnUnitPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TranslationUnit.HumanTranslation))
        {
            UpdateProgress();
        }
    }

    private void UpdateProgress()
    {
        if (SelectedProject == null || SelectedProject.TranslationUnits == null || SelectedProject.TranslationUnits.Count == 0)
        {
            TranslationProgress = 0;
            ProgressDescription = "0% (0/0)";
            return;
        }

        var total = SelectedProject.TranslationUnits.Count;
        var translated = SelectedProject.TranslationUnits.Count(u => !string.IsNullOrEmpty(u.HumanTranslation));
        
        TranslationProgress = (double)translated / total * 100;
        ProgressDescription = $"{TranslationProgress:F1}% ({translated}/{total})";
    }

    private async Task LoadProjectDetails(TranslationProject project)
    {
        IsBusy = true;
        try
        {
            // If loaded from file, we might already have details. 
            // If from DB (legacy), we might need this.
            // For file-based workflow, usually everything is loaded at once.
            // But let's keep this as fallback if project came from Service/DB originally.
            var fullProject = await _projectService.GetProjectAsync(project.Id);
            if (fullProject != null)
            {
                _loadedProjectIds.Add(fullProject.Id);
                
                var index = Projects.IndexOf(project);
                if (index != -1)
                {
                    Projects[index] = fullProject;
                    SelectedProject = fullProject;
                }
            }
        }
        catch (Exception)
        {
            // Fail silently or log, as it might be a new file-based project not in DB
            // MessageBox.Show($"Error loading project details: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateProject(string gamePath)
    {
        IsBusy = true;
        try
        {
            // For now, assume RPG Maker MV/MZ if dropped
            // Run in background thread to avoid UI freeze during heavy parsing
            var project = await Task.Run(() => _projectService.CreateProjectAsync(gamePath, "RPG Maker"));
            
            // Auto-set default FilePath if possible, or leave empty to prompt on save
            project.FilePath = System.IO.Path.Combine(gamePath, $"{project.Name}.nkproj");

            Projects.Add(project);
            SelectedProject = project;
            _loadedProjectIds.Add(project.Id); // Created project has units loaded
        }
        catch (GameExtractionRequiredException ex)
        {
            var msg = string.Format(_localizationService.GetString("Msg_ExtractionRequired"), ex.RequiredPath);
            System.Windows.MessageBox.Show(msg, _localizationService.GetString("Title_ExtractionRequired"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            // Stop process as requested
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(string.Format(_localizationService.GetString("Error_CreatingProject"), ex.Message), _localizationService.GetString("Title_Error"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenGameFile()
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = _localizationService.GetString("Filter_GameExecutable"),
            Title = _localizationService.GetString("Title_SelectGameExecutable")
        };

        if (openFileDialog.ShowDialog() == true)
        {
            var directory = System.IO.Path.GetDirectoryName(openFileDialog.FileName);
            if (!string.IsNullOrEmpty(directory))
            {
                await CreateProject(directory);
            }
        }
    }

    [RelayCommand]
    private async Task SaveProject()
    {
        if (SelectedProject == null) return;

        if (string.IsNullOrEmpty(SelectedProject.FilePath))
        {
             var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"{SelectedProject.Name}.nkproj",
                Filter = _localizationService.GetString("Filter_NekomataProject"),
                Title = _localizationService.GetString("Title_SaveProject")
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                SelectedProject.FilePath = saveFileDialog.FileName;
            }
            else
            {
                return;
            }
        }

        IsBusy = true;
        try 
        {
             // Update timestamp
             SelectedProject.UpdatedAt = DateTime.Now;

             var json = JsonConvert.SerializeObject(SelectedProject, Formatting.Indented, new JsonSerializerSettings
             {
                 ReferenceLoopHandling = ReferenceLoopHandling.Ignore
             });
             await System.IO.File.WriteAllTextAsync(SelectedProject.FilePath, json);
             
             // Optional: visual feedback
             _snackbarService.Show(
                _localizationService.GetString("Title_Success"),
                _localizationService.GetString("Msg_ProjectSaved"),
                ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.Save24),
                TimeSpan.FromSeconds(2)
            );
        }
        catch (Exception ex)
        {
             System.Windows.MessageBox.Show(string.Format(_localizationService.GetString("Error_SavingProject"), ex.Message), _localizationService.GetString("Title_Error"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CloseProject(TranslationProject project)
    {
        if (project == null) return;

        // Auto-save before closing? Or just close?
        // User said "Save and Close".
        // If the project passed here is the SelectedProject, we can use SaveProjectCommand logic.
        // If it's not selected, we need to temporarily handle it.
        
        // Let's implement specific save logic for the target project to be safe.
        
        IsBusy = true;
        try
        {
            if (string.IsNullOrEmpty(project.FilePath))
            {
                 // If no path, we must ask.
                 var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"{project.Name}.nkproj",
                    Filter = _localizationService.GetString("Filter_NekomataProject"),
                    Title = _localizationService.GetString("Title_SaveProject")
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    project.FilePath = saveFileDialog.FileName;
                }
                else
                {
                    // If user cancels save, do we still close? 
                    // Let's assume yes, or maybe return. 
                    // For "Save and Close", cancellation usually stops the action.
                    IsBusy = false;
                    return;
                }
            }

            project.UpdatedAt = DateTime.Now;
            var json = JsonConvert.SerializeObject(project, Formatting.Indented, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
            await System.IO.File.WriteAllTextAsync(project.FilePath, json);
            
            // Remove from list
            Projects.Remove(project);
            if (SelectedProject == project)
            {
                SelectedProject = null;
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(string.Format(_localizationService.GetString("Error_SavingClosingProject"), ex.Message), _localizationService.GetString("Title_Error"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyTranslation()
    {
        if (SelectedProject == null) return;

        IsBusy = true;
        try
        {
            // Create a safe output directory
            var outputPath = System.IO.Path.Combine(SelectedProject.GamePath, "TranslatedData");
            
            await _projectService.ApplyTranslationAsync(SelectedProject, outputPath);
            
            _snackbarService.Show(
                _localizationService.GetString("Title_Success"),
                string.Format("{0}\n{1} {2}\n{3}", 
                    _localizationService.GetString("Msg_TranslationApplied"), 
                    _localizationService.GetString("Msg_FilesExported"), 
                    outputPath, 
                    _localizationService.GetString("Msg_CopyInstruction")),
                ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.CheckmarkCircle24),
                TimeSpan.FromSeconds(5)
            );
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(string.Format(_localizationService.GetString("Error_ApplyingTranslation"), ex.Message), _localizationService.GetString("Title_Error"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var settingsViewModel = _serviceProvider.GetRequiredService<SettingsViewModel>();
        var settingsWindow = new SettingsWindow(settingsViewModel);
        settingsWindow.Owner = Application.Current.MainWindow;
        settingsWindow.ShowDialog();
    }

    [RelayCommand]
    private async Task OpenProject()
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = _localizationService.GetString("Filter_OpenProject"),
            Title = _localizationService.GetString("Title_OpenProject")
        };

        if (openFileDialog.ShowDialog() == true)
        {
            IsBusy = true;
            try
            {
                var json = await System.IO.File.ReadAllTextAsync(openFileDialog.FileName);
                var project = await Task.Run(() => JsonConvert.DeserializeObject<TranslationProject>(json));
                
                if (project != null)
                {
                    // Set FilePath to where we opened it from
                    project.FilePath = openFileDialog.FileName;
                    
                    // Check if already open
                    var existing = Projects.FirstOrDefault(p => p.Id == project.Id);
                    if (existing != null)
                    {
                        SelectedProject = existing;
                        // Maybe reload data?
                    }
                    else
                    {
                        Projects.Add(project);
                        SelectedProject = project;
                        _loadedProjectIds.Add(project.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(_localizationService.GetString("Error_OpeningProject"), ex.Message), _localizationService.GetString("Title_Error"));
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    [RelayCommand]
    private async Task ExportProject(TranslationProject project)
    {
        // Legacy or explicit export. Can keep it.
        // Logic similar to SaveProject but explicitly "Save Copy As"
        if (project == null) return;

        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"{project.Name}_export.json",
            Filter = _localizationService.GetString("Filter_ExportProject"),
            Title = _localizationService.GetString("Title_ExportProject")
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            IsBusy = true;
            try
            {
                var json = JsonConvert.SerializeObject(project, Formatting.Indented, new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
                await System.IO.File.WriteAllTextAsync(saveFileDialog.FileName, json);
                _snackbarService.Show(
                    _localizationService.GetString("Title_Success"), 
                    _localizationService.GetString("Msg_ProjectExported"), 
                    ControlAppearance.Success, 
                    new SymbolIcon(SymbolRegular.ArrowExportUp24), 
                    TimeSpan.FromSeconds(2)
                );
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(string.Format(_localizationService.GetString("Error_ExportingProject"), ex.Message), _localizationService.GetString("Title_Error"));
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    private bool FilterTranslationUnit(object obj)
    {
        if (obj is not TranslationUnit unit) return false;

        if (SelectedFilterContext != "All")
        {
            if (string.IsNullOrEmpty(unit.Context) || !unit.Context.Contains(SelectedFilterContext, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            bool match = (unit.OriginalText?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                      || (unit.HumanTranslation?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                      || (unit.Context?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false);
            if (!match) return false;
        }

        return true;
    }

    [RelayCommand]
    private async Task BatchApplyTranslation()
    {
        if (SelectedUnit == null || string.IsNullOrEmpty(SelectedUnit.HumanTranslation)) return;
        
        var targetText = SelectedUnit.HumanTranslation;
        var original = SelectedUnit.OriginalText;
        
        IsBusy = true;
        try
        {
            var candidates = await Task.Run(() => 
                SelectedProject?.TranslationUnits
                .Where(u => u.OriginalText == original && u != SelectedUnit)
                .ToList());

            if (candidates == null || candidates.Count == 0)
            {
                 _snackbarService.Show("Info", "No other units with same original text found.", ControlAppearance.Info, new SymbolIcon(SymbolRegular.Info24), TimeSpan.FromSeconds(2));
                 return;
            }

            if (candidates.Any(u => !string.IsNullOrEmpty(u.HumanTranslation)))
            {
                var result = System.Windows.MessageBox.Show(
                    "Some units already have translations. Overwrite?", 
                    "Confirm Overwrite", 
                    System.Windows.MessageBoxButton.YesNo, 
                    System.Windows.MessageBoxImage.Question);
                
                if (result == System.Windows.MessageBoxResult.No) return;
            }

            await Task.Run(() => 
            {
                foreach (var unit in candidates)
                {
                    unit.HumanTranslation = targetText;
                }
            });
            
            _snackbarService.Show("Success", $"Applied to {candidates.Count} units.", ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle24), TimeSpan.FromSeconds(2));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenFindReplace()
    {
        var window = new FindReplaceWindow(this);
        window.Owner = Application.Current.MainWindow;
        window.Show();
    }

    public void FindNext(string text)
    {
        if (string.IsNullOrEmpty(text) || TranslationUnitsView == null) return;

        var iterator = TranslationUnitsView.GetEnumerator();
        TranslationUnit? found = null;
        
        bool startSearching = (SelectedUnit == null);
        TranslationUnit? firstMatch = null; 

        while (iterator.MoveNext())
        {
            if (iterator.Current is not TranslationUnit unit) continue;

            // Check if this is the SelectedUnit
            if (!startSearching && unit == SelectedUnit)
            {
                startSearching = true;
                continue;
            }

            // Check match
            bool match = (unit.HumanTranslation?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false);
            
            if (match)
            {
                if (firstMatch == null) 
                {
                    firstMatch = unit;
                }

                if (startSearching)
                {
                    found = unit;
                    break;
                }
            }
        }

        // If not found after current position, wrap around
        if (found == null && firstMatch != null)
        {
            found = firstMatch;
        }

        if (found != null)
        {
            SelectedUnit = found;
        }
        else
        {
             System.Windows.MessageBox.Show("Not found.");
        }
    }

    public void ReplaceOne(string find, string replace)
    {
        if (SelectedUnit == null) 
        {
            FindNext(find); 
            return;
        }
        
        if (SelectedUnit.HumanTranslation != null && SelectedUnit.HumanTranslation.Contains(find, StringComparison.OrdinalIgnoreCase))
        {
            SelectedUnit.HumanTranslation = SelectedUnit.HumanTranslation.Replace(find, replace, StringComparison.OrdinalIgnoreCase);
            FindNext(find);
        }
        else
        {
            FindNext(find);
        }
    }

    public async Task ReplaceAll(string find, string replace)
    {
         if (TranslationUnitsView == null) return;

         IsBusy = true;
         try
         {
             var itemsToUpdate = new List<TranslationUnit>();
             foreach (var item in TranslationUnitsView)
             {
                 if (item is TranslationUnit unit && 
                     unit.HumanTranslation != null && 
                     unit.HumanTranslation.Contains(find, StringComparison.OrdinalIgnoreCase))
                 {
                     itemsToUpdate.Add(unit);
                 }
             }

             if (itemsToUpdate.Count == 0) 
             {
                 System.Windows.MessageBox.Show("No occurrences found.");
                 return;
             }

             int count = 0;
             await Task.Run(async () => 
             {
                 foreach (var unit in itemsToUpdate)
                 {
                     var newText = unit.HumanTranslation!.Replace(find, replace, StringComparison.OrdinalIgnoreCase);
                     
                     if (unit.HumanTranslation != newText)
                     {
                         unit.HumanTranslation = newText;
                         count++;
                         
                         // Throttle every 100 items to keep UI responsive
                         if (count % 100 == 0) await Task.Delay(1);
                     }
                 }
             });
             
             System.Windows.MessageBox.Show(string.Format("Replaced {0} occurrences.", count));
         }
         finally
         {
             IsBusy = false;
         }
    }
}


