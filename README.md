# Employee Sync Service (.NET 10)

ASP.NET Core Web API that receives employees from two HR providers, stores them in SQL Server and
synchronises them to the TrackTik Employees API using OAuth2 client credentials.

```
Provider 1 ──POST──┐
                   ├──> Employee Sync API ──> SQL Server (employee + sync status)
Provider 2 ──POST──┘          │
                              ├──OAuth2 client credentials──> TrackTik API (POST / PUT /employees)
                              └──EmployeeSynced──> NATS (or in-memory)
```

## Quick start (Docker Compose)

Prerequisites: Docker Desktop (Compose v2).

```bash
cp .env.example .env          # then fill in the values (see Configuration)
docker compose up --build     # API + SQL Server + NATS
```

- API: http://localhost:8080
- OpenAPI document: http://localhost:8080/openapi/v1.json
- Health: http://localhost:8080/health
- `EmployeeSync.Api.http` (in `src/EmployeeSync.Api`) has ready-made requests for VS Code / Rider / Visual Studio.

The API applies EF Core migrations on startup in compose (`Database__MigrateOnStartup=true`).
SQL Server has no arm64 image, so on Apple Silicon it runs under emulation and needs ~30 s before it is healthy.

### Try it

```bash
curl -i -X POST http://localhost:8080/api/v1/providers/provider1/employees \
  -H 'Content-Type: application/json' \
  -d '{"emp_id":"P1_001","first_name":"Alice","last_name":"Johnson","email_address":"alice.johnson@provider1.com","phone":"+1-555-0101","job_title":"Security Officer","dept":"Security Operations","hire_date":"2024-01-15","employment_status":"active"}'

curl -s http://localhost:8080/api/v1/employees/provider1/P1_001
```

