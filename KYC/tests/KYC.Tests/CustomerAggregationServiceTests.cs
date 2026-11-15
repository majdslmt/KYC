using KYC.Application;
using KYC.Domain;

namespace KYC.Tests;

public sealed class CustomerAggregationServiceTests
{
    private const string Ssn = "19800115-1234";
    private static readonly PersonalDetails Personal = new("Anna", "Andersson", "Street");

    [Fact]
    public async Task Cold_request_loads_three_sources_and_persists_snapshot()
    {
        var source = new FakeSource { Personal = Personal, Contacts = new(null, null), Form = new(null) };
        var repository = new FakeRepository();
        var service = new CustomerAggregationService(repository, source);

        var result = await service.GetAsync(Ssn, CancellationToken.None);

        Assert.Equal("Anna", result.FirstName);
        Assert.Equal(1, source.PersonalCalls);
        Assert.Equal(1, source.ContactCalls);
        Assert.Equal(1, source.FormCalls);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task Warm_request_uses_repository_without_http_calls()
    {
        var source = new FakeSource { Personal = Personal };
        var repository = new FakeRepository();
        var service = new CustomerAggregationService(repository, source);
        var first = await service.GetAsync(Ssn, CancellationToken.None);

        var second = await service.GetAsync(Ssn, CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal(1, source.PersonalCalls);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task Cache_hit_from_previous_process_does_not_call_upstream()
    {
        var snapshot = new AggregatedKycData(Ssn, "Cached", "Name", "Address", null, null, null, null);
        var repository = new FakeRepository { Cached = snapshot };
        var source = new FakeSource();

        var result = await new CustomerAggregationService(repository, source).GetAsync(Ssn, CancellationToken.None);

        Assert.Same(snapshot, result);
        Assert.Equal(0, source.PersonalCalls);
    }

    [Fact]
    public async Task Missing_personal_details_are_not_cached_and_optional_endpoints_not_called()
    {
        var source = new FakeSource();
        var repository = new FakeRepository();
        var service = new CustomerAggregationService(repository, source);

        await Assert.ThrowsAsync<CustomerNotFoundException>(() => service.GetAsync(Ssn, CancellationToken.None));
        Assert.Equal(0, repository.SaveCalls);
        Assert.Equal(0, source.ContactCalls);
        Assert.Equal(0, source.FormCalls);
    }

    [Fact]
    public async Task Invalid_ssn_fails_before_reading_cache_or_http()
    {
        var source = new FakeSource();
        var repository = new FakeRepository();
        var service = new CustomerAggregationService(repository, source);

        await Assert.ThrowsAsync<InvalidSsnException>(() => service.GetAsync("invalid", CancellationToken.None));
        Assert.Equal(0, repository.FindCalls);
        Assert.Equal(0, source.PersonalCalls);
    }

    [Fact]
    public async Task Concurrent_requests_for_same_ssn_only_fetch_once()
    {
        var source = new FakeSource { Personal = Personal, ArtificialDelay = TimeSpan.FromMilliseconds(30) };
        var repository = new FakeRepository();
        var service = new CustomerAggregationService(repository, source);

        var results = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => service.GetAsync(Ssn, CancellationToken.None)));

        Assert.All(results, result => Assert.Equal("Anna", result.FirstName));
        Assert.Equal(1, source.PersonalCalls);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task Upstream_failure_does_not_save_partial_customer()
    {
        var source = new FakeSource { Personal = Personal, FailKyc = true };
        var repository = new FakeRepository();
        var service = new CustomerAggregationService(repository, source);

        await Assert.ThrowsAsync<UpstreamServiceException>(() => service.GetAsync(Ssn, CancellationToken.None));
        Assert.Equal(0, repository.SaveCalls);
    }

    private sealed class FakeRepository : ICustomerSnapshotRepository
    {
        private readonly object _sync = new();
        public AggregatedKycData? Cached { get; set; }
        public int SaveCalls { get; private set; }
        public int FindCalls { get; private set; }

        public Task<AggregatedKycData?> FindAsync(string ssn, CancellationToken cancellationToken)
        {
            lock (_sync) { FindCalls++; return Task.FromResult(Cached?.Ssn == ssn ? Cached : null); }
        }

        public Task SaveAsync(AggregatedKycData customer, CancellationToken cancellationToken)
        {
            lock (_sync) { Cached = customer; SaveCalls++; }
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSource : ICustomerDataSource
    {
        private int _personalCalls;
        private int _contactCalls;
        private int _formCalls;
        public PersonalDetails? Personal { get; init; }
        public ContactDetails? Contacts { get; init; }
        public KycForm? Form { get; init; }
        public bool FailKyc { get; init; }
        public TimeSpan ArtificialDelay { get; init; }
        public int PersonalCalls => _personalCalls;
        public int ContactCalls => _contactCalls;
        public int FormCalls => _formCalls;

        public async Task<PersonalDetails?> GetPersonalDetailsAsync(string ssn, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _personalCalls);
            if (ArtificialDelay > TimeSpan.Zero) await Task.Delay(ArtificialDelay, cancellationToken);
            return Personal;
        }

        public Task<ContactDetails?> GetContactDetailsAsync(string ssn, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _contactCalls);
            return Task.FromResult(Contacts);
        }

        public Task<KycForm?> GetKycFormAsync(string ssn, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _formCalls);
            return FailKyc ? Task.FromException<KycForm?>(new UpstreamServiceException("unavailable"))
                : Task.FromResult(Form);
        }
    }
}
