using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Nekomata.Models;

public class TranslationProject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string GamePath { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string EngineType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ObservableCollection<TranslationUnit> TranslationUnits { get; set; } = new();

    // UI Persistence
    public string LastFilterContext { get; set; } = "All";
    public string LastGroupOption { get; set; } = "None";
    public string LastSearchText { get; set; } = string.Empty;
    public Guid? LastSelectedUnitId { get; set; }
    public double LastScrollOffset { get; set; }
    public List<string> CollapsedGroups { get; set; } = new();
}
