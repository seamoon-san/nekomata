using System.Collections.Generic;

namespace Nekomata.Models;

public class GameData
{
    public Dictionary<string, string> Scripts { get; set; } = new();
    public List<TranslationUnit> TextData { get; set; } = new();
}
