using System;
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

    // UI State Preservation
    public string? LastFilter { get; set; }
    public string? LastGroup { get; set; }
    public double? LastScrollOffset { get; set; }

    public ObservableCollection<TranslationUnit> TranslationUnits { get; set; } = new();
}
