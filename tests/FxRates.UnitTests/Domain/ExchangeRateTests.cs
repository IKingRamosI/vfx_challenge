using FxRates.Domain;

namespace FxRates.UnitTests.Domain;

public class ExchangeRateTests
{
    [Fact]
    public void Create_WithValidData_NormalizesCodesAndSetsValues()
    {
        var rate = ExchangeRate.Create(" usd ", "eur", 0.92m, 0.93m, RateSource.Manual);

        Assert.Equal("USD", rate.BaseCurrency);
        Assert.Equal("EUR", rate.QuoteCurrency);
        Assert.Equal(0.92m, rate.Bid);
        Assert.Equal(0.93m, rate.Ask);
        Assert.Equal(RateSource.Manual, rate.Source);
    }

    [Fact]
    public void Create_WithBidGreaterThanAsk_Throws() =>
        Assert.Throws<DomainValidationException>(() =>
        ExchangeRate.Create("USD", "EUR", 1.2m, 1.1m, RateSource.Manual));

    [Fact]
    public void Create_WithBidEqualToAsk_IsAllowed()
    {
        var rate = ExchangeRate.Create("USD", "EUR", 0.93m, 0.93m, RateSource.Manual);

        Assert.Equal(rate.Bid, rate.Ask);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    public void Create_WithNonPositivePrice_Throws(decimal bid, decimal ask) =>
        Assert.Throws<DomainValidationException>(() =>
        ExchangeRate.Create("USD", "EUR", bid, ask, RateSource.Manual));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("12X")]
    [InlineData("EURO")]
    public void Create_WithInvalidCurrencyCode_Throws(string? code) =>
        Assert.Throws<DomainValidationException>(() =>
        ExchangeRate.Create(code!, "EUR", 1.1m, 1.2m, RateSource.Manual));

    [Fact]
    public void Create_WithSameBaseAndQuote_Throws() =>
        Assert.Throws<DomainValidationException>(() =>
        ExchangeRate.Create("EUR", "eur", 1m, 1m, RateSource.Manual));

    [Fact]
    public void UpdatePrices_SetsPricesAndSource()
    {
        var rate = ExchangeRate.Create("USD", "EUR", 0.92m, 0.93m, RateSource.AlphaVantage);

        rate.UpdatePrices(0.95m, 0.96m, RateSource.Manual);

        Assert.Equal(0.95m, rate.Bid);
        Assert.Equal(0.96m, rate.Ask);
        Assert.Equal(RateSource.Manual, rate.Source);
    }

    [Fact]
    public void UpdatePrices_WithInvalidPrices_KeepsPreviousState()
    {
        var rate = ExchangeRate.Create("USD", "EUR", 0.92m, 0.93m, RateSource.AlphaVantage);

        Assert.Throws<DomainValidationException>(() => rate.UpdatePrices(0.99m, 0.90m, RateSource.Manual));

        Assert.Equal(0.92m, rate.Bid);
        Assert.Equal(0.93m, rate.Ask);
        Assert.Equal(RateSource.AlphaVantage, rate.Source);
    }
}