Watch the events on NATS (optional, needs the [nats CLI](https://github.com/nats-io/natscli)):
`nats sub 'tracktik.employees.>'`

## Configuration

All settings are standard ASP.NET Core configuration, so every value can come from environment variables
(`__` separates sections; names are case-insensitive). **No secrets are committed**: `appsettings.json` has empty
placeholders, `.env` is git-ignored, and `.env.example` documents the variables.

| Variable | Required | Description |
|---|---|---|
| `TRACKTIK__TOKENURL` | yes | OAuth2 token URL (not under `/v1`) – from `oauth-credentials.json` |
| `TRACKTIK__APIBASEURL` | yes | API base URL **including** `/v1` |
| `TRACKTIK__CLIENTID` | yes | OAuth2 client id |
| `TRACKTIK__CLIENTSECRET` | yes | OAuth2 client secret |
| `TRACKTIK__SCOPE` | no | Requested scope; omitted by default (the token gets every scope registered for the client) |
| `TRACKTIK__TIMEOUTSECONDS` | no | HTTP timeout for TrackTik calls (default 10) |
| `CONNECTIONSTRINGS__EMPLOYEESYNC` | yes (outside compose) | SQL Server connection string |
| `DATABASE__MIGRATEONSTARTUP` | no | Apply migrations at startup (default `false`; `true` in compose) |
| `EVENTS__PUBLISHER` | no | `InMemory` (default) or `Nats` |
| `EVENTS__NATS__URL` | no | Default `nats://localhost:4222` |
| `MSSQL_SA_PASSWORD` | compose only | Local-only SA password for the SQL Server container |

The app validates the TrackTik settings at startup and refuses to start if one is missing.

For `dotnet run` outside Docker, prefer user secrets:

```bash
cd src/EmployeeSync.Api
dotnet user-secrets init
dotnet user-secrets set "TrackTik:ClientSecret" "<secret>"
# ...and the other TrackTik:* values, plus ConnectionStrings:EmployeeSync
dotnet run    # http://localhost:8080
```

## API

| Method | Route | Result |
|---|---|---|
| `POST` | `/api/v1/providers/{provider}/employees` | `201` new + synced · `200` resubmission + synced · `400` validation `ProblemDetails` · `404` unknown provider · `502` stored but TrackTik sync failed |
| `GET` | `/api/v1/employees/{provider}/{providerEmployeeId}` | `200` stored employee + sync state · `404` `ProblemDetails` |

`provider` is `provider1` or `provider2`; the body is that provider's own JSON (see the challenge `schemas/`).
Every error is `application/problem+json`. The 502 body carries `syncStatus`, `attempts`, `lastError`,
`lastAttemptAt` and the stored `employee`.

### Behaviour (the contract)

1. **Validate** the provider payload. Errors are keyed by the provider's own field path
   (e.g. `personal_info.email`) → `400`, nothing stored, nothing sent.
2. **Map** to the TrackTik schema. `employeeId = {provider}-{providerEmployeeId}` (e.g. `provider1-P1_001`), so the
   same raw id from two providers never collides. provider2 status: `employed→active`, `on_leave→inactive`,
   `terminated→terminated`. Emails are trimmed and lower-cased. A missing status defaults to `active`.
3. **Persist** (upsert on the unique index `(Provider, ProviderEmployeeId)`) and **commit as `pending`**.
4. **Forward** to TrackTik: `PUT /employees/{employeeId}` if a previous create succeeded, otherwise `POST /employees`.
   If a `PUT` returns 404 (deleted on the TrackTik side) it falls back to `POST`.
5. **Record** `synced` or `failed` with `attempts`, `lastAttemptAt` and `lastError`. Failures are never rolled back.
6. **Publish** `EmployeeSynced` only after the `synced` state is committed, once per successful sync.

Resubmitting the same employee updates the stored row and retries the sync, including rows that are `failed`.

Required fields: TrackTik needs first name, last name and email, so they are required for both providers even
though the provider2 schema only requires the top-level objects.

## Architecture

```
src/
  EmployeeSync.Domain          Employee aggregate, enums, EmployeeSynced event. No dependencies.
  EmployeeSync.Application     Commands/queries + handlers, provider mappers, sync service, ports
                               (IEmployeeRepository, IEmployeeReadStore, IUnitOfWork, ITrackTikClient, IEventPublisher).
  EmployeeSync.Infrastructure  EF Core (SQL Server) + migrations, TrackTik HTTP client + OAuth, NATS / in-memory publisher.
  EmployeeSync.Api             Minimal API endpoints, ProblemDetails, OpenAPI, composition root.
tests/
  EmployeeSync.UnitTests         Mapping, domain, handler/sync logic, token cache, HTTP client.
  EmployeeSync.IntegrationTests  WebApplicationFactory over the real pipeline.
db/migrations.sql                Idempotent script generated from the migrations (for DBAs / CI).
```

Dependencies point inwards (Api → Infrastructure → Application → Domain). The API layer only translates HTTP to
commands/queries and results to status codes.

### Design patterns used

| Pattern | Where | Why |
|---|---|---|
| **CQRS (lightweight)** | `Application/Employees/Commands`, `Queries`; `ICommandHandler` / `IQueryHandler` | Writes go through the aggregate; reads use a no-tracking read store returning DTOs. Hand-rolled: two interfaces are enough here, MediatR would add a dependency and indirection for no gain. |
| **Strategy** | `IProviderEmployeeMapper` + `Provider1EmployeeMapper` / `Provider2EmployeeMapper`, picked by `ProviderMapperResolver` | Adding a provider is one class + one DI line. |
| **Aggregate + domain events** | `Domain/Employees/Employee.cs`, `EmployeeSynced` | State transitions (`pending → synced/failed`) live in one place; events are collected and dispatched after commit. |
| **Repository + Unit of Work** | `IEmployeeRepository`, `IUnitOfWork` (the `DbContext`) | Keeps EF Core out of the application layer and makes the handler unit-testable with fakes. |
| **Adapter / Port** | `ITrackTikClient` → `TrackTikClient` (typed `HttpClient`), `IEventPublisher` → `NatsEventPublisher` / `InMemoryEventPublisher` | Infrastructure is swappable by configuration. |
| **Decorator (delegating handler)** | `TrackTikAuthenticationHandler` | Adds the Bearer token and retries once with a fresh token on `401`, transparently to the client. |
| **Options pattern** | `TrackTikOptions`, `EventsOptions` | Typed, validated config bound from env vars. |

### OAuth2 token handling

`TrackTikAccessTokenProvider` is a singleton that caches the token until `expires_in - 60s` (configurable) and uses a
`SemaphoreSlim` so concurrent requests trigger a single token call. A `401` from the API invalidates the cache and the
request is retried once. The client secret is only sent in the form body and is never logged.

## Database and migrations

EF Core migrations live in `src/EmployeeSync.Infrastructure/Persistence/Migrations`:

- `InitialCreate` – `Employees` table, unique index `UX_Employees_Provider_ProviderEmployeeId`, index on `SyncStatus`.
- `AddUspGetEmployeesBySyncStatus` – deploys the `dbo.usp_GetEmployeesBySyncStatus` report procedure from
  `Persistence/Sql/usp_GetEmployeesBySyncStatus.sql` (embedded resource, single source of truth).

Enums are stored as their API strings (`pending`, `provider1`, `active`, …) so the table and the report are readable.

```bash
dotnet tool install --global dotnet-ef

# apply to a database (or let the API do it with Database__MigrateOnStartup=true)
export ConnectionStrings__EmployeeSync="Server=localhost,1433;Database=EmployeeSync;User Id=sa;Password=<local>;TrustServerCertificate=True"
dotnet ef database update --project src/EmployeeSync.Infrastructure --startup-project src/EmployeeSync.Api

# add a migration (no database needed: DesignTimeDbContextFactory)
dotnet ef migrations add <Name> --project src/EmployeeSync.Infrastructure \
  --startup-project src/EmployeeSync.Api --output-dir Persistence/Migrations

# regenerate the idempotent script
dotnet ef migrations script --idempotent --project src/EmployeeSync.Infrastructure \
  --startup-project src/EmployeeSync.Api -o db/migrations.sql
```

### Report procedure

```sql
EXEC dbo.usp_GetEmployeesBySyncStatus;                          -- everything
EXEC dbo.usp_GetEmployeesBySyncStatus @SyncStatus = 'failed';   -- what needs attention
EXEC dbo.usp_GetEmployeesBySyncStatus @SyncStatus = 'synced', @Provider = 'provider2';
```

Returns a summary per status (count, total attempts, last attempt) and the matching rows with `LastError`.
Parameters are validated and the procedure is fully parameterised.

## Tests

```bash
dotnet test
```

No local .NET SDK? Run them in the official SDK container:

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test
```

- **Unit tests** (`tests/EmployeeSync.UnitTests`): both mappers (every field, status vocabularies, validation errors
  with provider paths, the four sample payloads → `expected-tracktik-employees.json`); the submit handler and sync
  service with in-memory fakes (pending committed before the call, POST vs PUT, PUT 404 → POST, failure → `failed` +
  `lastError`, retry increments `attempts`, no duplicates, event only on success, publish failure tolerated);
  token caching/refresh/concurrency; the typed client and the 401 retry.
- **Integration tests** (`tests/EmployeeSync.IntegrationTests`): `WebApplicationFactory<Program>` runs the real
  pipeline (routing, model binding, ProblemDetails, OpenAPI, DI, EF Core, OAuth handler, typed `HttpClient`).
  Two substitutions:
  - **SQLite in-memory** instead of SQL Server, created with `EnsureCreated()` from the same EF model. Fast and needs
    no Docker, but it does not run the SQL Server migrations or the stored procedure (see trade-offs).
  - **`FakeTrackTikApi`**, an `HttpMessageHandler` plugged in as the primary handler of both TrackTik clients. It
    checks the Bearer token and can simulate 5xx and token failures, which the hosted mock cannot.

## Trade-offs and what I would do next

- **Synchronous forward.** The contract wants the sync result in the response (502 on failure), so TrackTik is called
  inline. For volume I would accept with `202`, sync from a queue/background worker, and use the same
  `EmployeeSyncService` (it is already separate from the handler for that reason).
- **No outbox.** `EmployeeSynced` is published right after the commit; if NATS is down the event is logged and lost
  (core NATS is also at-most-once). Next step: a transactional outbox table + dispatcher, and JetStream.
- **No automatic retries / circuit breaker.** A failed sync stays `failed` until the provider resubmits. I would add
  `Microsoft.Extensions.Http.Resilience` (retry with jitter for 5xx/timeouts, circuit breaker) and a scheduled job
  that retries `failed` rows using `IX_Employees_SyncStatus`.
- **POST vs PUT from our own state.** The hosted mock does not persist employees (GET always returns a sample), so
  "exists in TrackTik" is the `ExistsInTrackTik` flag set after a successful create, plus a `PUT 404 → POST` fallback.
- **Concurrency.** Two simultaneous *first* submissions of the same employee hit the unique index and one gets a 500.
  Next: catch the unique violation and retry as an update, and add a `rowversion` for optimistic concurrency.
- **Integration DB.** SQLite keeps the suite fast and Docker-free. Testcontainers (`Testcontainers.MsSql`) would also
  cover the migrations and the stored procedure; I verified those manually against the compose SQL Server.
- **Versioning.** URL-segment versioning with a route group (`/api/v1`). `Asp.Versioning` would be worth it once a v2
  exists.
- **Auth on this API.** Not in scope; in production the provider endpoints would need authentication
  (API keys or OAuth per provider) and rate limiting.

## Assumptions

- `startDate` must be `yyyy-MM-dd`; phone numbers are stored as given (max 32 chars).
- An omitted status means `active`.
- Validation happens before persistence, so a resubmission that fails validation leaves the stored record untouched.
