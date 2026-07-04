using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InfoOrganizer.Data;

/// <summary>Lets <c>dotnet ef migrations</c> build the context without the web host.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=infoorganizer-design.db")
            .Options;
        return new AppDbContext(options);
    }
}
