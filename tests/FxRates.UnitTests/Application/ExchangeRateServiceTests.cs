using FxRates.Application;
using FxRates.Application.Dtos;
using FxRates.Application.Events;
using FxRates.Application.Exceptions;
using FxRates.Application.Interfaces;
using FxRates.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FxRates.UnitTests.Application;

public class ExchangeRateServiceTests
{
    private readonly IExchangeRateRepository _repository = Substitute.For<IExchangeRateRepository>();
    private readonly IExchangeRateProvider _provider = Substitute.For<IExchangeRateProvider>();
    private readonly IEventPublisher _publisher = Substitute.For<IEventPublisher>();
    private readonly ExchangeRateService _sut;

    public ExchangeRateServiceTests() => _sut = new ExchangeRateService(
        _repository, _provider, _publisher, NullLogger<ExchangeRateService>.Instance
        );

    // GetByPairAsync

    [Fact]
    public async Task GetByPair_WhenStored_ReturnsItWithoutCallingProvider()
    {
        var stored = ExchangeRate.Create("USD", "EUR", 1.10m, 1.11m, RateSource.Manual);

        _repository.GetByPairAsync("USD", "EUR", Arg.Any<CancellationToken>()).Returns(stored);

        var result = await _sut.GetByPairAsync("usd", "eur", CancellationToken.None);

        Assert.Equal(1.10m, result!.Bid);

        await _provider.DidNotReceiveWithAnyArgs().GetRateAsync(default!, default!, default);
    }

    [Fact]
    public async Task GetByPair_WhenMissing_FetchesStoresAndPublishes()
    {
        _provider.GetRateAsync("GBP", "USD", Arg.Any<CancellationToken>()).Returns(new ExternalRate(1.25m, 1.26m));

        var result = await _sut.GetByPairAsync("GBP", "USD", CancellationToken.None);

        Assert.Equal("AlphaVantage", result!.Source);

        await _repository.Received(1).AddAsync(Arg.Any<ExchangeRate>(), Arg.Any<CancellationToken>());

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        await _publisher.Received(1).PublishAsync(Arg.Any<string>(), Arg.Any<RateCreatedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByPair_WhenProviderDoesNotKnowThePair_ReturnsNullAndStoresNothing()
    {
        _provider.GetRateAsync("USD", "XYZ", Arg.Any<CancellationToken>()).Returns((ExternalRate?)null);

        var result = await _sut.GetByPairAsync("USD", "XYZ", CancellationToken.None);

        Assert.Null(result);

        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);

        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync<RateCreatedEvent>(default!, default!, default);
    }

    [Fact]
    public async Task GetByPair_WhenProviderSendsInvalidPrices_ThrowsProviderUnavailable()
    {
        // bid above ask: the provider's data breaks a domain rule, so it is the provider's fault (503), not the client's (400)
        _provider.GetRateAsync("USD", "EUR", Arg.Any<CancellationToken>()).Returns(new ExternalRate(1.30m, 1.20m));

        await Assert.ThrowsAsync<ExternalProviderUnavailableException>(() =>
            _sut.GetByPairAsync("USD", "EUR", CancellationToken.None));

        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task GetByPair_WhenPublishFails_StillReturnsTheRate()
    {
        _provider.GetRateAsync("USD", "JPY", Arg.Any<CancellationToken>()).Returns(new ExternalRate(150.10m, 150.12m));

        _publisher.PublishAsync(Arg.Any<string>(), Arg.Any<RateCreatedEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("broker down"));

        var result = await _sut.GetByPairAsync("USD", "JPY", CancellationToken.None);

        Assert.NotNull(result);

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByPair_WithInvalidCode_ThrowsWithoutCallingProvider()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            _sut.GetByPairAsync("US1", "EUR", CancellationToken.None));

        await _provider.DidNotReceiveWithAnyArgs().GetRateAsync(default!, default!, default);
    }

    // CreateAsync

    [Fact]
    public async Task Create_WithNewPair_StoresAndPublishes()
    {
        var result = await _sut.CreateAsync(new CreateRateRequest("eur", "gbp", 0.85m, 0.86m), CancellationToken.None);

        Assert.Equal("EUR", result.BaseCurrency);
        Assert.Equal("GBP", result.QuoteCurrency);
        Assert.Equal("Manual", result.Source);

        await _repository.Received(1).AddAsync(Arg.Any<ExchangeRate>(), Arg.Any<CancellationToken>());

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        await _publisher.Received(1).PublishAsync(Arg.Any<string>(), Arg.Any<RateCreatedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WhenPairExists_ThrowsAndStoresNothing()
    {
        _repository.ExistsAsync("EUR", "GBP", Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<RateAlreadyExistsException>(() =>
            _sut.CreateAsync(new CreateRateRequest("eur", "gbp", 0.85m, 0.86m), CancellationToken.None));

        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    // UpdateAsync

    [Fact]
    public async Task Update_WhenRateExists_ChangesPricesAndSaves()
    {
        var stored = ExchangeRate.Create("USD", "EUR", 0.92m, 0.93m, RateSource.AlphaVantage);

        _repository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(stored);

        var result = await _sut.UpdateAsync(1, new UpdateRateRequest(0.95m, 0.96m), CancellationToken.None);

        Assert.Equal(0.95m, result!.Bid);
        Assert.Equal(0.96m, result.Ask);
        Assert.Equal("Manual", result.Source);

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WhenRateDoesNotExist_ReturnsNull()
    {
        _repository.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns((ExchangeRate?)null);

        var result = await _sut.UpdateAsync(99, new UpdateRateRequest(0.95m, 0.96m), CancellationToken.None);

        Assert.Null(result);

        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    // DeleteAsync

    [Fact]
    public async Task Delete_WhenRateExists_RemovesAndReturnsTrue()
    {
        var stored = ExchangeRate.Create("USD", "EUR", 0.92m, 0.93m, RateSource.Manual);

        _repository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(stored);

        var deleted = await _sut.DeleteAsync(1, CancellationToken.None);

        Assert.True(deleted);

        _repository.Received(1).Remove(stored);

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WhenRateDoesNotExist_ReturnsFalse()
    {
        _repository.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns((ExchangeRate?)null);

        var deleted = await _sut.DeleteAsync(99, CancellationToken.None);

        Assert.False(deleted);

        _repository.DidNotReceiveWithAnyArgs().Remove(default!);
    }
}
