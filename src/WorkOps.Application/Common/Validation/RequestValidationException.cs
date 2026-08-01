namespace WorkOps.Application.Common.Validation;

public sealed class RequestValidationException(string code) : Exception
{
    public string Code { get; } = code;
}
