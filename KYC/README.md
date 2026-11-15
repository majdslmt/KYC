# KYC Aggregation Service (.NET 10)

A Clean Architecture ASP.NET Core API that aggregates customer data and persists the first successful snapshot in SQLite.

## Structure

- `Domain`: immutable customer and upstream data models, without framework dependencies.
- `Application`: use-case orchestration, validation, mapping, contact-selection policies and interfaces.
- `Infrastructure`: typed HTTP client, shared upstream request gate, retry/backoff, EF Core SQLite storage.
- `Api`: minimal HTTP endpoint, Swagger/OpenAPI documentation, dependency injection, centralized HTTP error mapping.
- `Tests`: xUnit business-rule tests, HTTP client tests using a fake handler, and SQLite persistence tests.

Dependency direction: API → Application + Infrastructure; Infrastructure → Application → Domain.

## Requirements

Install the **.NET 10 SDK** (packages will restore from NuGet on the first build).

```bash
dotnet restore KYC.slnx
dotnet build KYC.slnx --configuration Release
dotnet test KYC.slnx --configuration Release
dotnet run --project src/KYC.Api
```

With `ASPNETCORE_ENVIRONMENT=Development` (the included `http` launch profile sets this), Swagger UI is available at **http://localhost:5080/swagger** and the generated OpenAPI document at **http://localhost:5080/swagger/v1/swagger.json**. Swagger is disabled outside Development to avoid exposing interactive API documentation in production by default.

Then make a request:

```bash
curl http://localhost:5080/kyc-data/19800115-1234
```

Response uses JSON snake_case (`first_name`, `phone_number`, `tax_country`, etc.).

## NuGet package versions

The API explicitly references `Microsoft.OpenApi` **3.11.0** and `System.Security.Cryptography.Pkcs` **11.0.0**. Swagger uses `Swashbuckle.AspNetCore` **11.0.1**, which supports Microsoft.OpenApi 3.x (Swashbuckle 10.x uses Microsoft.OpenApi 2.x). The PKCS package is pinned as requested but the current aggregation flow does not use PKCS/CMS operations.

## Configuration

- `CustomerDataApi:BaseUrl`: upstream API origin (default in `appsettings.json`).
- `KycStorage:DatabasePath`: persistent SQLite file path (default `data/kyc.db`, relative to working directory).
- Environment-variable forms: `CustomerDataApi__BaseUrl`, `KycStorage__DatabasePath`.

## Request behavior

1. Validate the SSN format and birthday (`yyyyMMdd-xxxx`).
2. Return the persisted snapshot if present.
3. Serialize concurrent cold misses for the same SSN, check storage again, then fetch personal details.
4. If personal details exist, fetch contact details and **`/kyc-form/{ssn}`** concurrently.
5. Prefer a nonblank `preferred` email/phone, otherwise use the first nonblank one.
6. Extract `tax_country` and parse nonnegative `annual_income`; missing/invalid values become `null`.
7. Persist the complete snapshot. Future requests, including after application restart, read SQLite.

A missing personal-details response means 404. Missing optional contact/KYC responses produce null optional values. Malformed/missing required personal data and upstream failures produce 503, invalid SSN produces 400, unexpected errors 500. System errors are logged, while API errors avoid exposing internal exceptions.

The shared in-process outbound gate allows at most 30 requests in any rolling 60-second window; upstream 429 responses are retried up to two times using `Retry-After`. It is **not a distributed limiter**; for multiple replicas, use shared distributed coordination. A maximum of three upstream requests are made for one cold customer without retries.

### Limitations and deployment notes

- Cache entries are snapshots with no expiration or refresh. This explicitly follows the assignment's "no remote API again" requirement.
- Uses EF Core `EnsureCreated` for a simple self-initializing exercise database. Use versioned EF migrations for schema evolution in production.
- HTTP API and SQLite persistence tests have been authored but **not executed in the authoring environment** (the .NET SDK was not installed). Run the above commands before submission.
- SQLite persistence works on a single writable filesystem. Multiple instances sharing one database or a cross-instance rate limit require more infrastructure.
- Requests must not be logged with full SSNs or other personal details; review retention, access control, encryption, and audit requirements before production usage.

## Reflection

`REFLECTION.md` is intentionally left for the applicant to write personally, as required by the coding exercise.
