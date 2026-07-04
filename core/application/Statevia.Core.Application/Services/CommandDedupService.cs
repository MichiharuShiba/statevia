namespace Statevia.Core.Application.Services;

/// <summary><see cref="ICommandDedupService"/> 実装。</summary>
internal sealed class CommandDedupService : ICommandDedupService
{
    public CommandDedupKey? Create(string tenantKey, string? idempotencyKey, string method, string path, string? requestHash = null)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return null;

        var trimmed = (path ?? string.Empty).TrimEnd('/');
        var endpoint = $"{method} {trimmed}";
        var idempotencyKeyTrimmed = idempotencyKey.Trim();
        var dedupKey = string.IsNullOrWhiteSpace(requestHash)
            ? $"{tenantKey}|{endpoint}:{idempotencyKeyTrimmed}"
            : $"{tenantKey}|{endpoint}:{idempotencyKeyTrimmed}:{requestHash}";

        return new CommandDedupKey
        {
            DedupKey = dedupKey,
            Endpoint = endpoint,
            IdempotencyKey = idempotencyKeyTrimmed
        };
    }
}
