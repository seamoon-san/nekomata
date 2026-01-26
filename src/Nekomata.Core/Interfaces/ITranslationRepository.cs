using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nekomata.Models;

namespace Nekomata.Core.Interfaces;

public interface ITranslationRepository
{
    Task<TranslationProject> AddProjectAsync(TranslationProject project);
    Task<TranslationProject?> GetProjectAsync(Guid id);
    Task<List<TranslationProject>> GetAllProjectsAsync();
    Task UpdateProjectAsync(TranslationProject project);
    Task DeleteProjectAsync(Guid id);
    Task UpdateTranslationUnitAsync(TranslationUnit unit);
}
