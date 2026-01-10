using Microsoft.EntityFrameworkCore;
using MangaWhisper.Domain.Entities;
using MangaWhisper.Domain.Repositories;
using MangaWhisper.Infrastructure.Data;

namespace MangaWhisper.Infrastructure.Repositories;

public class ChapterRepository : IChapterRepository
{
    private readonly ApplicationDbContext _context;

    public ChapterRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Chapter?> GetByMangaAndNumberAsync(int mangaId, decimal number)
    {
        return await _context.Chapters
            .FirstOrDefaultAsync(c => c.MangaId == mangaId && c.Number == number);
    }

    public async Task<IEnumerable<Chapter>> GetAllAsync()
    {
        return await _context.Chapters
            .OrderByDescending(c => c.Number)
            .ToListAsync();
    }

    public async Task<IEnumerable<Chapter>> GetPaginatedAsync(int page, int pageSize)
    {
        return await _context.Chapters
            .OrderByDescending(c => c.Number)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task AddAsync(Chapter chapter)
    {
        await _context.Chapters.AddAsync(chapter);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<int> GetTotalCountAsync()
    {
        return await _context.Chapters.CountAsync();
    }
}
