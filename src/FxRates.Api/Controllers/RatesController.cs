using FxRates.Application.Dtos;
using FxRates.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FxRates.Api.Controllers;

/// <summary>CRUD for exchange rates plus lookup by currency pair.</summary>
[ApiController]
[Route("api/rates")]
public sealed class RatesController(IExchangeRateService service) : ControllerBase
{
    /// <summary>Lists all stored rates, ordered by currency pair.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ExchangeRateResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ExchangeRateResponse>>> GetAll(CancellationToken ct)
    {
        var rates = await service.GetAllAsync(ct);
        return Ok(rates);
    }

    /// <summary>Gets a stored rate by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<ExchangeRateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExchangeRateResponse>> GetById(int id, CancellationToken ct)
    {
        var rate = await service.GetByIdAsync(id, ct);
        return rate is null ? NotFound() : Ok(rate);
    }

    /// <summary>
    /// Gets the rate for a currency pair. If it is not stored, fetches it from AlphaVantage,
    /// stores it and publishes a RateCreated event.
    /// </summary>
    [HttpGet("{baseCurrency}/{quoteCurrency}")]
    [ProducesResponseType<ExchangeRateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ExchangeRateResponse>> GetByPair(string baseCurrency, string quoteCurrency, CancellationToken ct)
    {
        var rate = await service.GetByPairAsync(baseCurrency, quoteCurrency, ct);
        return rate is null ? NotFound() : Ok(rate);
    }

    /// <summary>Creates a rate manually and publishes a RateCreated event.</summary>
    [HttpPost]
    [ProducesResponseType<ExchangeRateResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ExchangeRateResponse>> Create(CreateRateRequest request, CancellationToken ct)
    {
        var rate = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = rate.Id }, rate);
    }

    /// <summary>Replaces the bid and ask of a rate; its source becomes Manual.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<ExchangeRateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExchangeRateResponse>> Update(int id, UpdateRateRequest request, CancellationToken ct)
    {
        var rate = await service.UpdateAsync(id, request, ct);
        return rate is null ? NotFound() : Ok(rate);
    }

    /// <summary>Deletes a rate.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await service.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }
}