using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Persistence.Repositories;

namespace Statevia.Core.Api.Services;

public readonly struct CommandDedupKey
{
    public string DedupKey { get; init; }
    public string Endpoint { get; init; }
}

public interface ICommandDedupService
{
    CommandDedupKey? Create(string tenantId, string? idempotencyKey, string method, string path);
}

public sealed class CommandDedupService : ICommandDedupService
{
    public CommandDedupKey? Create(string tenantId, string? idempotencyKey, string method, string path)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return null;

        var trimmed = (path ?? string.Empty).TrimEnd('/');
        var endpoint = $"{method} {trimmed}";
        var dedupKey = $"{tenantId}|{endpoint}:{idempotencyKey}";

        return new CommandDedupKey
        {
            DedupKey = dedupKey,
            Endpoint = endpoint
        };
    }
}

