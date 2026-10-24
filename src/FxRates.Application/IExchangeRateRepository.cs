using FxRates.Domain;

namespace FxRates.Application.Abstractions;

/// <summary>Persistence for exchange rates.</summary>
public interface IExchangeRateRepository
{
    Task<IReadOnlyList<ExchangeRate>> GetAllAsync(CancellationToken ct);
    Task<ExchangeRate?> GetByIdAsync(int id, CancellationToken ct);
    Task<ExchangeRate?> GetByPairAsync(string baseCurrency, string quoteCurrency, CancellationToken ct);
    Task<bool> ExistsAsync(string baseCurrency, string quoteCurrency, CancellationToken ct);
    Task AddAsync(ExchangeRate rate, CancellationToken ct);
    void Remove(ExchangeRate rate);
    Task SaveChangesAsync(CancellationToken ct);
}
