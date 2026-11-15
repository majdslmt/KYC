using KYC.Domain;
using Microsoft.EntityFrameworkCore;

namespace KYC.Infrastructure;

/// <summary>EF Core mapping for persisted aggregated customer snapshots.</summary>
public sealed class CustomerCacheDbContext(DbContextOptions<CustomerCacheDbContext> options) : DbContext(options)
{
    public DbSet<CustomerCacheEntry> Customers => Set<CustomerCacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerCacheEntry>(entity =>
        {
            // One snapshot per SSN. The unique primary key also guards against
            // duplicate customer records in the SQLite file.
            entity.HasKey(x => x.Ssn);
            entity.Property(x => x.Ssn).HasMaxLength(13);
            entity.Property(x => x.FirstName).IsRequired();
            entity.Property(x => x.LastName).IsRequired();
            entity.Property(x => x.Address).IsRequired();
        });
    }
}

/// <summary>
/// Database entity isolated from the Domain record, so storage details can change
/// without changing the API response or application model.
/// </summary>
public sealed class CustomerCacheEntry
{
    public string Ssn { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Address { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? TaxCountry { get; set; }
    public long? Income { get; set; }

    /// <summary>Converts a persisted row to the public immutable snapshot.</summary>
    public AggregatedKycData ToDomain() => new(Ssn, FirstName, LastName,
        Address, PhoneNumber, Email, TaxCountry, Income);

    /// <summary>Creates a database row from a completed aggregation.</summary>
    public static CustomerCacheEntry FromDomain(AggregatedKycData data) => new()
    {
        Ssn = data.Ssn, FirstName = data.FirstName, LastName = data.LastName,
        Address = data.Address, PhoneNumber = data.PhoneNumber, Email = data.Email,
        TaxCountry = data.TaxCountry, Income = data.Income
    };
}
