namespace KYC.Domain;

// These records represent upstream payloads. System.Text.Json's web defaults
// bind camelCase response fields to the matching C# properties.
public sealed record PersonalDetails(string? FirstName, string? SurName, string? Address);

public sealed record EmailContact(bool Preferred, string? EmailAddress);
public sealed record PhoneContact(bool Preferred, string? Number);
public sealed record ContactDetails(
    IReadOnlyList<EmailContact>? Emails,
    IReadOnlyList<PhoneContact>? PhoneNumbers);

// KYC form data uses a flexible array of key/value answers, not fixed fields.
public sealed record KycAnswer(string? Key, string? Value);
public sealed record KycForm(IReadOnlyList<KycAnswer>? Items);

/// <summary>
/// The public response and stored snapshot. Required identity fields cannot be
/// absent after mapping; contact details, tax country, and income may be null.
/// The API configures snake_case JSON naming for the contract.
/// </summary>
public sealed record AggregatedKycData(
    string Ssn,
    string FirstName,
    string LastName,
    string Address,
    string? PhoneNumber,
    string? Email,
    string? TaxCountry,
    long? Income);
