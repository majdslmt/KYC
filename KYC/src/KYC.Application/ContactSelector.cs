using KYC.Domain;

namespace KYC.Application;

/// <summary>
/// Chooses one email and one phone from the potentially multi-valued contact API.
/// Both selection methods apply the same business rule for consistency.
/// </summary>
public static class ContactSelector
{
    /// <summary>Returns a nonempty preferred email, otherwise the first nonempty email.</summary>
    public static string? SelectEmail(ContactDetails? details) =>
        Select(details?.Emails, x => x.Preferred, x => x.EmailAddress);

    /// <summary>Returns a nonempty preferred phone, otherwise the first nonempty phone.</summary>
    public static string? SelectPhone(ContactDetails? details) =>
        Select(details?.PhoneNumbers, x => x.Preferred, x => x.Number);

    /// <summary>
    /// Ignore blank values even when marked preferred. If no usable value exists,
    /// return null rather than returning an empty or whitespace-only contact.
    /// Preserve source order when more than one entry is eligible.
    /// </summary>
    private static string? Select<T>(IReadOnlyList<T>? entries,
        Func<T, bool> preferred, Func<T, string?> value) where T : class
    {
        if (entries is null) return null;
        var selected = entries.FirstOrDefault(x => preferred(x) && !string.IsNullOrWhiteSpace(value(x)));
        selected ??= entries.FirstOrDefault(x => !string.IsNullOrWhiteSpace(value(x)));
        return selected is null ? null : value(selected)?.Trim();
    }
}
