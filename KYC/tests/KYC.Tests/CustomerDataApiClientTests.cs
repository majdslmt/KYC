using System.Net;
using KYC.Application;
using KYC.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace KYC.Tests;

public sealed class CustomerDataApiClientTests
{
    private const string Ssn = "19800115-1234";

    [Fact]
    public async Task Calls_new_kyc_route_and_deserializes_answers()
    {
        var handler = new StubHandler((request, _) =>
        {
            Assert.Equal($"/kyc-form/{Ssn}", request.RequestUri!.AbsolutePath);
            return Json("{\"items\":[{\"key\":\"tax_country\",\"value\":\"SE\"}]}");
        });
        var form = await MakeClient(handler).GetKycFormAsync(Ssn, CancellationToken.None);
        Assert.Equal("SE", form!.Items![0].Value);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Deserializes_personal_and_contact_details()
    {
        var handler = new StubHandler((request, _) => request.RequestUri!.AbsolutePath.StartsWith("/personal-details/")
            ? Json("{\"firstName\":\"Lars\",\"surName\":\"Larsson\",\"address\":\"Street\"}")
            : Json("{\"emails\":[{\"preferred\":true,\"emailAddress\":\"lars@example.com\"}],\"phoneNumbers\":[]}"));
        var client = MakeClient(handler);
        var personal = await client.GetPersonalDetailsAsync(Ssn, CancellationToken.None);
        var contact = await client.GetContactDetailsAsync(Ssn, CancellationToken.None);
        Assert.Equal("Larsson", personal!.SurName);
        Assert.Equal("lars@example.com", contact!.Emails![0].EmailAddress);
    }

    [Fact]
    public async Task Not_found_is_reported_as_missing_source()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound));
        Assert.Null(await MakeClient(handler).GetPersonalDetailsAsync(Ssn, CancellationToken.None));
    }

    [Fact]
    public async Task Upstream_bad_request_is_a_validation_error()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.BadRequest));
        await Assert.ThrowsAsync<InvalidSsnException>(() =>
            MakeClient(handler).GetPersonalDetailsAsync(Ssn, CancellationToken.None));
    }

    [Fact]
    public async Task Rate_limit_retries_then_returns_data()
    {
        var handler = new StubHandler((_, n) => n == 1
            ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero) }
            }
            : Json("{\"items\":[]}"));
        var result = await MakeClient(handler).GetKycFormAsync(Ssn, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task Repeated_rate_limits_become_service_error()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero) }
        });
        await Assert.ThrowsAsync<UpstreamServiceException>(() =>
            MakeClient(handler).GetKycFormAsync(Ssn, CancellationToken.None));
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task Invalid_json_becomes_service_error()
    {
        var handler = new StubHandler((_, _) => Json("{invalid"));
        await Assert.ThrowsAsync<UpstreamServiceException>(() =>
            MakeClient(handler).GetKycFormAsync(Ssn, CancellationToken.None));
    }

    [Fact]
    public async Task Upstream_server_error_becomes_service_error()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        await Assert.ThrowsAsync<UpstreamServiceException>(() =>
            MakeClient(handler).GetKycFormAsync(Ssn, CancellationToken.None));
    }

    private static CustomerDataApiClient MakeClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") },
            new OutboundRequestGate(), NullLogger<CustomerDataApiClient>.Instance);

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, int, HttpResponseMessage> handler) : HttpMessageHandler
    {
        private int _callCount;
        public int CallCount => _callCount;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var number = Interlocked.Increment(ref _callCount);
            return Task.FromResult(handler(request, number));
        }
    }
}
