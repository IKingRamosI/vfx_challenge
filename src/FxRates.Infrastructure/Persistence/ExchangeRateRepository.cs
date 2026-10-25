using FxRates.Application.Interfaces;
using FxRates.Domain;
using Microsoft.EntityFrameworkCore;

namespace FxRates.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IExchangeRateRepository"/>.</summary>
public sealed class ExchangeRateRepository(FxRatesDbContext dbContext) : IExchangeRateRepository
{
    // EF Core's AddAsync returns a ValueTask, so it is awaited to match the Task in the interface.
    public async Task AddAsync(ExchangeRate rate, CancellationToken ct) => await dbContext.ExchangeRates.AddAsync(rate, ct);

    public Task<bool> ExistsAsync(string baseCurrency, string quoteCurrency, CancellationToken ct) =>
        dbContext.ExchangeRates.AnyAsync(
            r => r.BaseCurrency == baseCurrency && r.QuoteCurrency == quoteCurrency, ct);

    // Awaited because Task<List<T>> cannot be returned as Task<IReadOnlyList<T>>.
    public async Task<IReadOnlyList<ExchangeRate>> GetAllAsync(CancellationToken ct) =>
        await dbContext.ExchangeRates
            .AsNoTracking() // read-only listing: no change tracking overhead
            .OrderBy(r => r.BaseCurrency).ThenBy(r => r.QuoteCurrency)
            .ToListAsync(ct);

    // Tracked on purpose: UpdateAsync and DeleteAsync change this entity and then call SaveChangesAsync.
    public Task<ExchangeRate?> GetByIdAsync(int id, CancellationToken ct) =>
        dbContext.ExchangeRates.FirstOrDefaultAsync(r => r.Id == id, ct);

    // Codes arrive already normalized (upper case) by the service; the unique index makes this a single-row lookup.
    public Task<ExchangeRate?> GetByPairAsync(string baseCurrency, string quoteCurrency, CancellationToken ct) =>
        dbContext.ExchangeRates.FirstOrDefaultAsync(r => r.BaseCurrency == baseCurrency && r.QuoteCurrency == quoteCurrency, ct);

    // Marks the entity as deleted; the DELETE is sent by SaveChangesAsync.
    public void Remove(ExchangeRate rate) => dbContext.ExchangeRates.Remove(rate);

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
