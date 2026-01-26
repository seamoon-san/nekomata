using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nekomata.Core.Interfaces;
using Nekomata.Infrastructure.Data;
using Nekomata.Models;

namespace Nekomata.Infrastructure.Repositories;

public class TranslationRepository : ITranslationRepository
{
    private readonly AppDbContext _context;

    public TranslationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TranslationProject> AddProjectAsync(TranslationProject project)
    {
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();
        return project;
    }

    public async Task<TranslationProject?> GetProjectAsync(Guid id)
    {
        return await _context.Projects
            .Include(p => p.TranslationUnits)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<TranslationProject>> GetAllProjectsAsync()
    {
        return await _context.Projects.ToListAsync();
    }

    public async Task UpdateProjectAsync(TranslationProject project)
    {
        _context.Projects.Update(project);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteProjectAsync(Guid id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project != null)
        {
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateTranslationUnitAsync(TranslationUnit unit)
    {
        _context.TranslationUnits.Update(unit);
        await _context.SaveChangesAsync();
    }
}
