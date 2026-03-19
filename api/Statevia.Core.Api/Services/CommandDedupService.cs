using Statevia.Core.Api.Abstractions.Services;

namespace Statevia.Core.Api.Services;

public sealed class CommandDedupService : ICommandDedupService
{
    public CommandDedupKey? Create(string tenantId, string? idempotencyKey, string method, string path)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return null;

        var trimmed = (path ?? string.Empty).TrimEnd('/');
        var endpoint = $"{method} {trimmed}";
        var idempotencyKeyTrimmed = idempotencyKey.Trim();
        var dedupKey = $"{tenantId}|{endpoint}:{idempotencyKeyTrimmed}";

        return new CommandDedupKey
        {
            DedupKey = dedupKey,
            Endpoint = endpoint,
            IdempotencyKey = idempotencyKeyTrimmed
        };
    }
}
