using FxRates.Api.ErrorHandling;
using FxRates.Application;
using FxRates.Application.Interfaces;
using FxRates.Infrastructure.AlphaVantage;
using FxRates.Infrastructure.Persistence;
using FxRates.Infrastructure.RabbitMq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Application
builder.Services.AddScoped<IExchangeRateService, ExchangeRateService>();

// Database
builder.Services.AddDbContext<FxRatesDbContext>(o => o.UseSqlServer(
    builder.Configuration.GetConnectionString("Default"),
    sql => sql.EnableRetryOnFailure())); // retries transient SQL errors
builder.Services.AddScoped<IExchangeRateRepository, ExchangeRateRepository>();

// AlphaVantage
builder.Services.Configure<AlphaVantageOptions>(builder.Configuration.GetSection("AlphaVantage"));
builder.Services.AddHttpClient<IExchangeRateProvider, AlphaVantageClient>((sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<AlphaVantageOptions>>().Value;
    client.BaseAddress = new Uri(o.BaseUrl);
})
    .AddStandardResilienceHandler(); // retries, timeouts and circuit breaker

// RabbitMQ
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapControllers();

// Create or update the database at startup, so whoever runs the project needs no extra command.
if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<FxRatesDbContext>().Database.MigrateAsync();
}

app.Run();