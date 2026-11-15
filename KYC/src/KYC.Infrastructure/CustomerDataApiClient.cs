using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KYC.Application;
using KYC.Domain;
using Microsoft.Extensions.Logging;

namespace KYC.Infrastructure;

/// <summary>
/// HTTP adapter for the Customer Data API. Converts HTTP errors to application
/// exceptions and obeys the shared outbound request gate for all three routes.
/// </summary>
public sealed class CustomerDataApiClient(
    HttpClient httpClient, OutboundRequestGate gate, ILogger<CustomerDataApiClient> logger)
    : ICustomerDataSource
{
    public Task<PersonalDetails?> GetPersonalDetailsAsync(string ssn, CancellationToken cancellationToken) =>
        GetAsync<PersonalDetails>($"personal-details/{Uri.EscapeDataString(ssn)}", cancellationToken);

    public Task<ContactDetails?> GetContactDetailsAsync(string ssn, CancellationToken cancellationToken) =>
        GetAsync<ContactDetails>($"contact-details/{Uri.EscapeDataString(ssn)}", cancellationToken);

    // Use the current /kyc-form/{ssn} route. The legacy date route is intentionally
    // not used, because it has no impact on the data and is for older clients.
    public Task<KycForm?> GetKycFormAsync(string ssn, CancellationToken cancellationToken) =>
        GetAsync<KycForm>($"kyc-form/{Uri.EscapeDataString(ssn)}", cancellationToken);

    /// <summary>
    /// Performs at most three attempts. A 404 means an absent dataset, 400 means
    /// bad SSN, and 429 delays according to Retry-After before retrying.
    /// Other failures become a safe service exception and are logged.
    /// </summary>
    private async Task<T?> GetAsync<T>(string relativePath, CancellationToken ct) where T : class
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            // The upstream quota applies per client IP across all endpoints.
            // Obtain a permit for each attempt, including retries.
            await gate.WaitForPermitAsync(ct);
            try
            {
                using var response = await httpClient.GetAsync(relativePath,
                    HttpCompletionOption.ResponseHeadersRead, ct);
                if (response.StatusCode == HttpStatusCode.NotFound) return null;
                if (response.StatusCode == HttpStatusCode.BadRequest) throw new InvalidSsnException();

                // Prefer the server-provided retry time over a hard-coded delay.
                // The request gate still applies before the next attempt.
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (attempt == 2) break;
                    var retryAfter = response.Headers.RetryAfter;
                    var delay = retryAfter?.Delta ??
                        (retryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : TimeSpan.FromSeconds(2));
                    if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
                    logger.LogWarning("Upstream rate limited a request; backing off for {Delay}.", delay);
                    await Task.Delay(delay, ct);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogError("Customer Data API returned HTTP {StatusCode}.", (int)response.StatusCode);
                    throw new UpstreamServiceException("Customer Data API is temporarily unavailable.");
                }

                // Deserialize only successful responses; unexpected/malformed JSON
                // must not be silently cached as an empty customer.
                var data = await response.Content.ReadFromJsonAsync<T>(
                    new JsonSerializerOptions(JsonSerializerDefaults.Web), ct);
                if (data is null) throw new JsonException("Upstream JSON response was empty.");
                return data;
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "Customer Data API network failure.");
                throw new UpstreamServiceException("Customer Data API is temporarily unavailable.", ex);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Customer Data API timeout.");
                throw new UpstreamServiceException("Customer Data API timed out.", ex);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Customer Data API returned malformed JSON.");
                throw new UpstreamServiceException("Customer Data API returned invalid data.", ex);
            }
        }

        logger.LogError("Customer Data API rate limit persisted after retries.");
        throw new UpstreamServiceException("Customer Data API rate limit exceeded. Try again later.");
    }
}
