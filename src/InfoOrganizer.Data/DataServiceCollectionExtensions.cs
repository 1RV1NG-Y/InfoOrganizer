using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfoOrganizer.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>Registers the SQLite-backed <see cref="AppDbContext"/> as a pooled factory.
    /// Blazor Server components should resolve <c>IDbContextFactory&lt;AppDbContext&gt;</c> and create
    /// a short-lived context per operation rather than sharing one scoped instance.</summary>
    public static IServiceCollection AddInfoOrganizerData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(connectionString));
        return services;
    }
}
