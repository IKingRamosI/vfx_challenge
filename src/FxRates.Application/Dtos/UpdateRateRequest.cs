namespace FxRates.Application.Dtos;

/// <summary>Body of PUT /api/rates/{id}. Replaces both prices.</summary>
public sealed record UpdateRateRequest(
    decimal Bid, 
    decimal Ask
    );
