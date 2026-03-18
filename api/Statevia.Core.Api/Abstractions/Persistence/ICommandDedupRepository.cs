using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Abstractions.Persistence;

public interface ICommandDedupRepository
{
    Task<CommandDedupRow?> FindValidAsync(string dedupKey, DateTime utcNow, CancellationToken ct);
    Task SaveAsync(CommandDedupRow row, CancellationToken ct);
}
