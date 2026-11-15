using KYC.Application;
using KYC.Domain;

namespace KYC.Tests;

public sealed class ContactSelectorTests
{
    [Fact]
    public void Preferred_email_wins_even_if_listed_second()
    {
        var data = new ContactDetails(
            [new(false, "other@example.com"), new(true, " preferred@example.com ")], null);
        Assert.Equal("preferred@example.com", ContactSelector.SelectEmail(data));
    }

    [Fact]
    public void Ignores_blank_preferred_email_and_falls_back_to_first_usable()
    {
        var data = new ContactDetails([new(true, " "), new(false, "ok@example.com")], null);
        Assert.Equal("ok@example.com", ContactSelector.SelectEmail(data));
    }

    [Fact]
    public void Preferred_phone_wins()
    {
        var data = new ContactDetails(null,
            [new(false, "001"), new(true, " 002 ")]);
        Assert.Equal("002", ContactSelector.SelectPhone(data));
    }

    [Fact]
    public void Phone_falls_back_to_first_nonblank()
    {
        var data = new ContactDetails(null,
            [new(false, ""), new(false, " 123 "), new(false, "456")]);
        Assert.Equal("123", ContactSelector.SelectPhone(data));
    }

    [Fact]
    public void Missing_or_blank_details_return_null()
    {
        Assert.Null(ContactSelector.SelectEmail(null));
        Assert.Null(ContactSelector.SelectPhone(new ContactDetails(null, [new(true, " ")])));
    }
}
