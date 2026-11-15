using KYC.Application;
using KYC.Domain;
using Microsoft.EntityFrameworkCore;

namespace KYC.Infrastructure;

/// <summary>SQLite-backed implementation of the snapshot persistence boundary.</summary>
public sealed class SqliteCustomerSnapshotRepository(CustomerCacheDbContext db) : ICustomerSnapshotRepository
{
    /// <summary>
    /// Returns a saved snapshot or null. AsNoTracking avoids unnecessary EF
    /// change tracking for read-only requests.
    /// </summary>
    public async Task<AggregatedKycData?> FindAsync(string ssn, CancellationToken cancellationToken)
    {
        var found = await db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Ssn == ssn, cancellationToken);
        return found?.ToDomain();
    }

    /// <summary>
    /// Persists a completed snapshot. SQLite is file-backed, so cached customers
    /// remain available after the API process restarts.
    /// </summary>
    public async Task SaveAsync(AggregatedKycData customer, CancellationToken cancellationToken)
    {
        db.Customers.Add(CustomerCacheEntry.FromDomain(customer));
        await db.SaveChangesAsync(cancellationToken);
    }
}
