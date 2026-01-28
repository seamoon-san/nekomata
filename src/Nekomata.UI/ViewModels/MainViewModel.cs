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

    [ObservableProperty]
    private double _currentScrollOffset;

    private System.Threading.CancellationTokenSource? _searchCts;

    public ObservableCollection<string> FilterContexts { get; } = new() { "All", "Choice", "Show Text", "Name", "CommonEvent" };
    public ObservableCollection<string> GroupOptions { get; } = new() { "None", "Context", "OriginalText", "HumanTranslation" };

    async partial void OnSearchTextChanged(string value)
    {
        await ApplyFilterAsync();
    } 

    async partial void OnSelectedFilterContextChanged(string value) => await ApplyFilterAsync();
    async partial void OnSelectedGroupOptionChanged(string value) => await ChangeGroupOptionAsync(value);

    private async Task ChangeGroupOptionAsync(string option)
    {
        IsBusy = true;
        try 
        {
            // 1. Clear View to force UI to release resources/layout
            await Application.Current.Dispatcher.InvokeAsync(() => 
            {
                TranslationUnitsView = null;
            });

            // 2. Allow UI thread to breathe and finish clearing
            await Task.Delay(100);

            // 3. Rebuild view and apply grouping
            // Note: We need the current filtered list. 
            // Since we don't store the filtered list separately, we might need to re-run filter or access source.
            // But usually ApplyFilterAsync handles View creation.
            // Let's reuse ApplyFilterAsync logic but force the grouping option.
            
            // Wait, simply calling ApplyFilterAsync() will read SelectedGroupOption and rebuild the view.
            // So we just need to ensure ApplyFilterAsync is called.
            // Since SelectedGroupOption is bound, it's already updated.
            
            // However, to ensure the "Clear View" step happens, we can call ApplyFilterAsync
            // but we want to make sure we don't duplicate work.
            
            await ApplyFilterAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateGroupingForView(ICollectionView? view, string option)
    {
        if (view == null) return;

        // Ensure we are on UI thread for View operations
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(() => UpdateGroupingForView(view, option));
            return;
        }

        using (view.DeferRefresh())
        {
            view.GroupDescriptions.Clear();
            if (option != "None")
            {
                string propertyName = option switch
                {
                    "OriginalText" => nameof(TranslationUnit.OriginalText),
                    "HumanTranslation" => nameof(TranslationUnit.HumanTranslation),
                    _ => nameof(TranslationUnit.Context)
                };
                view.GroupDescriptions.Add(new PropertyGroupDescription(propertyName));
            }
        }
    }

    private async Task ApplyFilterAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new System.Threading.CancellationTokenSource();
        var token = _searchCts.Token;

        var project = SelectedProject;
        if (project == null)
        {
            Application.Current.Dispatcher.Invoke(() => TranslationUnitsView = null);
            return;
        }

        var searchText = SearchText;
        var filterContext = SelectedFilterContext;

        try
        {
            // Debounce
            await Task.Delay(300, token);
            if (token.IsCancellationRequested) return;

            IsBusy = true;

            var filteredList = await Task.Run(() =>
            {
                var units = project.TranslationUnits;
                IEnumerable<TranslationUnit> query = units;

                if (filterContext != "All")
                {
                    query = query.Where(u => !string.IsNullOrEmpty(u.Context) && u.Context.Contains(filterContext, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    query = query.Where(u =>
                        (u.OriginalText != null && u.OriginalText.Contains(searchText, StringComparison.OrdinalIgnoreCase)) ||
                        (u.HumanTranslation != null && u.HumanTranslation.Contains(searchText, StringComparison.OrdinalIgnoreCase)) ||
                        (u.Context != null && u.Context.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    );
                }

                return query.ToList();
            }, token);

            if (token.IsCancellationRequested) return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var newView = new ListCollectionView(filteredList);
                UpdateGroupingForView(newView, SelectedGroupOption);
                TranslationUnitsView = newView;
            });
        }
        catch (TaskCanceledException) { }
        finally
        {
            if (!token.IsCancellationRequested) IsBusy = false;
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

        _localizationService.LanguageChanged += OnLanguageChanged;
        
        // No longer loading history from DB
        // LoadProjectsCommand.Execute(null);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Refresh lists to trigger converter re-run
        RefreshCollection(FilterContexts);
        RefreshCollection(GroupOptions);
    }

    private void RefreshCollection(ObservableCollection<string> collection)
    {
        var items = collection.ToList();
        collection.Clear();
        foreach (var item in items) collection.Add(item);
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

            // Restore UI State
            // Note: Setting these properties might trigger filtering/grouping.
            // We should probably set them before calling ApplyFilterAsync if possible,
            // or let ApplyFilterAsync use them.
            
            // Suspend automatic filtering/grouping triggers if possible, or just set them.
            // The PropertyChanged handlers call Refresh/UpdateGrouping. 
            // We can temporarily ignore them or just let them run (might be redundant but safe).
            
            // 1. Search Text & Filter Context
            if (!string.IsNullOrEmpty(value.LastSearchText)) 
                SearchText = value.LastSearchText;
            else 
                SearchText = string.Empty;
                
            if (!string.IsNullOrEmpty(value.LastFilterContext))
            {
                if (value.LastFilterContext == "ShowText")
                    SelectedFilterContext = "Show Text";
                else
                    SelectedFilterContext = value.LastFilterContext;
            }
            else
                SelectedFilterContext = "All";
                
            // 2. Grouping
            if (!string.IsNullOrEmpty(value.LastGroupOption))
                SelectedGroupOption = value.LastGroupOption;
            else
                SelectedGroupOption = "None";

            // 3. Collapsed Groups
            CollapsedGroupNames.Clear();
            if (value.CollapsedGroups != null)
            {
                foreach (var name in value.CollapsedGroups)
                    CollapsedGroupNames.Add(name);
            }

            await ApplyFilterAsync();
            
            // 4. Restore Selection (after filter applied)
            if (value.LastSelectedUnitId.HasValue)
            {
                var unitToSelect = value.TranslationUnits.FirstOrDefault(u => u.Id == value.LastSelectedUnitId.Value);
                if (unitToSelect != null)
                {
                    SelectedUnit = unitToSelect;
                }
            }

            CurrentScrollOffset = value.LastScrollOffset;
        }
        else
        {
            TranslationUnitsView = null;
            CollapsedGroupNames.Clear();
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

    // Use HashSet for unique names, but convert to List for persistence
    public HashSet<string> CollapsedGroupNames { get; } = new();

    [RelayCommand]
    private async Task SaveProject()
    {
        if (SelectedProject == null) return;

        // Persist UI State
        SelectedProject.LastFilterContext = SelectedFilterContext;
        SelectedProject.LastGroupOption = SelectedGroupOption;
        SelectedProject.LastSearchText = SearchText;
        SelectedProject.LastSelectedUnitId = SelectedUnit?.Id;
        SelectedProject.LastScrollOffset = CurrentScrollOffset;
        SelectedProject.CollapsedGroups = CollapsedGroupNames.ToList();

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
            // If it's the currently selected project, update its state from UI first
            if (project == SelectedProject)
            {
                project.LastFilterContext = SelectedFilterContext;
                project.LastGroupOption = SelectedGroupOption;
                project.LastSearchText = SearchText;
                project.LastSelectedUnitId = SelectedUnit?.Id;
                project.LastScrollOffset = CurrentScrollOffset;
                project.CollapsedGroups = CollapsedGroupNames.ToList();
            }

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
            await OpenProjectFromFileAsync(openFileDialog.FileName);
        }
    }

    public async Task OpenProjectFromFileAsync(string filePath)
    {
        IsBusy = true;
        try
        {
            var json = await System.IO.File.ReadAllTextAsync(filePath);
            var project = await Task.Run(() => JsonConvert.DeserializeObject<TranslationProject>(json));
            
            if (project != null)
            {
                // Set FilePath to where we opened it from
                project.FilePath = filePath;
                
                // Check if already open
                var existing = Projects.FirstOrDefault(p => p.Id == project.Id);
                if (existing != null)
                {
                    SelectedProject = existing;
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
                 _snackbarService.Show(_localizationService.GetString("Title_Info"), _localizationService.GetString("Msg_NoCandidates"), ControlAppearance.Info, new SymbolIcon(SymbolRegular.Info24), TimeSpan.FromSeconds(2));
                 return;
            }

            if (candidates.Any(u => !string.IsNullOrEmpty(u.HumanTranslation)))
            {
                var result = System.Windows.MessageBox.Show(
                    _localizationService.GetString("Msg_ConfirmOverwrite"), 
                    _localizationService.GetString("Title_ConfirmOverwrite"), 
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
            
            _snackbarService.Show(_localizationService.GetString("Title_Success"), string.Format(_localizationService.GetString("Msg_AppliedCount"), candidates.Count), ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle24), TimeSpan.FromSeconds(2));
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
             System.Windows.MessageBox.Show(_localizationService.GetString("Msg_NotFound"));
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
                 System.Windows.MessageBox.Show(_localizationService.GetString("Msg_NoOccurrences"));
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
             
             System.Windows.MessageBox.Show(string.Format(_localizationService.GetString("Msg_ReplacedCount"), count));
         }
         finally
         {
             IsBusy = false;
         }
    }
}


