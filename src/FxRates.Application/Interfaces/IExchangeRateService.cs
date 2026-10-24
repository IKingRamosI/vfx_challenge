using FxRates.Application.Dtos;

namespace FxRates.Application.Interfaces;

/// <summary>Use cases for managing exchange rates.</summary>
public interface IExchangeRateService
{
    Task<IReadOnlyList<ExchangeRateResponse>> GetAllAsync(CancellationToken ct);
    Task<ExchangeRateResponse?> GetByIdAsync(int id, CancellationToken ct);
    Task<ExchangeRateResponse?> GetByPairAsync(string baseCurrency, string quoteCurrency, CancellationToken ct);
    Task<ExchangeRateResponse> CreateAsync(CreateRateRequest request, CancellationToken ct);
    Task<ExchangeRateResponse?> UpdateAsync(int id, UpdateRateRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}