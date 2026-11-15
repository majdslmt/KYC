using System.Globalization;
using KYC.Domain;

namespace KYC.Application;

/// <summary>
/// Translates upstream API payloads to the public aggregation contract.
/// Kept independent of HTTP and EF Core so mapping rules can be unit-tested.
/// </summary>
public static class CustomerMapper
{
    /// <summary>
    /// Personal details are mandatory for an aggregated customer. Contact and KYC
    /// form responses are optional, so absent values are represented as null.
    /// Annual income is returned only for a nonnegative, valid integer value.
    /// </summary>
    public static AggregatedKycData Map(string ssn, PersonalDetails personal,
        ContactDetails? contacts, KycForm? form)
    {
        if (string.IsNullOrWhiteSpace(personal.FirstName) ||
            string.IsNullOrWhiteSpace(personal.SurName) ||
            string.IsNullOrWhiteSpace(personal.Address))
            throw new InvalidUpstreamDataException();

        // KYC answers arrive as key/value items, not strongly typed JSON properties.
        // tax_country must come from the KYC form; never infer it from address.
        var taxCountry = GetAnswer(form, "tax_country");
        var incomeValue = GetAnswer(form, "annual_income");
        long? income = long.TryParse(incomeValue, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var parsed) && parsed >= 0 ? parsed : null;

        return new AggregatedKycData(ssn, personal.FirstName.Trim(), personal.SurName.Trim(),
            personal.Address.Trim(), ContactSelector.SelectPhone(contacts),
            ContactSelector.SelectEmail(contacts), taxCountry, income);
    }

    /// <summary>
    /// Finds the first nonblank answer for a key without depending on its case.
    /// Returns null when the form, items, or requested answer is missing.
    /// </summary>
    public static string? GetAnswer(KycForm? form, string key) =>
        form?.Items?.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(x.Value))?.Value?.Trim();
}
