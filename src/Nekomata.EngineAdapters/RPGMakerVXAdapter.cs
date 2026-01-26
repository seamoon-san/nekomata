using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nekomata.Core.Exceptions;
using Nekomata.Core.Interfaces;
using Nekomata.EngineAdapters.Ruby;
using Nekomata.Models;

namespace Nekomata.EngineAdapters;

public class RPGMakerVXAdapter : IGameEngineAdapter
{
    public string EngineName => "RPG Maker VX/VX Ace";
    public Version SupportedVersion => new Version(1, 0);

    public bool CanHandle(string gamePath)
    {
        // Check for VX (RGSS2) or VX Ace (RGSS3)
        // Look for Game.rgss3a, Game.rgss2a, or Data/*.rvdata2, Data/*.rvdata
        
        if (Directory.Exists(gamePath))
        {
             if (File.Exists(Path.Combine(gamePath, "Game.rgss3a")) || 
                 File.Exists(Path.Combine(gamePath, "Game.rgss2a")))
             {
                 return true;
             }
             
             var dataPath = Path.Combine(gamePath, "Data");
             if (Directory.Exists(dataPath))
             {
                 if (Directory.GetFiles(dataPath, "*.rvdata2").Length > 0 || 
                     Directory.GetFiles(dataPath, "*.rvdata").Length > 0)
                 {
                     return true;
                 }
             }
        }
        else if (File.Exists(gamePath) && (gamePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
        {
            var dir = Path.GetDirectoryName(gamePath);
            if (dir != null) return CanHandle(dir);
        }

        return false;
    }

    public async Task<GameData> LoadGameDataAsync(string gamePath)
    {
        return await Task.Run(() =>
        {
            var rootDir = Directory.Exists(gamePath) ? gamePath : Path.GetDirectoryName(gamePath);
            if (rootDir == null) throw new DirectoryNotFoundException("Game path not valid");

            var nekomataDir = Path.Combine(rootDir, ".nekomata");
            var dataDir = Path.Combine(nekomataDir, "Data");

            // Check if data is already extracted in .nekomata
            bool isExtracted = Directory.Exists(dataDir) && 
                               (Directory.GetFiles(dataDir, "*.rvdata2").Length > 0 || 
                                Directory.GetFiles(dataDir, "*.rvdata").Length > 0);

            if (!isExtracted)
            {
                // Create .nekomata folder
                if (!Directory.Exists(nekomataDir))
                {
                    Directory.CreateDirectory(nekomataDir);
                }
                
                string archiveName = "Game.rgss3a";
                if (File.Exists(Path.Combine(rootDir, "Game.rgss2a"))) archiveName = "Game.rgss2a";

                throw new GameExtractionRequiredException(
                    $"Detected {EngineName}.\nPlease extract the contents of '{archiveName}' into the folder:\n{nekomataDir}\n\nThen try importing again.",
                    nekomataDir);
            }

            var gameData = new GameData();
            var files = Directory.GetFiles(dataDir, "*.rvdata2").Concat(Directory.GetFiles(dataDir, "*.rvdata"));

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                try 
                {
                    using (var stream = File.OpenRead(file))
                    {
                        var reader = new MarshalReader(stream);
                        var root = reader.Read();
                        ExtractText(root, fileName, gameData);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing {fileName}: {ex.Message}");
                }
            }
            
            return gameData;
        });
    }

    private void ExtractText(RubyValue root, string context, GameData gameData)
    {
        var visited = new HashSet<RubyValue>();
        Traverse(root, visited, (node) =>
        {
            if (node is RubyObject obj)
            {
                var className = obj.ClassName.Name;
                
                if (className == "RPG::EventCommand")
                {
                    ExtractEventCommand(obj, context, gameData);
                }
                else if (className == "RPG::Map")
                {
                    if (obj.GetVar("@display_name") is RubyString displayName)
                    {
                        AddUnit(gameData, displayName.Decode(), context, "Map Name");
                    }
                }
                else if (className == "RPG::System")
                {
                    // Terms extraction could be added here
                }
                else if (IsBaseItem(className))
                {
                     ExtractBaseItem(obj, context, gameData);
                }
            }
        });
    }

    private bool IsBaseItem(string className)
    {
        return className == "RPG::Skill" || className == "RPG::Item" || 
               className == "RPG::Weapon" || className == "RPG::Armor" ||
               className == "RPG::Enemy" || className == "RPG::State" || 
               className == "RPG::Actor" || className == "RPG::Class";
    }

    private RubyString? GetRubyString(RubyValue? val)
    {
        if (val == null) return null;
        if (val is RubyString s) return s;
        if (val is RubyIVar iv && iv.Value is RubyString s2) return s2;
        return null;
    }

    private void ExtractEventCommand(RubyObject obj, string context, GameData gameData)
    {
        var codeVal = obj.GetVar("@code") as RubyFixnum;
        var paramsVal = obj.GetVar("@parameters") as RubyArray;
        
        if (codeVal == null || paramsVal == null) return;
        
        int code = codeVal.Value;
        
        if (code == 401) // Show Text
        {
            if (paramsVal.Elements.Count > 0)
            {
                var str = GetRubyString(paramsVal.Elements[0]);
                if (str != null)
                {
                    AddUnit(gameData, str.Decode(), context, "Show Text");
                }
            }
        }
        else if (code == 102) // Show Choices
        {
            if (paramsVal.Elements.Count > 0 && paramsVal.Elements[0] is RubyArray choices)
            {
                foreach (var c in choices.Elements)
                {
                    var s = GetRubyString(c);
                    if (s != null) AddUnit(gameData, s.Decode(), context, "Choice");
                }
            }
        }
    }

    private void ExtractBaseItem(RubyObject obj, string context, GameData gameData)
    {
        if (GetRubyString(obj.GetVar("@name")) is RubyString name)
        {
            AddUnit(gameData, name.Decode(), context, "Name");
        }
        if (GetRubyString(obj.GetVar("@description")) is RubyString desc)
        {
            AddUnit(gameData, desc.Decode(), context, "Description");
        }
    }

    private void AddUnit(GameData gameData, string text, string context, string type)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        gameData.TextData.Add(new TranslationUnit
        {
            Id = Guid.NewGuid(),
            OriginalText = text,
            Context = $"{context} ({type})",
            TranslatedAt = DateTime.MinValue
        });
    }

    public async Task<bool> ApplyTranslationAsync(GameData translatedData, string originalGamePath, string outputPath)
    {
        return await Task.Run(() =>
        {
            var rootDir = Directory.Exists(originalGamePath) ? originalGamePath : Path.GetDirectoryName(originalGamePath);
            if (rootDir == null) return false;

            var nekomataDir = Path.Combine(rootDir, ".nekomata");
            var dataDir = Path.Combine(nekomataDir, "Data");
            
            if (!Directory.Exists(dataDir)) return false;

            // Prepare map
            var translationMap = new Dictionary<string, Dictionary<string, string>>();
            foreach (var unit in translatedData.TextData)
            {
                 string trans = !string.IsNullOrWhiteSpace(unit.HumanTranslation) ? unit.HumanTranslation : unit.OriginalText;
                 if (trans == unit.OriginalText) continue;
                 
                 var fileName = unit.Context.Split(' ')[0];
                 if (!translationMap.ContainsKey(fileName)) translationMap[fileName] = new Dictionary<string, string>();
                 translationMap[fileName][unit.OriginalText] = trans;
            }

            if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);
            
            var files = Directory.GetFiles(dataDir, "*.rvdata2").Concat(Directory.GetFiles(dataDir, "*.rvdata"));
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var destPath = Path.Combine(outputPath, fileName);
                
                try
                {
                    using (var stream = File.OpenRead(file))
                    {
                        var reader = new MarshalReader(stream);
                        var root = reader.Read();
                        
                        if (translationMap.ContainsKey(fileName))
                        {
                            ApplyText(root, translationMap[fileName]);
                        }
                        
                        using (var writeStream = File.Create(destPath))
                        {
                            var writer = new MarshalWriter(writeStream);
                            writer.Write(root);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error applying to {fileName}: {ex.Message}");
                    // Copy original if failed?
                    File.Copy(file, destPath, true);
                }
            }
            
            // RGSS3A Packing (Placeholder)
            // Check if user wanted RGSS3A
            // We can create a dummy file or just skip if we don't have the algorithm
            // For now, we only output .rvdata2 which is sufficient for game to run.
            
            return true;
        });
    }

    private void UpdateString(RubyValue? val, Dictionary<string, string> translations)
    {
        if (val == null) return;
        var s = GetRubyString(val);
        if (s == null) return;
        
        var text = s.Decode();
        if (translations.TryGetValue(text, out var trans))
        {
             s.Bytes = System.Text.Encoding.UTF8.GetBytes(trans);
        }
    }

    private void ApplyText(RubyValue root, Dictionary<string, string> translations)
    {
         var visited = new HashSet<RubyValue>();
         Traverse(root, visited, (node) => 
         {
             if (node is RubyObject obj)
             {
                 var className = obj.ClassName.Name;
                 if (className == "RPG::EventCommand")
                 {
                     var codeVal = obj.GetVar("@code") as RubyFixnum;
                     var paramsVal = obj.GetVar("@parameters") as RubyArray;
                     if (codeVal != null && paramsVal != null)
                     {
                         if (codeVal.Value == 401 && paramsVal.Elements.Count > 0)
                         {
                             UpdateString(paramsVal.Elements[0], translations);
                         }
                         else if (codeVal.Value == 102 && paramsVal.Elements.Count > 0 && paramsVal.Elements[0] is RubyArray choices)
                         {
                             foreach (var c in choices.Elements) UpdateString(c, translations);
                         }
                     }
                 }
                 else if (IsBaseItem(className))
                 {
                     UpdateString(obj.GetVar("@name"), translations);
                     UpdateString(obj.GetVar("@description"), translations);
                 }
                 else if (className == "RPG::Map")
                 {
                     UpdateString(obj.GetVar("@display_name"), translations);
                 }
             }
         });
    }

    private void Traverse(RubyValue node, HashSet<RubyValue> visited, Action<RubyValue> action)
    {
        if (node == null || visited.Contains(node)) return;
        visited.Add(node);

        action(node);

        if (node is RubyArray arr)
        {
            foreach (var e in arr.Elements) Traverse(e, visited, action);
        }
        else if (node is RubyHash hash)
        {
            foreach (var p in hash.Pairs)
            {
                Traverse(p.Key, visited, action);
                Traverse(p.Value, visited, action);
            }
        }
        else if (node is RubyObject obj)
        {
            foreach (var v in obj.Variables.Values) Traverse(v, visited, action);
        }
        else if (node is RubyStruct s)
        {
             foreach (var v in s.Members.Values) Traverse(v, visited, action);
        }
        else if (node is RubyIVar ivar)
        {
             Traverse(ivar.Value, visited, action);
             foreach (var v in ivar.Variables.Values) Traverse(v, visited, action);
        }
    }
}
