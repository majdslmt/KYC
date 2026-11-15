
# KYC Aggregation Service

A REST API built with **ASP.NET Core / .NET 10** and **C#**, following **Clean Architecture**.

The application aggregates customer identity, contact information, and Know Your Customer (KYC) form data from an external Customer Data API into a single response.

On the first successful request, the aggregated customer data is stored locally using **Entity Framework Core and SQLite**. Subsequent requests retrieve the saved data without making additional requests to the external API.

## Features

- RESTful API using ASP.NET Core Minimal API
- Clean Architecture with separation of concerns
- Integration with the external Customer Data API
- Persistent storage using Entity Framework Core and SQLite
- Customer data caching to avoid unnecessary API calls
- Social Security Number (SSN) validation
- Preferred email and phone number selection
- KYC data aggregation and mapping
- Centralized exception handling
- Structured application logging
- External API rate limiting and retry handling
- Swagger UI and OpenAPI documentation
- Unit tests using xUnit
- Dependency Injection and asynchronous programming

## Technology Stack

| Technology | Description |
|---|---|
| .NET 10 | Application framework |
| C# | Programming language |
| ASP.NET Core | REST API |
| Clean Architecture | Application design |
| Entity Framework Core 10 | Object-Relational Mapper |
| SQLite | Persistent database |
| HttpClientFactory | External API communication |
| Swashbuckle.AspNetCore 11.0.1 | Swagger documentation |
| Microsoft.OpenApi 3.11.0 | OpenAPI support |
| System.Security.Cryptography.Pkcs 11.0.0 | Referenced NuGet dependency |
| xUnit | Unit testing |
| Git | Version control |

## Project Structure

The solution follows Clean Architecture principles.

```text
KYC/
|
|-- KYC.slnx
|-- Directory.Build.props
|-- README.md
|-- REFLECTION.md
|
|-- src/
|   |
|   |-- KYC.Api/
|   |   |-- Program.cs
|   |   |-- ApiExceptionHandler.cs
|   |   |-- appsettings.json
|   |
|   |-- KYC.Application/
|   |   |-- CustomerAggregationService.cs
|   |   |-- CustomerMapper.cs
|   |   |-- ContactSelector.cs
|   |   |-- SsnValidator.cs
|   |   |-- Abstractions.cs
|   |
|   |-- KYC.Domain/
|   |   |-- Models.cs
|   |
|   |-- KYC.Infrastructure/
|       |-- CustomerDataApiClient.cs
|       |-- CustomerCacheDbContext.cs
|       |-- SqliteCustomerSnapshotRepository.cs
|       |-- OutboundRequestGate.cs
|       |-- DependencyInjection.cs
|
|-- tests/
    |
    |-- KYC.Tests/
        |-- CustomerAggregationServiceTests.cs
        |-- CustomerMapperTests.cs
        |-- ContactSelectorTests.cs
        |-- SsnValidatorTests.cs
        |-- CustomerDataApiClientTests.cs
        |-- OutboundRequestGateTests.cs
        |-- SqliteCustomerSnapshotRepositoryTests.cs
```

## Clean Architecture

The application is divided into four main layers.

### 1. Domain Layer

The Domain layer contains the core data models.

Responsibilities:

- Represent customer information
- Define domain entities and models
- Remain independent of external frameworks

This layer does not depend on the other application layers.

### 2. Application Layer

The Application layer contains the business logic.

Responsibilities:

- Validate customer SSNs
- Aggregate customer information
- Select preferred contact details
- Map external API responses
- Extract KYC form information
- Coordinate database and external API operations

The Application layer defines abstractions for persistence and external integrations.

### 3. Infrastructure Layer

The Infrastructure layer implements external dependencies.

Responsibilities:

- Communicate with the Customer Data API
- Store customer information in SQLite
- Implement repository operations
- Handle external API rate limiting
- Retry requests when appropriate

Entity Framework Core is used for database access.

### 4. API Layer

The API layer exposes the application functionality through HTTP endpoints.

Responsibilities:

- Configure dependency injection
- Define REST API endpoints
- Validate and process requests
- Translate exceptions into HTTP responses
- Configure Swagger documentation

## Prerequisites

Before running the application, install:

- .NET 10 SDK
- Visual Studio 2022 or later with .NET 10 support, or Visual Studio Code
- Git
- Internet connection for NuGet and external API access

Verify the installed .NET SDK:

```bash
dotnet --version
```

The output should indicate a .NET 10 SDK.

