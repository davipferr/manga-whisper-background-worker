using MangaWhisper.Domain.Entities;

namespace MangaWhisper.Domain.Repositories;

public interface IMangaRepository
{
    Task<Manga?> GetByTitleAsync(string title);
    Task AddAsync(Manga manga);
    Task SaveChangesAsync();
}
