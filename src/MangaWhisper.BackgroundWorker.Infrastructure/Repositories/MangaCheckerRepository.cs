using Microsoft.EntityFrameworkCore;
using MangaWhisper.Common.Enums;
using MangaWhisper.Domain.Entities;
using MangaWhisper.Domain.Repositories;
using MangaWhisper.Infrastructure.Data;

namespace MangaWhisper.Infrastructure.Repositories;

public class MangaCheckerRepository : IMangaCheckerRepository
{
    private readonly ApplicationDbContext _context;

    public MangaCheckerRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<MangaChecker>> GetActiveCheckersAsync()
    {
        return await _context.MangaCheckers
            .Include(mc => mc.Manga)
            .Where(mc => mc.IsActive)
            .ToListAsync();
    }

    public async Task<IEnumerable<MangaChecker>> GetAllCheckersAsync()
    {
        return await _context.MangaCheckers
            .Include(mc => mc.Manga)
            .ToListAsync();
    }

    public async Task<MangaChecker?> GetByIdAsync(int id)
    {
        return await _context.MangaCheckers
            .Include(mc => mc.Manga)
            .FirstOrDefaultAsync(mc => mc.Id == id);
    }

    public async Task AddAsync(MangaChecker checker)
    {
        await _context.MangaCheckers.AddAsync(checker);
    }

    public async Task UpdateStatusAsync(int checkerId, MangaCheckerStatus status)
    {
        var checker = await _context.MangaCheckers.FindAsync(checkerId);
        if (checker != null)
        {
            checker.CheckerStatus = status;
            checker.LastCheckedAt = DateTime.UtcNow;
        }
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<MangaChecker>> GetByMangaTitleAsync(string mangaTitle)
    {
        return await _context.MangaCheckers
            .Include(mc => mc.Manga)
            .Where(mc => mc.Manga.Title.Contains(mangaTitle))
            .ToListAsync();
    }
}
