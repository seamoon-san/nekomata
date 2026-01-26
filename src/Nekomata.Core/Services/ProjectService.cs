using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nekomata.Core.Interfaces;
using Nekomata.Models;

namespace Nekomata.Core.Services;

public class ProjectService : IProjectService
{
    private readonly ITranslationRepository _repository;
    private readonly IEnumerable<IGameEngineAdapter> _adapters;

    public ProjectService(ITranslationRepository repository, IEnumerable<IGameEngineAdapter> adapters)
    {
        _repository = repository;
        _adapters = adapters;
    }

    public async Task<TranslationProject> CreateProjectAsync(string gamePath, string engineType)
    {
        // Find the matching adapter
        IGameEngineAdapter? adapter = null;

        // 1. Try auto-detection via CanHandle
        foreach (var a in _adapters)
        {
            if (a.CanHandle(gamePath))
            {
                adapter = a;
                break;
            }
        }

        // 2. Fallback to name matching if provided and detection failed
        if (adapter == null && !string.IsNullOrEmpty(engineType))
        {
            adapter = _adapters.FirstOrDefault(a => a.EngineName.Contains(engineType, StringComparison.OrdinalIgnoreCase));
        }

        // 3. Fallback to first available (Legacy)
        if (adapter == null)
        {
             adapter = _adapters.FirstOrDefault(); 
        }

        if (adapter == null)
        {
            throw new InvalidOperationException($"No adapter found for engine type: {engineType} or path: {gamePath}");
        }

        var gameData = await adapter.LoadGameDataAsync(gamePath);

        var project = new TranslationProject
        {
            Id = Guid.NewGuid(),
            Name = System.IO.Path.GetFileName(gamePath), // Use folder name or exe name as default
            GamePath = gamePath,
            EngineType = adapter.EngineName,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            TranslationUnits = gameData.TextData
        };
        
        // Ensure units have ProjectId
        foreach (var unit in project.TranslationUnits)
        {
            unit.ProjectId = project.Id;
        }

        // Return the project directly without saving to DB
        return project;
    }

    public async Task<TranslationProject?> GetProjectAsync(Guid id)
    {
        return await _repository.GetProjectAsync(id);
    }

    public async Task<List<TranslationProject>> GetAllProjectsAsync()
    {
        return await _repository.GetAllProjectsAsync();
    }

    public async Task SaveProjectAsync(TranslationProject project)
    {
        project.UpdatedAt = DateTime.Now;
        await _repository.UpdateProjectAsync(project);
    }

    public async Task DeleteProjectAsync(Guid id)
    {
        await _repository.DeleteProjectAsync(id);
    }

    public async Task<TranslationProject> ImportProjectAsync(TranslationProject project)
    {
        // Check if project exists
        var existing = await _repository.GetProjectAsync(project.Id);
        if (existing != null)
        {
            // If exists, delete it first to ensure clean overwrite (especially for child collections)
            await _repository.DeleteProjectAsync(project.Id);
        }
        
        // Ensure CreatedAt/UpdatedAt are set if missing (though they should be in file)
        if (project.CreatedAt == default) project.CreatedAt = DateTime.Now;
        project.UpdatedAt = DateTime.Now;

        return await _repository.AddProjectAsync(project);
    }

    public async Task UpdateTranslationUnitAsync(TranslationUnit unit)
    {
        unit.TranslatedAt = DateTime.Now;
        await _repository.UpdateTranslationUnitAsync(unit);
    }

    public async Task ApplyTranslationAsync(TranslationProject project, string outputPath)
    {
        var adapter = _adapters.FirstOrDefault(a => a.EngineName == project.EngineType)
                      ?? _adapters.FirstOrDefault();

        if (adapter == null)
        {
            throw new InvalidOperationException($"No adapter found for engine type: {project.EngineType}");
        }

        var gameData = new GameData
        {
            TextData = project.TranslationUnits
        };

        await adapter.ApplyTranslationAsync(gameData, project.GamePath, outputPath);
    }
}
