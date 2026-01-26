using System;
using System.Threading.Tasks;
using Nekomata.Models;

namespace Nekomata.Core.Interfaces;

public interface IGameEngineAdapter
{
    string EngineName { get; }
    Version SupportedVersion { get; }
    bool CanHandle(string gamePath);
    Task<GameData> LoadGameDataAsync(string gamePath);
    Task<bool> ApplyTranslationAsync(GameData translatedData, string originalGamePath, string outputPath);
}
