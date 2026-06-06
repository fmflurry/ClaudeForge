using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using ClaudeForge.Infrastructure.Persistence;

namespace ClaudeForge.Tests.Integration.Fixtures;

/// <summary>
/// Shared xUnit test fixture that spins up a Postgres 16 container once per test collection,
/// runs EnsureCreated (or Migrate) to apply the full schema, and tears down afterwards.
///
/// If Docker is not reachable the fixture constructor will throw, which causes xUnit to skip the
/// collection with a clear error rather than running tests against a missing database.
/// </summary>
[CollectionDefinition(PostgresFixture.CollectionName)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture> { }

public sealed class PostgresFixture : IAsyncLifetime
{
    public const string CollectionName = "Postgres";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("marketplace_test")
        .WithUsername("test")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public MarketplaceDbContext CreateContext()
    {
        DbContextOptions<MarketplaceDbContext> options = new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new MarketplaceDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using MarketplaceDbContext ctx = CreateContext();
        // Apply all migrations (or create schema from model if no migrations exist yet).
        // For integration tests we prefer EnsureCreated which reflects the EF model directly.
        await ctx.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