SQLite does not require a separate database server installation.

## Getting Started

### 1. Clone the Repository

```bash
git clone <repository-url>
cd KYC
```

Replace `<repository-url>` with the actual GitHub repository URL.

### 2. Restore Dependencies

```bash
dotnet restore KYC.slnx
```

### 3. Build the Solution

```bash
dotnet build KYC.slnx
```

### 4. Run Unit Tests

```bash
dotnet test KYC.slnx
```

### 5. Run the Application

```bash
dotnet run --project src/KYC.Api --launch-profile http
```

The API runs at:

```text
http://localhost:5080
```

## Swagger Documentation

Swagger UI is enabled in the Development environment.

Open:

http://localhost:5080/swagger

The OpenAPI specification is available at:

http://localhost:5080/swagger/v1/swagger.json

Swagger allows developers to:

- Explore available API endpoints
- View request parameters
- Inspect response schemas
- Execute API requests
- Inspect HTTP status codes and JSON responses

To test the application:

1. Open Swagger UI.
2. Expand `GET /kyc-data/{ssn}`.
3. Click "Try it out".
4. Enter a valid SSN.
5. Click "Execute".
6. Inspect the returned response.

## API Endpoints

### GET /kyc-data/{ssn}

Returns aggregated customer and KYC information.

**Request:**

```http
GET /kyc-data/19800115-1234
```

**Example Response:**

```json
{
  "ssn": "19800115-1234",
  "first_name": "Lars",
  "last_name": "Larsson",
  "address": "Smågatan 1, 123 22 Malmö",
  "phone_number": "+46 70 123 45 67",
  "email": "lars.larsson@example.com",
  "tax_country": "SE",
  "income": 550000
}
```

The response above is illustrative. Actual values depend on the external Customer Data API.

### Response Fields

| Field | Description |
|---|---|
| ssn | Customer Social Security Number |
| first_name | Customer first name |
| last_name | Customer surname |
| address | Customer address |
| phone_number | Preferred customer phone number |
| email | Preferred customer email |
| tax_country | Tax country from the KYC form |
| income | Annual income from the KYC form |

The `tax_country` field is nullable.

If the KYC form does not contain `tax_country`, the application returns `null`.

The `income` field is also nullable.

If annual income is unavailable or cannot be parsed into a valid integer, the application returns `null`.

## External Customer Data API

The application integrates with the following external service:

```text
https://customerdataapi-codetest-bughceejfwbwbkb9.swedencentral-01.azurewebsites.net/
```

Three endpoints are used.

### Personal Details

```http
GET /personal-details/{ssn}
```

Retrieves:

- First name
- Surname
- Address

### Contact Details

```http
GET /contact-details/{ssn}
```

Retrieves:

- Email addresses
- Phone numbers
- Additional contact addresses

The application selects the preferred nonempty email and phone number.

If a preferred value is unavailable, the first nonempty value is used.

### KYC Form

```http
GET /kyc-form/{ssn}
```

Retrieves KYC information represented as key-value pairs.

Example:

```json
{
  "items": [
    {
      "key": "annual_income",
      "value": "550000"
    },
    {
      "key": "tax_country",
      "value": "SE"
    }
  ]
}
```

The application maps:

- `annual_income` to `income`
- `tax_country` to `tax_country`

### Legacy KYC Endpoint

The external Customer Data API also documents:

```http
GET /kyc-form/{ssn}/{asOfDate}
```

This is a legacy endpoint retained for backward compatibility.

The date must use the format:

```text
yyyy-MM-dd
```

The KYC Aggregation Service uses the newer endpoint:

```http
GET /kyc-form/{ssn}
```

No date parameter is required.

## Data Persistence

SQLite is used to persist aggregated customer information.

The application follows a cache-first approach.

### First Request

When a customer is requested for the first time:

1. Validate the SSN.
2. Search the local SQLite database.
3. If the customer is not found, call the external API.
4. Retrieve personal details.
5. Retrieve contact details.
6. Retrieve the KYC form.
7. Aggregate and map the information.
8. Save the aggregated customer data in SQLite.
9. Return the response.

### Subsequent Requests

When the same customer is requested again:

1. Validate the SSN.
2. Search the local database.
3. Retrieve the existing customer record.
4. Return the saved data.

No additional calls to the external API are required.

### Persistence Across Restarts

Customer records are stored in a SQLite database file.

The default path is:

```text
data/kyc.db
```

The path is relative to the application's working directory.

