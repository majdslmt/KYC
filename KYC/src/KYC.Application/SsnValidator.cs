using System.Globalization;
using System.Text.RegularExpressions;

namespace KYC.Application;

/// <summary>
/// Performs lightweight format and birth-date validation before any remote API calls.
/// This validates the test API's yyyyMMdd-xxxx format; it is not a full Swedish
/// personal identity number checksum or identity verification process.
/// </summary>
public static partial class SsnValidator
{
    [GeneratedRegex(@"^\d{8}-\d{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatRegex();

    /// <summary>
    /// Accepts exactly eight date digits, a hyphen, and four trailing digits.
    /// Rejects impossible calendar dates such as February 30.
    /// </summary>
    public static bool IsValid(string? ssn) =>
        ssn is not null && FormatRegex().IsMatch(ssn) &&
        DateOnly.TryParseExact(ssn[..8], "yyyyMMdd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _);
}
