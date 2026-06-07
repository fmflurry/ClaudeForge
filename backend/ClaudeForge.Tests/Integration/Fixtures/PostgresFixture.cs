using ClaudeForge.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

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

    // max_connections raised to 300 to support the 100-concurrent-call download-counter test
    // while other tests are running in parallel against the same container.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("marketplace_test")
        .WithUsername("test")
        .WithPassword("test")
        .WithCommand("-c", "max_connections=300")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    // Shared data source — created once after the container starts, disposed on teardown.
    // EnableRecordsAsTuples is required by Npgsql 9.x for EF Core projections to ValueTuple
    // (e.g., ValueTuple.Create(p.Name, p.Slug)).  A single shared data source is correct
    // here because NpgsqlDataSource owns the connection pool; creating one per CreateContext()
    // call would exhaust the pool under parallel test execution.
    private NpgsqlDataSource? _dataSource;

    public string ConnectionString => _container.GetConnectionString();

    public MarketplaceDbContext CreateContext()
    {
        if (_dataSource is null)
            throw new InvalidOperationException("PostgresFixture has not been initialized. Ensure InitializeAsync has completed.");

        DbContextOptions<MarketplaceDbContext> options = new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseNpgsql(_dataSource)
            .Options;

        return new MarketplaceDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _dataSource = new NpgsqlDataSourceBuilder(ConnectionString)
            .EnableRecordsAsTuples()
            .Build();

        await using MarketplaceDbContext ctx = CreateContext();
        // Apply all migrations (or create schema from model if no migrations exist yet).
        // For integration tests we prefer EnsureCreated which reflects the EF model directly.
        await ctx.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
            await _dataSource.DisposeAsync();

        await _container.DisposeAsync();
    }
}
