using FxRates.Application.Interfaces;
using FxRates.Infrastructure.Persistence;
using FxRates.IntegrationTests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace FxRates.IntegrationTests;

/// <summary>Hosts the real API in memory with an isolated SQLite database and fake external systems.</summary>
public sealed class FxRatesApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public FakeRateProvider Provider { get; } = new();
    public FakeEventPublisher Publisher { get; } = new();

    // The in-memory database lives as long as the connection is open
    public FxRatesApiFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");

        builder.ConfigureTestServices(services =>
        {
            // Swap SQL Server for SQLite. From EF Core 9, both registrations must be removed.
            services.RemoveAll<DbContextOptions<FxRatesDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<FxRatesDbContext>>();
            services.AddDbContext<FxRatesDbContext>(o => o.UseSqlite(_connection));

            // Replace only the external systems: AlphaVantage and RabbitMQ
            services.RemoveAll<IExchangeRateProvider>();
            services.AddSingleton<IExchangeRateProvider>(Provider);
            services.RemoveAll<IEventPublisher>();
            services.AddSingleton<IEventPublisher>(Publisher);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<FxRatesDbContext>().Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _connection.Dispose();
    }
}
