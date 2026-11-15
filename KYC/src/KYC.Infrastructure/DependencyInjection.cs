using KYC.Application;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KYC.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers infrastructure implementations behind application interfaces.
    /// Configuration controls the SQLite file location and upstream base URL.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services,
        IConfiguration configuration)
    {
        var path = Path.GetFullPath(configuration["KycStorage:DatabasePath"] ?? "data/kyc.db");
        // Ensure the data directory exists before EF opens the SQLite file.
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();

        services.AddDbContext<CustomerCacheDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<ICustomerSnapshotRepository, SqliteCustomerSnapshotRepository>();
        // A singleton coordinates the provider's shared per-IP quota across all
        // requests made by this application process.
        services.AddSingleton<OutboundRequestGate>();
        // Typed HttpClient supplies connection pooling and centralizes timeouts.
        services.AddHttpClient<ICustomerDataSource, CustomerDataApiClient>(client =>
        {
            var url = configuration["CustomerDataApi:BaseUrl"]
                ?? "https://customerdataapi-codetest-bughceejfwbwbkb9.swedencentral-01.azurewebsites.net/";
            client.BaseAddress = new Uri(url.TrimEnd('/') + "/", UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        return services;
    }
}
