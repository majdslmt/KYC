namespace KYC.Application;

/// <summary>Input fails the SSN format or date validation rules.</summary>
public sealed class InvalidSsnException : Exception
{
    public InvalidSsnException() : base("SSN must use the format yyyyMMdd-xxxx and contain a valid birth date.") { }
}

/// <summary>The authoritative personal-details lookup returned no customer.</summary>
public sealed class CustomerNotFoundException : Exception
{
    public CustomerNotFoundException() : base("Customer data not found for the provided SSN.") { }
}

/// <summary>Upstream returned a customer without required fields.</summary>
public sealed class InvalidUpstreamDataException : Exception
{
    public InvalidUpstreamDataException() : base("Required customer details are missing from the upstream response.") { }
}

/// <summary>An upstream transport, throttling, or response failure prevented retrieval.</summary>
public sealed class UpstreamServiceException : Exception
{
    public UpstreamServiceException(string message) : base(message) { }
    public UpstreamServiceException(string message, Exception innerException) : base(message, innerException) { }
}
