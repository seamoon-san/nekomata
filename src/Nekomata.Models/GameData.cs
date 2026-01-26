using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Nekomata.Models;

public class GameData
{
    public Dictionary<string, string> Scripts { get; set; } = new();
    public ObservableCollection<TranslationUnit> TextData { get; set; } = new();
}
