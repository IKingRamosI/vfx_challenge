using System.Net;
using System.Net.Http.Json;
using FxRates.Application.Dtos;
using FxRates.Application.Events;
using Microsoft.AspNetCore.Mvc;

namespace FxRates.IntegrationTests;

/// <summary>
/// End-to-end tests through HTTP: routing, controller, service, EF Core and error handling.
/// xUnit creates a new instance per test, so every test gets its own API host and database.
/// </summary>
public sealed class RatesApiTests : IDisposable
{
    private readonly FxRatesApiFactory _factory = new();
    private readonly HttpClient _client;

    public RatesApiTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // GET /api/rates/{base}/{quote}

    [Fact]
    public async Task GetByPair_FirstCallFetchesAndStores_SecondCallIsServedFromDatabase()
    {
        _factory.Provider.Set("USD", "EUR", 0.8898m, 0.8899m);

        var first = await _client.GetFromJsonAsync<ExchangeRateResponse>("/api/rates/usd/eur");
        var second = await _client.GetFromJsonAsync<ExchangeRateResponse>("/api/rates/USD/EUR");

        Assert.Equal("AlphaVantage", first!.Source);
        Assert.Equal(first.Id, second!.Id);
        Assert.Equal(1, _factory.Provider.Calls);                                // AlphaVantage was called only once
        Assert.IsType<RateCreatedEvent>(Assert.Single(_factory.Publisher.Published)); // one event for one new rate
    }

    [Fact]
    public async Task GetByPair_UnknownPair_Returns404()
    {
        var response = await _client.GetAsync("/api/rates/USD/XYZ");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(_factory.Publisher.Published);
    }

    [Fact]
    public async Task GetByPair_ProviderUnavailable_Returns503ProblemDetails()
    {
        _factory.Provider.IsUnavailable = true;

        var response = await _client.GetAsync("/api/rates/USD/EUR");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Rate provider unavailable", problem!.Title);
    }

    [Fact]
    public async Task GetByPair_InvalidCurrencyCode_Returns400ProblemDetails()
    {
        var response = await _client.GetAsync("/api/rates/US1/EUR");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, _factory.Provider.Calls);
    }

    // POST /api/rates

    [Fact]
    public async Task Create_Returns201WithLocation_AndTheRateCanBeReadBack()
    {
        var created = await _client.PostAsJsonAsync("/api/rates", new CreateRateRequest("eur", "gbp", 0.85m, 0.86m));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);

        var stored = await _client.GetFromJsonAsync<ExchangeRateResponse>(created.Headers.Location);

        Assert.Equal("EUR", stored!.BaseCurrency);
        Assert.Equal("GBP", stored.QuoteCurrency);
        Assert.Equal("Manual", stored.Source);
        Assert.Single(_factory.Publisher.Published);
    }

    [Fact]
    public async Task Create_DuplicatePair_Returns409()
    {
        var request = new CreateRateRequest("EUR", "GBP", 0.85m, 0.86m);

        var created = await _client.PostAsJsonAsync("/api/rates", request);
        var duplicate = await _client.PostAsJsonAsync("/api/rates", request);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Create_BidAboveAsk_Returns400AndStoresNothing()
    {
        var response = await _client.PostAsJsonAsync("/api/rates", new CreateRateRequest("EUR", "GBP", 0.90m, 0.86m));
        var all = await _client.GetFromJsonAsync<List<ExchangeRateResponse>>("/api/rates");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(all!);
    }

    // PUT and DELETE /api/rates/{id}

    [Fact]
    public async Task Update_ChangesArePersisted()
    {
        var created = await _client.PostAsJsonAsync("/api/rates", new CreateRateRequest("EUR", "GBP", 0.85m, 0.86m));
        var rate = await created.Content.ReadFromJsonAsync<ExchangeRateResponse>();

        var updated = await _client.PutAsJsonAsync($"/api/rates/{rate!.Id}", new UpdateRateRequest(0.87m, 0.88m));
        var stored = await _client.GetFromJsonAsync<ExchangeRateResponse>($"/api/rates/{rate.Id}");

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(0.87m, stored!.Bid);   // read back from the database, not from the PUT response
        Assert.Equal(0.88m, stored.Ask);
    }

    [Fact]
    public async Task Delete_Returns204_ThenTheRateIsGone()
    {
        var created = await _client.PostAsJsonAsync("/api/rates", new CreateRateRequest("EUR", "GBP", 0.85m, 0.86m));
        var rate = await created.Content.ReadFromJsonAsync<ExchangeRateResponse>();

        var deleted = await _client.DeleteAsync($"/api/rates/{rate!.Id}");
        var afterDelete = await _client.GetAsync($"/api/rates/{rate.Id}");
        var secondDelete = await _client.DeleteAsync($"/api/rates/{rate.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, secondDelete.StatusCode);
    }
}
