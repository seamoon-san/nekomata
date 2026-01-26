using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nekomata.Core.Interfaces;
using Nekomata.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nekomata.EngineAdapters;

public class RPGMakerMVAdapter : IGameEngineAdapter
{
    public string EngineName => "RPG Maker MV/MZ";
    public Version SupportedVersion => new Version(1, 0);

    public bool CanHandle(string gamePath)
    {
        // Check for MV/MZ structure
        // Usually contains 'data' folder with JSON files, or 'www/data'
        var dataPath = GetDataPath(gamePath);
        return !string.IsNullOrEmpty(dataPath) && Directory.GetFiles(dataPath, "*.json").Length > 0;
    }

    public async Task<GameData> LoadGameDataAsync(string gamePath)
    {
        var gameData = new GameData();
        var dataPath = GetDataPath(gamePath);

        if (string.IsNullOrEmpty(dataPath))
        {
            throw new DirectoryNotFoundException("Could not find 'data' or 'www/data' directory.");
        }

        // Load System.json to get game title or basic info
        var systemJsonPath = Path.Combine(dataPath, "System.json");
        if (File.Exists(systemJsonPath))
        {
            var json = await File.ReadAllTextAsync(systemJsonPath);
            var jObject = JObject.Parse(json);
            if (jObject["gameTitle"] != null)
            {
                // We could store metadata here if GameData supported it, 
                // for now just verify it loads.
            }
        }

        // Process all JSON files in data directory
        var jsonFiles = Directory.GetFiles(dataPath, "*.json");
        foreach (var file in jsonFiles)
        {
            var fileName = Path.GetFileName(file);
            var json = await File.ReadAllTextAsync(file);
            
            // Basic parsing logic based on file type
            if (fileName.StartsWith("Map") && fileName != "MapInfos.json")
            {
                ExtractMapText(json, fileName, gameData);
            }
            else if (fileName == "CommonEvents.json")
            {
                ExtractCommonEventsText(json, fileName, gameData);
            }
            // Add more handlers for Items, Skills, etc.
        }

        return gameData;
    }

    public async Task<bool> ApplyTranslationAsync(GameData translatedData, string originalGamePath, string outputPath)
    {
        var dataPath = GetDataPath(originalGamePath);
        if (string.IsNullOrEmpty(dataPath)) return false;

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        // Prepare translation map: FileName -> OriginalText -> Translation
        var translationMap = new Dictionary<string, Dictionary<string, string>>();
        foreach (var unit in translatedData.TextData)
        {
            // Priority: Human > Machine > Original
            string trans = !string.IsNullOrWhiteSpace(unit.HumanTranslation) ? unit.HumanTranslation :
                           !string.IsNullOrWhiteSpace(unit.MachineTranslation) ? unit.MachineTranslation : 
                           unit.OriginalText;
            
            if (trans == unit.OriginalText) continue;

            // Context format: "Map001.json (Show Text)"
            var fileName = unit.Context.Split(' ')[0];
            if (!translationMap.ContainsKey(fileName))
            {
                translationMap[fileName] = new Dictionary<string, string>();
            }
            
            // Note: If multiple identical original texts exist in the same file but have different translations,
            // this simple map will only use the last one processed. 
            // For a robust solution, we need unique identifiers (e.g. JSON path) stored in TranslationUnit.
            if (!string.IsNullOrEmpty(unit.OriginalText))
            {
                translationMap[fileName][unit.OriginalText] = trans;
            }
        }

        var jsonFiles = Directory.GetFiles(dataPath, "*.json");
        foreach (var file in jsonFiles)
        {
            var fileName = Path.GetFileName(file);
            var outputFilePath = Path.Combine(outputPath, fileName);

            if (translationMap.ContainsKey(fileName))
            {
                var json = await File.ReadAllTextAsync(file);
                string newJson = json;
                
                try 
                {
                    if (fileName.StartsWith("Map") && fileName != "MapInfos.json")
                    {
                        newJson = ApplyMapText(json, translationMap[fileName]);
                    }
                    else if (fileName == "CommonEvents.json")
                    {
                        newJson = ApplyCommonEventsText(json, translationMap[fileName]);
                    }
                }
                catch (Exception)
                {
                    // Fallback to original on error
                    newJson = json; 
                }

                await File.WriteAllTextAsync(outputFilePath, newJson);
            }
            else
            {
                File.Copy(file, outputFilePath, true);
            }
        }
        
        return true;
    }

    private string ApplyMapText(string json, Dictionary<string, string> translations)
    {
        var jObject = JObject.Parse(json);
        var events = jObject["events"] as JArray;
        if (events == null) return json;

        foreach (var evt in events)
        {
            if (evt == null || evt.Type == JTokenType.Null) continue;
            
            var pages = evt["pages"] as JArray;
            if (pages == null) continue;

            foreach (var page in pages)
            {
                var list = page["list"] as JArray;
                if (list == null) continue;

                ApplyEventCommands(list, translations);
            }
        }
        return jObject.ToString(Formatting.Indented);
    }

    private string ApplyCommonEventsText(string json, Dictionary<string, string> translations)
    {
        var jArray = JArray.Parse(json);
        foreach (var evt in jArray)
        {
            if (evt == null || evt.Type == JTokenType.Null) continue;

            var list = evt["list"] as JArray;
            if (list == null) continue;

            ApplyEventCommands(list, translations);
        }
        return jArray.ToString(Formatting.Indented);
    }

    private void ApplyEventCommands(JArray list, Dictionary<string, string> translations)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var cmd = list[i];
            var code = (int?)cmd["code"];
            var parameters = cmd["parameters"] as JArray;

