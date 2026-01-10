using Microsoft.EntityFrameworkCore;
using MangaWhisper.Domain.Entities;

namespace MangaWhisper.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Manga> Mangas { get; set; }
    public DbSet<MangaChecker> MangaCheckers { get; set; }
    public DbSet<Chapter> Chapters { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Note: Entity configurations should match the main project's database schema
        // If configurations exist, they should be copied from the main project
    }
}
