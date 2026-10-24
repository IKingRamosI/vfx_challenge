namespace FxRates.Application.Dtos;

/// <summary>A rate returned by the external provider.</summary>
public sealed record ExternalRate(
    decimal Bid, 
    decimal Ask
    );