Because SQLite stores data on disk, the information remains available after restarting the application, provided the same database path is used.

### Concurrency Handling

The application uses synchronization to prevent concurrent requests for the same uncached customer from unnecessarily fetching duplicate data.

After entering the synchronization section, the cache is checked again before calling the external API.

This protection applies within a single application process.

## Error Handling

The application implements centralized exception handling.

Errors are translated into appropriate HTTP responses.

| HTTP Status | Description |
|---|---|
| 200 OK | Customer information retrieved successfully |
| 400 Bad Request | Invalid SSN |
| 404 Not Found | Customer personal details not found |
| 503 Service Unavailable | Required external data cannot be retrieved |
| 500 Internal Server Error | Unexpected application failure |

### Example 404 Response

```json
{
  "error": "Customer data not found for the provided SSN."
}
```

### Error Handling Strategy

The application separates expected application errors from unexpected system failures.

Expected errors are returned with meaningful HTTP status codes.

Unexpected errors are logged on the server.

Sensitive exception details are not included in public API responses.

## Rate Limiting and Retry Handling

The external Customer Data API supports a maximum of:

**30 requests per minute per client IP address.**

When the limit is exceeded, the external service responds with:

```http
429 Too Many Requests
```

The response may include:

```http
Retry-After: 10
```

The application implements outbound request throttling to reduce the likelihood of exceeding the external API limit.

It also supports retry handling when HTTP 429 is returned.

### Retry Strategy

When a rate limit response is received:

1. Read the `Retry-After` header.
2. Wait for the indicated interval.
3. Retry the request.
4. Stop after the configured maximum number of attempts.

The implementation allows up to three total attempts.

The outbound throttling mechanism operates per application instance rather than across multiple deployed instances.

## Configuration

Application configuration is stored in:

```text
src/KYC.Api/appsettings.json
```

Example:

```json
{
  "CustomerDataApi": {
    "BaseUrl": "https://customerdataapi-codetest-bughceejfwbwbkb9.swedencentral-01.azurewebsites.net/"
  },
  "KycStorage": {
    "DatabasePath": "data/kyc.db"
  }
}
```

### Environment Variables

Configuration can be overridden using environment variables.

| Variable | Purpose |
|---|---|
| CustomerDataApi__BaseUrl | External API base URL |
| KycStorage__DatabasePath | SQLite database location |
| ASPNETCORE_ENVIRONMENT | Application environment |
| ASPNETCORE_URLS | Application listening URL |

## Unit Testing

The project uses xUnit for automated testing.

Unit tests focus on business logic and expected application behavior.

### Test Modules

| Test Class | Description |
|---|---|
| SsnValidatorTests | Validates SSN formats and dates |
| ContactSelectorTests | Tests preferred email and phone selection |
| CustomerMapperTests | Tests customer and KYC mapping |
| CustomerAggregationServiceTests | Tests aggregation and caching behavior |
| CustomerDataApiClientTests | Tests external HTTP integration behavior |
| OutboundRequestGateTests | Tests outbound request throttling |
| SqliteCustomerSnapshotRepositoryTests | Tests database persistence behavior |

### Run All Tests

```bash
dotnet test KYC.slnx
```

### Run Tests with Detailed Output

```bash
dotnet test KYC.slnx --logger "console;verbosity=detailed"
```

### Run Tests in a Specific Project

```bash
dotnet test tests/KYC.Tests/KYC.Tests.csproj
```

### Testing Principles

The tests are designed to verify:

- Correct business behavior
- Expected results for valid inputs
- Error handling for invalid inputs
- Mapping of nullable information
- Selection of preferred contact information
- Cache-first customer retrieval
- Correct handling of upstream HTTP responses
- Persistence of saved records

External dependencies are replaced with test doubles where appropriate to keep business-rule tests isolated.

**Verification note:** Compilation and test execution must be verified locally with the .NET 10 SDK before submission.

## Test Data

The external Customer Data API provides the following test SSNs:

```text
19800115-1234
19900220-5678
19751230-9101
19850505-4321
19951212-3456
```

### Test Using cURL

```bash
curl -i http://localhost:5080/kyc-data/19800115-1234

curl -i http://localhost:5080/kyc-data/19900220-5678

curl -i http://localhost:5080/kyc-data/19751230-9101

curl -i http://localhost:5080/kyc-data/19850505-4321

curl -i http://localhost:5080/kyc-data/19951212-3456
```

### Test All Customers with PowerShell

