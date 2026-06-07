using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClaudeForge.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <see cref="MarketplaceDbContext"/>.
/// Used exclusively by <c>dotnet ef migrations add</c> and <c>dotnet ef database update</c>
/// when run from the CLI — not registered in the DI container.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed class MarketplaceDbContextFactory : IDesignTimeDbContextFactory<MarketplaceDbContext>
{
    public MarketplaceDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=plugin_marketplace;Username=postgres;Password=devpassword";

        DbContextOptions<MarketplaceDbContext> options = new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new MarketplaceDbContext(options);
    }
}
