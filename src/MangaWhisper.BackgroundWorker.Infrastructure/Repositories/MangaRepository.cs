using Microsoft.EntityFrameworkCore;
using MangaWhisper.Domain.Entities;
using MangaWhisper.Domain.Repositories;
using MangaWhisper.Infrastructure.Data;

namespace MangaWhisper.Infrastructure.Repositories;

public class MangaRepository : IMangaRepository
{
    private readonly ApplicationDbContext _context;

    public MangaRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Manga?> GetByTitleAsync(string title)
    {
        return await _context.Mangas
            .FirstOrDefaultAsync(m => m.Title == title);
    }

    public async Task AddAsync(Manga manga)
    {
        await _context.Mangas.AddAsync(manga);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
