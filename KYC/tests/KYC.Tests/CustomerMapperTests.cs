using KYC.Application;
using KYC.Domain;

namespace KYC.Tests;

public sealed class CustomerMapperTests
{
    private static readonly PersonalDetails Personal = new(" Anna ", " Andersson ", " Storgatan 1 ");

    [Fact]
    public void Maps_personal_contact_and_kyc_values()
    {
        var contacts = new ContactDetails([new(true, "a@example.com")], [new(true, "+46701234567")]);
        var form = new KycForm([new("annual_income", " 550000 "), new("tax_country", " SE ")]);

        var result = CustomerMapper.Map("19800115-1234", Personal, contacts, form);

        Assert.Equal("19800115-1234", result.Ssn);
        Assert.Equal("Anna", result.FirstName);
        Assert.Equal("Andersson", result.LastName);
        Assert.Equal("Storgatan 1", result.Address);
        Assert.Equal("a@example.com", result.Email);
        Assert.Equal("+46701234567", result.PhoneNumber);
        Assert.Equal("SE", result.TaxCountry);
        Assert.Equal(550000, result.Income);
    }

    [Fact]
    public void Missing_optional_sources_return_null_fields()
    {
        var result = CustomerMapper.Map("19800115-1234", Personal, null, null);
        Assert.Null(result.Email);
        Assert.Null(result.PhoneNumber);
        Assert.Null(result.TaxCountry);
        Assert.Null(result.Income);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("99999999999999999999999999")]
    [InlineData("-1")]
    [InlineData("")]
    public void Invalid_or_negative_income_is_null(string value)
    {
        var result = CustomerMapper.Map("19800115-1234", Personal, null,
            new KycForm([new("annual_income", value)]));
        Assert.Null(result.Income);
    }

    [Fact]
    public void Kyc_keys_are_case_insensitive_and_blank_values_ignored()
    {
        var form = new KycForm([new("TAX_COUNTRY", " "), new("Tax_Country", "DK")]);
        var result = CustomerMapper.Map("19800115-1234", Personal, null, form);
        Assert.Equal("DK", result.TaxCountry);
    }

    [Theory]
    [InlineData(null, "Last", "Address")]
    [InlineData("First", "", "Address")]
    [InlineData("First", "Last", " ")]
    public void Missing_required_personal_data_is_an_error(string? first, string? last, string? address) =>
        Assert.Throws<InvalidUpstreamDataException>(() =>
            CustomerMapper.Map("19800115-1234", new PersonalDetails(first, last, address), null, null));

    [Fact]
    public void GetAnswer_returns_null_for_missing_items_and_missing_keys()
    {
        Assert.Null(CustomerMapper.GetAnswer(null, "tax_country"));
        Assert.Null(CustomerMapper.GetAnswer(new KycForm(null), "tax_country"));
        Assert.Null(CustomerMapper.GetAnswer(new KycForm([new("occupation", "Engineer")]), "tax_country"));
    }
}
