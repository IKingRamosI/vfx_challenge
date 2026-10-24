namespace FxRates.Application.Events;

/// <summary>Raised every time a new rate is added to the database.</summary>
public sealed record RateCreatedEvent(
    int RateId, 
    string BaseCurrency, 
    string QuoteCurrency, 
    decimal Bid, 
    decimal Ask, 
    string Source, 
    DateTime OccurredAtUtc
    );
