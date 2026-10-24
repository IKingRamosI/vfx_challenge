namespace FxRates.Application.Dtos;

/// <summary>Body of POST /api/rates. The domain checks the rules.</summary>
public sealed record CreateRateRequest(
    string BaseCurrency, 
    string QuoteCurrency, 
    decimal Bid, 
    decimal Ask
    );
