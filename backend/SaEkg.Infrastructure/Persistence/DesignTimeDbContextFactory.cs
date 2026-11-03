using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SaEkg.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SaEkgDbContext>
{
    public SaEkgDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SaEkgDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=saekg;Username=saekg;Password=saekg")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new SaEkgDbContext(options);
    }
}
