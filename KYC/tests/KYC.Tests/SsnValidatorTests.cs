using KYC.Application;

namespace KYC.Tests;

public sealed class SsnValidatorTests
{
    [Theory]
    [InlineData("19800115-1234")]
    [InlineData("20000229-0000")]
    [InlineData("19751230-9101")]
    public void Accepts_valid_dates_and_format(string ssn) => Assert.True(SsnValidator.IsValid(ssn));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("19800115-123")]
    [InlineData("198001151234")]
    [InlineData("19801315-1234")]
    [InlineData("20010229-1234")]
    [InlineData("abcdefgh-1234")]
    public void Rejects_invalid_ssn(string? ssn) => Assert.False(SsnValidator.IsValid(ssn));
}
