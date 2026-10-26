# FX Rates API

> VFX Financial technical challenge, developed by **Pedro Ramos**.

An ASP.NET Core Web API that manages foreign exchange rates. Each currency pair (e.g. USD/EUR) has a **bid** and an **ask** price.

- Full CRUD for exchange rates, stored in **SQL Server** with Entity Framework Core.
- `GET /api/rates/{base}/{quote}` returns the stored rate. If the pair is not stored yet, the API fetches it from [AlphaVantage](https://www.alphavantage.co), stores it and returns it.
- **Bonus:** every new rate publishes a `RateCreatedEvent` to a **RabbitMQ** queue (`rates.created`).
- The whole stack (API, SQL Server, RabbitMQ) starts with **one command**: `docker compose up --build`.

---

## Tech stack

.NET 10 · ASP.NET Core (controllers) · Entity Framework Core 10 + SQL Server · RabbitMQ (`RabbitMQ.Client` 7) · `Microsoft.Extensions.Http.Resilience` · Swagger (Swashbuckle) · xUnit + NSubstitute · Docker Compose

---

## How to run

You need a free AlphaVantage API key: https://www.alphavantage.co/support/#api-key

### Option 1: Docker (recommended)

Requirements: [Docker Desktop](https://www.docker.com/products/docker-desktop).

1. Create a `.env` file in the repository root from the example and fill in your values:

   ```bash
   cp .env.example .env        # PowerShell: copy .env.example .env
   ```

   ```env
   ALPHAVANTAGE_API_KEY=your-key-here
   SQL_SA_PASSWORD=Your_strong_Passw0rd
   ```

   The SQL Server password must have at least 8 characters and include three of: upper case, lower case, digits and symbols. Avoid `$`.

2. Start everything:

   ```bash
   docker compose up --build
   ```

   The first run downloads the images and takes a few minutes. The API waits until SQL Server and RabbitMQ are healthy, then applies the EF Core migrations automatically.

| What | Where |
| --- | --- |
| Swagger UI | http://localhost:8080/swagger |
| RabbitMQ management (guest / guest) | http://localhost:15672 |

To stop: `Ctrl+C`, then `docker compose down` (this also removes the database).

### Option 2: Visual Studio / `dotnet run`

Requirements: .NET 10 SDK and SQL Server LocalDB (included with Visual Studio).

1. Store the AlphaVantage key in User Secrets (never in `appsettings.json`). In Visual Studio: right-click `FxRates.Api` → **Manage User Secrets**:

   ```json
   {
     "AlphaVantage": { "ApiKey": "your-key-here" }
   }
   ```

   Or from the command line:

   ```bash
   dotnet user-secrets set "AlphaVantage:ApiKey" "your-key-here" --project src/FxRates.Api
   ```

2. Start RabbitMQ (optional: without it the API still works, see [Events](#events-rabbitmq)):

   ```bash
   docker compose up rabbitmq
   ```

3. Run the API (F5 in Visual Studio, or `dotnet run --project src/FxRates.Api`) and open `/swagger` on the URL shown in the console.

The `FxRates` database is created in `(localdb)\MSSQLLocalDB` on startup. You can see it in Visual Studio under **View → SQL Server Object Explorer → (localdb)\MSSQLLocalDB → Databases → FxRates → Tables**.

### Tests

```bash
dotnet test
```

26 unit tests, no database or Docker needed:

- **Domain** (`ExchangeRateTests`): currency code validation and normalization, positive prices, bid not above ask, same base and quote rejected, invalid updates leave the rate unchanged.
- **Service** (`ExchangeRateServiceTests`, with NSubstitute fakes): the pair endpoint reads the database first and only calls AlphaVantage when needed; an unknown pair returns 404 and stores nothing; invalid provider data is rejected; a RabbitMQ failure does not fail the request; duplicates are rejected; update and delete of missing ids.

---

## Endpoints

| Method | Route | Success | Errors |
| --- | --- | --- | --- |
| GET | `/api/rates` | 200, all rates ordered by pair | |
| GET | `/api/rates/{id}` | 200 | 404 |
| GET | `/api/rates/{base}/{quote}` | 200, from the database or fetched from AlphaVantage and stored | 400 invalid code · 404 unknown pair · 503 provider unavailable |
| POST | `/api/rates` | 201 + `Location` header | 400 invalid data · 409 pair already exists |
| PUT | `/api/rates/{id}` | 200 (replaces bid and ask; source becomes `Manual`) | 400 · 404 |
| DELETE | `/api/rates/{id}` | 204 | 404 |

Example:

```bash
curl http://localhost:8080/api/rates/USD/EUR

curl -X POST http://localhost:8080/api/rates \
     -H "Content-Type: application/json" \
     -d '{"baseCurrency":"eur","quoteCurrency":"gbp","bid":0.85,"ask":0.86}'
```

```json
{
  "id": 2,
  "baseCurrency": "EUR",
  "quoteCurrency": "GBP",
  "bid": 0.85,
  "ask": 0.86,
  "source": "Manual",
  "lastUpdatedUtc": "2026-10-25T18:20:00Z"
}
```

Errors are returned as RFC 7807 problem details (`application/problem+json`):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Invalid request",
  "status": 400,
  "detail": "Bid and ask must be greater than zero.",
  "traceId": "00-aa6d01bc9b2fbc003b705680833911fb-34bfa6eef239acae-00"
}
```

---

## Events (RabbitMQ)

When a rate is created, manually or from AlphaVantage, the API publishes a `RateCreatedEvent` to the durable queue `rates.created` as a persistent JSON message:

```json
{"RateId":1,"BaseCurrency":"USD","QuoteCurrency":"EUR","Bid":0.8898,"Ask":0.8899,"Source":"AlphaVantage","OccurredAtUtc":"2026-10-25T18:20:00Z"}
```

To see the messages:

1. Create a rate (e.g. `GET /api/rates/USD/EUR` for a pair that is not stored yet).
2. Open http://localhost:15672 and log in with `guest` / `guest`.
3. Open **Queues and Streams** → `rates.created`.
4. Under **Get messages**, set **Ack mode** to *Nack message requeue true* (keeps the message in the queue) and click **Get Message(s)**.

The queue is only created after the first event is published. There is no consumer: the challenge only asks to publish the event.

If RabbitMQ is down, the rate is still saved and returned to the client; the failure is logged. See [Limitations](#limitations-and-possible-improvements).

---

## Architecture

```
src/
  FxRates.Domain          ExchangeRate entity and its rules. No dependencies.
  FxRates.Application     ExchangeRateService (use cases), interfaces, DTOs, events, exceptions.
  FxRates.Infrastructure  EF Core (DbContext, repository, migrations), AlphaVantage client, RabbitMQ publisher.
  FxRates.Api             Controller, global exception handler, Program.cs (registrations).
tests/
  FxRates.UnitTests       Domain rules and service logic.
```

Dependencies point inwards: Api → Application ← Infrastructure, and everything depends on Domain. The Application only knows interfaces (`IExchangeRateRepository`, `IExchangeRateProvider`, `IEventPublisher`), so the service is tested without a database, network or broker, and any of them can be replaced without touching the business logic (e.g. Azure Service Bus instead of RabbitMQ).

### Design decisions

- **Rules live in the domain.** `ExchangeRate` has private setters and a `Create` factory: currency codes must be three letters (normalized to upper case), base and quote must differ, prices must be positive and the bid cannot be above the ask. An invalid rate cannot exist anywhere in the system. Data from AlphaVantage goes through the same rules.
- **Database first, then provider.** The pair endpoint only calls AlphaVantage when the pair is not stored, which saves latency and the provider's daily quota.
- **One row per pair.** A unique index on `(BaseCurrency, QuoteCurrency)` enforces it in the database; the service also checks before inserting so a duplicate returns 409.
- **Money types.** Prices are `decimal(18,6)`, never floating point. `LastUpdatedUtc` is the time the rate was stored or last updated, set by the entity in UTC.
- **Errors in one place.** The service throws typed exceptions and a single `IExceptionHandler` maps them to HTTP: domain rule → 400, duplicate pair → 409, provider failure → 503, anything else → 500 without internal details. Controllers have no try/catch.
- **The provider's errors are not the client's.** AlphaVantage answers HTTP 200 even for errors, so the client reads the body: an unknown pair becomes 404, a rate limit, invalid data, a timeout or a network error becomes 503.
- **Resilience.** The AlphaVantage `HttpClient` uses the standard resilience handler (retries with backoff, timeouts, circuit breaker). EF Core retries transient SQL errors, e.g. while the SQL Server container is starting.
- **Messaging.** One long-lived RabbitMQ connection, created on first use and re-created if it closes; a short-lived channel per publish, because channels should not be shared by concurrent publishers. A broker failure never fails the request.
- **Logging.** Structured `ILogger` messages with searchable fields (`{RateId}`, `{Base}`, `{Quote}`) show where each rate came from. Exceptions that reach the client are logged by the exception handler (4xx as warnings, 5xx as errors); the AlphaVantage client adds a warning with the provider's reason. `System.Net.Http.HttpClient` logs are kept at `Warning` because the AlphaVantage URL contains the API key.
- **Secrets.** The API key is never in the repository: User Secrets in development, a git-ignored `.env` file for Docker.
- **Zero setup.** Migrations are applied on startup (configurable with `Database:ApplyMigrationsOnStartup`), so whoever runs the project does not need to run EF commands.

---

## Limitations and possible improvements

- **Save and publish are not atomic.** The rate is committed, then the event is published; if RabbitMQ is down, the event is lost (it is logged). Production fix: the **Outbox pattern**, writing the event to an outbox table in the same transaction and publishing it from a background worker.
- **Concurrent requests for the same new pair.** Two requests can both pass the "not stored" check: the unique index keeps the data consistent, but the losing request gets a 500 instead of the stored rate (or a 409 on POST). Fix: catch the unique-key violation and return the existing row; a per-pair lock would also avoid the duplicate AlphaVantage call.
- **Stored rates never refresh.** Once fetched, a pair is served from the database forever. Add a configurable maximum age and re-fetch stale rates, or refresh popular pairs on a schedule.
- **Lost updates.** Two simultaneous `PUT`s on the same rate are last-write-wins. Add optimistic concurrency with a `rowversion` column and `ETag`/`If-Match`.
- **No authentication or rate limiting.** Add OAuth 2.0 / JWT bearer authentication (e.g. read vs. write scopes) and rate limiting before exposing the API.
- **POST is not idempotent.** A client retrying after a timeout gets 409 for a rate it already created. An `Idempotency-Key` header would make retries safe.
- **No pagination** on `GET /api/rates`; fine for a few hundred pairs.
- **Timestamps read back from SQL Server are not marked as UTC**, so they are serialized without the `Z` suffix. An EF Core value converter would fix it.
- **Migrations on startup** are convenient for a demo; with several instances in production they should run in the deployment pipeline (set `Database:ApplyMigrationsOnStartup` to `false`).
- **No integration tests.** The unit tests cover the domain and the service with mocks. Next step: `WebApplicationFactory` hosting the real API in memory, with a SQL Server container (Testcontainers) and fakes for AlphaVantage and RabbitMQ, to test the HTTP pipeline, EF Core mappings and error handling end to end.
- **No CI pipeline.** Next step: a GitHub Actions workflow that restores, builds and runs the tests on every push and pull request.
- **RabbitMQ uses the default `guest` credentials**; in a real environment they would come from configuration and a secret store.
- **AlphaVantage free tier** has a low daily request limit; production use needs a paid plan or a streaming provider.
- **Running in Azure:** Azure SQL, Azure Service Bus instead of RabbitMQ, Key Vault for secrets, App Service or Container Apps for hosting, and Application Insights for logs and tracing.
