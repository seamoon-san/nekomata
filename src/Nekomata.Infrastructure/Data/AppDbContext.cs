using Microsoft.EntityFrameworkCore;
using Nekomata.Models;

namespace Nekomata.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public DbSet<TranslationProject> Projects { get; set; } = null!;
    public DbSet<TranslationUnit> TranslationUnits { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=nekomata.db");
        }
    }
}
