namespace WorkOps.Application.Abstractions;

public interface ICorrelationContext
{
    string CorrelationId { get; }
}
