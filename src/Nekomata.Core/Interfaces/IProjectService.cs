using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nekomata.Models;

namespace Nekomata.Core.Interfaces;

public interface IProjectService
{
    Task<TranslationProject> CreateProjectAsync(string gamePath, string engineType);
    Task<TranslationProject?> GetProjectAsync(Guid id);
    Task<List<TranslationProject>> GetAllProjectsAsync();
    Task SaveProjectAsync(TranslationProject project);
    Task DeleteProjectAsync(Guid id);
    Task<TranslationProject> ImportProjectAsync(TranslationProject project);
    Task UpdateTranslationUnitAsync(TranslationUnit unit);
    Task ApplyTranslationAsync(TranslationProject project, string outputPath);
}
