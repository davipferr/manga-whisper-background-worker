using System.Data;
using Npgsql;

namespace MangaWhisper.Infrastructure.Data;

public class QueryDbContext : IDisposable
{
    private readonly IDbConnection _connection;

    public QueryDbContext(string connectionString)
    {
        _connection = new NpgsqlConnection(connectionString);
    }

    public IDbConnection Connection => _connection;

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