            if (code == 401) // Show Text
            {
                var buffer = new List<string>();
                int j = i;
                while (j < list.Count)
                {
                    var nextCmd = list[j];
                    if ((int?)nextCmd["code"] == 401)
                    {
                        var p = nextCmd["parameters"] as JArray;
                        var t = (p != null && p.Count > 0) ? (string?)p[0] : "";
                        buffer.Add(t ?? "");
                        j++;
                    }
                    else
                    {
                        break;
                    }
                }

                var originalText = string.Join("\n", buffer);
                if (!string.IsNullOrWhiteSpace(originalText) && translations.TryGetValue(originalText, out var trans))
                {
                    var transLines = trans.Replace("\r\n", "\n").Split('\n');
                    int originalCount = buffer.Count;
                    int newCount = transLines.Length;

                    // Update existing commands
                    int commonCount = Math.Min(originalCount, newCount);
                    for (int k = 0; k < commonCount; k++)
                    {
                        var p = list[i + k]["parameters"] as JArray;
                        if (p != null && p.Count > 0) p[0] = transLines[k];
                    }

                    if (newCount > originalCount)
                    {
                        // Insert new commands
                        var templateCmd = list[i + originalCount - 1];
                        for (int k = originalCount; k < newCount; k++)
                        {
                            var newCmd = (JObject)templateCmd.DeepClone();
                            var p = newCmd["parameters"] as JArray;
                            if (p != null && p.Count > 0) p[0] = transLines[k];
                            list.Insert(i + k, newCmd);
                        }
                    }
                    else if (newCount < originalCount)
                    {
                        // Remove excess commands
                        for (int k = 0; k < originalCount - newCount; k++)
                        {
                            list.RemoveAt(i + newCount);
                        }
                    }

                    // Update index to skip processed commands
                    i += newCount - 1;
                }
                else
                {
                    // Skip the block
                    i = j - 1;
                }
            }
            else if (code == 102 && parameters != null && parameters.Count > 0) // Show Choices
            {
                var choices = parameters[0] as JArray;
                if (choices != null)
                {
                    for (int k = 0; k < choices.Count; k++)
                    {
                        var text = (string?)choices[k];
                        if (!string.IsNullOrWhiteSpace(text) && translations.TryGetValue(text, out var trans))
                        {
                            choices[k] = trans;
                        }
                    }
                }
            }
        }
    }

    private string? GetDataPath(string rootPath)
    {
        // MV usually has www/data, MZ has data directly
        var mvData = Path.Combine(rootPath, "www", "data");
        if (Directory.Exists(mvData)) return mvData;

        var mzData = Path.Combine(rootPath, "data");
        if (Directory.Exists(mzData)) return mzData;
        
        // If rootPath itself is the data folder
        if (Path.GetFileName(rootPath) == "data" && Directory.Exists(rootPath)) return rootPath;

        return null;
    }

    private void ExtractMapText(string json, string fileName, GameData gameData)
    {
        var jObject = JObject.Parse(json);
        var events = jObject["events"] as JArray;
        if (events == null) return;

        foreach (var evt in events)
        {
            if (evt == null || evt.Type == JTokenType.Null) continue;
            
            var pages = evt["pages"] as JArray;
            if (pages == null) continue;

            foreach (var page in pages)
            {
                var list = page["list"] as JArray;
                if (list == null) continue;

                ExtractEventCommands(list, fileName, gameData);
            }
        }
    }

    private void ExtractCommonEventsText(string json, string fileName, GameData gameData)
    {
        var jArray = JArray.Parse(json);
        foreach (var evt in jArray)
        {
            if (evt == null || evt.Type == JTokenType.Null) continue;

            var list = evt["list"] as JArray;
            if (list == null) continue;

            ExtractEventCommands(list, fileName, gameData);
        }
    }

    private void ExtractEventCommands(JArray list, string fileName, GameData gameData)
    {
        var textBuffer = new List<string>();

        foreach (var cmd in list)
        {
            var code = (int?)cmd["code"];
            var parameters = cmd["parameters"] as JArray;

            if (code == 401 && parameters != null && parameters.Count > 0) // Show Text
            {
                var text = (string?)parameters[0];
                textBuffer.Add(text ?? string.Empty);
            }
            else
            {
                if (textBuffer.Count > 0)
                {
                    var combinedText = string.Join("\n", textBuffer);
                    if (!string.IsNullOrWhiteSpace(combinedText))
                    {
                        AddTranslationUnit(gameData, combinedText, fileName, "Show Text");
                    }
                    textBuffer.Clear();
                }

                if (code == 102 && parameters != null && parameters.Count > 0) // Show Choices
                {
                    var choices = parameters[0] as JArray;
                    if (choices != null)
                    {
                        foreach (var choice in choices)
                        {
                            var text = (string?)choice;
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                AddTranslationUnit(gameData, text, fileName, "Choice");
                            }
                        }
                    }
                }
            }
        }

        // Flush remaining buffer
        if (textBuffer.Count > 0)
        {
            var combinedText = string.Join("\n", textBuffer);
            if (!string.IsNullOrWhiteSpace(combinedText))
            {
                AddTranslationUnit(gameData, combinedText, fileName, "Show Text");
            }
        }
    }

    private void AddTranslationUnit(GameData gameData, string text, string context, string type)
    {
        // Avoid duplicates if needed, or allow them if context differs
        // For MVP, we simply add all
        gameData.TextData.Add(new TranslationUnit
        {
            Id = Guid.NewGuid(),
            OriginalText = text,
            Context = $"{context} ({type})",
            TranslatedAt = DateTime.MinValue
        });
    }
}
