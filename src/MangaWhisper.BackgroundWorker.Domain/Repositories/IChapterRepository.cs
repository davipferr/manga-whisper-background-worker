using MangaWhisper.Domain.Entities;

namespace MangaWhisper.Domain.Repositories;

public interface IChapterRepository
{
    Task<Chapter?> GetByMangaAndNumberAsync(int mangaId, decimal number);
    Task<IEnumerable<Chapter>> GetAllAsync();
    Task AddAsync(Chapter chapter);
    Task SaveChangesAsync();
    Task<IEnumerable<Chapter>> GetPaginatedAsync(int page, int pageSize);
    Task<int> GetTotalCountAsync();
}