```powershell
$ssns = @(
    "19800115-1234",
    "19900220-5678",
    "19751230-9101",
    "19850505-4321",
    "19951212-3456"
)

foreach ($ssn in $ssns) {
    Write-Host "`nTesting customer: $ssn"

    curl.exe -i "http://localhost:5080/kyc-data/$ssn"
}
```

## Manual Testing Scenarios

The following scenarios are recommended for validating the application.

| Scenario | Expected Result |
|---|---|
| Valid customer SSN | HTTP 200 |
| Invalid SSN format | HTTP 400 |
| Nonexistent customer | HTTP 404 |
| First customer request | Fetch and persist data |
| Repeated customer request | Retrieve data from SQLite |
| Application restart | Previously saved data remains available |
| Missing tax country | Return null |
| Missing annual income | Return null |
| External API unavailable | Appropriate error response |
| External API rate limit exceeded | Retry according to configured policy |

## Security Considerations

The application processes customer personal information.

Additional security measures would be required before production deployment.

These include:

- Authentication and authorization
- Encryption of sensitive stored information
- Secure configuration and secret management
- HTTPS enforcement
- Audit logging
- Data retention and deletion policies
- Protection of sensitive information in logs
- Access control for the database
- Monitoring and observability

## Design Considerations

### Why Clean Architecture?

Clean Architecture separates business rules from infrastructure implementation details.

This improves:

- Maintainability
- Testability
- Separation of concerns
- Flexibility
- Dependency management

Business rules can be tested independently of database and HTTP infrastructure.

### Why SQLite?

SQLite provides persistent data storage without requiring a separate database server.

It is suitable for a small application or coding assignment.

Benefits include:

- Simple configuration
- Lightweight storage
- EF Core integration
- No external database service
- Data persistence after restarts

### Why Entity Framework Core?

Entity Framework Core provides ORM functionality and simplifies data access.

It allows the application to work with strongly typed C# objects rather than requiring manual SQL queries for standard persistence operations.

### Why HttpClientFactory?

HttpClientFactory supports structured HTTP client configuration and dependency injection.

It also improves management of outbound HTTP connections.

### Why Unit Testing?

Unit tests provide confidence that business rules behave correctly.

They also make refactoring safer and help identify regressions.

## Known Limitations

This implementation focuses on the coding assignment requirements.

Current limitations include:

- Cached customer information does not automatically expire.
- SQLite is intended for a simple single-instance deployment.
- The outbound request limiter is not distributed.
- Concurrent request protection is limited to one application process.
- Production authentication and authorization are not implemented.
- Full production observability and compliance controls are outside the current scope.

Possible future improvements include:

- Configurable cache expiration and refresh
- Distributed caching
- SQL Server or PostgreSQL support
- Distributed rate limiting
- Authentication and authorization
- Health-check endpoints
- Additional integration tests
- OpenTelemetry tracing and metrics
- Automated CI/CD pipelines
- EF Core migrations for database schema changes

## Troubleshooting

### Swagger Returns 404

Ensure the application is running in the Development environment.

Run:

```bash
dotnet run --project src/KYC.Api --launch-profile http
```

Then open:

```text
http://localhost:5080/swagger
```

### Application Does Not Start

Verify that .NET 10 is installed.

```bash
dotnet --version
```

Restore the required packages:

```bash
dotnet restore KYC.slnx
```

### Customer Returns 404

Check that the supplied SSN exists in the external Customer Data API.

Try one of the provided test SSNs.

### SQLite Database Is Not Found

The database is created automatically when the application starts, provided the configured location is writable.

Verify the database path in `appsettings.json`.

### API Returns 503

Check whether the external Customer Data API is available.

Verify network connectivity and inspect the application logs.

Rate limiting or missing required upstream data may also cause the operation to fail.

## Submission

Before submitting the coding assignment:

1. Restore and build the application.
2. Run all automated tests.
3. Verify the API using Swagger.
4. Test the provided customer SSNs.
5. Verify that SQLite caching works.
6. Complete `REFLECTION.md` in your own words.
7. Commit the final changes to the main branch.
8. Create a ZIP archive of the solution.
9. Submit the project according to the assignment instructions.

## Reflection

The project includes a separate `REFLECTION.md` file.

As required by the coding assignment, this file should be written personally by the applicant and describe:

- Technical decisions
- Architectural choices
- Trade-offs and assumptions
- Testing decisions
- Potential improvements
- Topics for technical discussion

## License

This project was developed as part of a technical coding assignment for demonstration and evaluation purposes.
