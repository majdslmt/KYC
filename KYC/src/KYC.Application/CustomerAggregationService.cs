using KYC.Domain;

namespace KYC.Application;

/// <summary>
/// Orchestrates cache-first retrieval, external lookups, and persistence.
/// Application business logic depends on abstractions, not database or HTTP types.
/// </summary>
public sealed class CustomerAggregationService : ICustomerAggregationService
{
    private readonly ICustomerSnapshotRepository _repository;
    private readonly ICustomerDataSource _source;
    // A bounded number of locks avoids unbounded per-SSN semaphore accumulation.
    // Hash collisions only reduce concurrency; they do not compromise correctness.
    private static readonly SemaphoreSlim[] Locks = Enumerable.Range(0, 128)
        .Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public CustomerAggregationService(ICustomerSnapshotRepository repository, ICustomerDataSource source)
    {
        _repository = repository;
        _source = source;
    }

    /// <summary>
    /// Validates the customer identifier and returns an existing persisted snapshot,
    /// or fetches, combines, and saves a snapshot on the first request.
    /// </summary>
    public async Task<AggregatedKycData> GetAsync(string ssn, CancellationToken cancellationToken)
    {
        if (!SsnValidator.IsValid(ssn)) throw new InvalidSsnException();

        // Fast path: no lock and no upstream request for an already cached customer.
        var cached = await _repository.FindAsync(ssn, cancellationToken);
        if (cached is not null) return cached;

        // Serialise concurrent misses for the same SSN within this process.
        // The striped locks are a bounded alternative to one semaphore per customer.
        var gate = Locks[(uint)StringComparer.Ordinal.GetHashCode(ssn) % (uint)Locks.Length];
        await gate.WaitAsync(cancellationToken);
        try
        {
            // Double-check after entering the lock: another request may have saved
            // the snapshot while this request waited.
            cached = await _repository.FindAsync(ssn, cancellationToken);
            if (cached is not null) return cached;

            var personal = await _source.GetPersonalDetailsAsync(ssn, cancellationToken);
            if (personal is null) throw new CustomerNotFoundException();

            // Optional datasets may be absent. The personal-details endpoint is authoritative.
            var contactTask = _source.GetContactDetailsAsync(ssn, cancellationToken);
            var formTask = _source.GetKycFormAsync(ssn, cancellationToken);
            await Task.WhenAll(contactTask, formTask);

            // Write only after both optional lookups and mapping have completed.
            // Failed aggregation must not leave behind a partial snapshot.
            var result = CustomerMapper.Map(ssn, personal, await contactTask, await formTask);
            await _repository.SaveAsync(result, cancellationToken);
            return result;
        }
        finally
        {
            gate.Release();
        }
    }
}
