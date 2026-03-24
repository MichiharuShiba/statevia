namespace Statevia.Core.Api.Abstractions.Services;

public readonly struct CommandDedupKey
{
    public string DedupKey { get; init; }
    public string Endpoint { get; init; }
    public string IdempotencyKey { get; init; }
}

public interface ICommandDedupService
{
    CommandDedupKey? Create(string tenantId, string? idempotencyKey, string method, string path, string? requestHash = null);
}
