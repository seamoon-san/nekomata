using System;
using CommunityToolkit.Mvvm.ComponentModel;

public partial class TranslationUnit : ObservableObject
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private Guid _projectId;

    [ObservableProperty]
    private string _originalText = string.Empty;

    [ObservableProperty]
    private string _machineTranslation = string.Empty;

    [ObservableProperty]
    private string _humanTranslation = string.Empty;

    [ObservableProperty]
    private string _context = string.Empty;

    [ObservableProperty]
    private DateTime _translatedAt;
}
