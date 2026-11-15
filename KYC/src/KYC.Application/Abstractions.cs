using KYC.Domain;

namespace KYC.Application;

/// <summary>Boundary for the three upstream Customer Data API endpoints.</summary>
/// <remarks>Implementations may use HTTP, but business logic does not know about HTTP.</remarks>
public interface ICustomerDataSource
{
    Task<PersonalDetails?> GetPersonalDetailsAsync(string ssn, CancellationToken cancellationToken);
    Task<ContactDetails?> GetContactDetailsAsync(string ssn, CancellationToken cancellationToken);
    Task<KycForm?> GetKycFormAsync(string ssn, CancellationToken cancellationToken);
}

/// <summary>Persistence boundary for completed aggregated customer snapshots.</summary>
public interface ICustomerSnapshotRepository
{
    Task<AggregatedKycData?> FindAsync(string ssn, CancellationToken cancellationToken);
    Task SaveAsync(AggregatedKycData customer, CancellationToken cancellationToken);
}

/// <summary>Application use case exposed to the HTTP entry point.</summary>
public interface ICustomerAggregationService
{
    Task<AggregatedKycData> GetAsync(string ssn, CancellationToken cancellationToken);
}
