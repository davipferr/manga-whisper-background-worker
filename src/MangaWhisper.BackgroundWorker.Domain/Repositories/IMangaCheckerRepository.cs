using MangaWhisper.Common.Enums;
using MangaWhisper.Domain.Entities;

namespace MangaWhisper.Domain.Repositories;

public interface IMangaCheckerRepository
{
    Task<IEnumerable<MangaChecker>> GetActiveCheckersAsync();
    Task<IEnumerable<MangaChecker>> GetAllCheckersAsync();
    Task<MangaChecker?> GetByIdAsync(int id);
    Task AddAsync(MangaChecker checker);
    Task UpdateStatusAsync(int checkerId, MangaCheckerStatus status);
    Task SaveChangesAsync();
    Task<IEnumerable<MangaChecker>> GetByMangaTitleAsync(string mangaTitle);
}
