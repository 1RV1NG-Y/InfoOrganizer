using InfoOrganizer.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfoOrganizer.Tests.Support;

/// <summary>A throwaway migrated SQLite database in a temp file, exposed as the same
/// <see cref="IDbContextFactory{AppDbContext}"/> the app uses.</summary>
public sealed class TestDb : IDbContextFactory<AppDbContext>, IDisposable
{
    private readonly string _path;
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDb()
    {
        _path = Path.Combine(Path.GetTempPath(), $"dm-test-{Guid.NewGuid():N}.db");
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options;

        using var db = CreateDbContext();
        db.Database.Migrate();
    }

    public AppDbContext CreateDbContext() => new(_options);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_path); } catch { /* temp file */ }
    }
}
