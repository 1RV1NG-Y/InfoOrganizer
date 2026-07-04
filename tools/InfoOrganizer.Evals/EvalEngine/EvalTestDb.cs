using InfoOrganizer.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfoOrganizer.Evals.EvalEngine;

public sealed class EvalTestDb : IDbContextFactory<AppDbContext>, IDisposable
{
    private readonly string _path;
    private readonly DbContextOptions<AppDbContext> _options;

    public EvalTestDb()
    {
        _path = Path.Combine(Path.GetTempPath(), $"dm-eval-{Guid.NewGuid():N}.db");
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
        try { File.Delete(_path); } catch { /* temp file cleanup */ }
    }
}
