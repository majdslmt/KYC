using System.Text.Json;
using KYC.Api;
using KYC.Application;
using KYC.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// API contract uses snake_case (first_name, tax_country, etc.), while C#
// model properties remain PascalCase according to .NET conventions.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
// Describe the API for interactive Swagger UI and generated OpenAPI JSON.
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "KYC Aggregation API Service",
        Version = "1.0.0",
        Description = "API for aggregating customer identity, contact, and KYC data."
    });
});
// All endpoints share the same error mapping and logging policy.
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddScoped<ICustomerAggregationService, CustomerAggregationService>();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseExceptionHandler();

// Keep interactive documentation unavailable outside development by default.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "KYC Aggregation API v1"));
}

// EnsureCreated is adequate for a coding test; a production system should use EF migrations.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CustomerCacheDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// Public aggregation endpoint: the use case decides whether to return a saved
// snapshot or fetch and persist fresh data from the three upstream endpoints.
app.MapGet("/kyc-data/{ssn}", async (string ssn,
    ICustomerAggregationService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetAsync(ssn, cancellationToken)))
    .WithName("GetAggregatedKycData")
    .WithTags("KYC Aggregation")
    .WithSummary("Get aggregated KYC data")
    .WithDescription("Fetches and persists customer KYC data on the first request; subsequent requests use the stored snapshot.")
    .Produces<KYC.Domain.AggregatedKycData>(StatusCodes.Status200OK)
    .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
    .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
    .Produces<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable)
    .Produces<ApiErrorResponse>(StatusCodes.Status500InternalServerError);

app.Run();

public partial class Program { }
