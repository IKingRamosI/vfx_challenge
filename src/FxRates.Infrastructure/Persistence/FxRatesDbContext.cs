using FxRates.Domain;
using Microsoft.EntityFrameworkCore;

namespace FxRates.Infrastructure.Persistence;

/// <summary>EF Core context: the ExchangeRates table, its column types and the unique index per pair.</summary>
public sealed class FxRatesDbContext(DbContextOptions<FxRatesDbContext> options) : DbContext(options)
{
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExchangeRate>(e =>
        {
            e.ToTable("ExchangeRates");
            e.HasKey(x => x.Id);
            e.Property(x => x.BaseCurrency).HasMaxLength(3).IsFixedLength().IsRequired();
            e.Property(x => x.QuoteCurrency).HasMaxLength(3).IsFixedLength().IsRequired();
            e.Property(x => x.Bid).HasPrecision(18, 6);
            e.Property(x => x.Ask).HasPrecision(18, 6);
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
            // One row per pair: the database protects the rule too
            e.HasIndex(x => new { x.BaseCurrency, x.QuoteCurrency }).IsUnique();
        });
    }
}