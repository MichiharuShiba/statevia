using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Persistence;

namespace Statevia.Service.Api.Services;

/// <summary>表示用 ID の実装。62 文字種・10 桁・衝突時再生成（U3）。</summary>
internal sealed class DisplayIdServiceImpl : IDisplayIdService, IDisplayIdWriteService
{
    private static readonly char[] Chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
    private const int Length = 10;
    private readonly ICoreTransactionExecutor _executor;

    public DisplayIdServiceImpl(ICoreTransactionExecutor executor) => _executor = executor;

    /// <inheritdoc />
    public async Task<string> AllocateAsync(ICoreUnitOfWork uow, string kind, Guid uuid, CancellationToken ct = default)
    {
        const int maxAttempts = 10;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var displayId = GenerateDisplayId();
            var exists = await uow.GetDb().DisplayIds.AnyAsync(x => x.DisplayId == displayId, ct).ConfigureAwait(false);
            if (exists)
                continue;

            uow.GetDb().DisplayIds.Add(new DisplayIdRow
            {
                Kind = kind,
                DisplayId = displayId,
                ResourceId = uuid,
                CreatedAt = DateTime.UtcNow
            });

            return displayId;
        }

        throw new InvalidOperationException($"Failed to allocate display_id for {kind} after {maxAttempts} attempts.");
    }

    /// <inheritdoc />
    public Task<Guid?> ResolveAsync(string kind, string idOrUuid, CancellationToken ct = default) =>
        _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) => await ResolveCoreAsync(uow, kind, idOrUuid, innerCt).ConfigureAwait(false),
            ct);

    /// <inheritdoc />
    public Task<string?> GetDisplayIdAsync(string kind, string idOrUuid, CancellationToken ct = default) =>
        _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) => await GetDisplayIdCoreAsync(uow, kind, idOrUuid, innerCt).ConfigureAwait(false),
            ct);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<Guid, string>> GetDisplayIdsAsync(
        string kind,
        IEnumerable<Guid> resourceIds,
        CancellationToken ct = default) =>
        _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) => await GetDisplayIdsCoreAsync(uow, kind, resourceIds, innerCt).ConfigureAwait(false),
            ct);

    private static async Task<Guid?> ResolveCoreAsync(
        ICoreUnitOfWork uow,
        string kind,
        string idOrUuid,
        CancellationToken ct)
    {
        if (Guid.TryParse(idOrUuid, out var guid))
        {
            var byDisplay = await uow.GetDb().DisplayIds.FirstOrDefaultAsync(x => x.Kind == kind && x.ResourceId == guid, ct)
                .ConfigureAwait(false);
            if (byDisplay is not null) return guid;
            if (kind is "definition" && await uow.GetDb().Definitions.AnyAsync(x => x.DefinitionId == guid, ct).ConfigureAwait(false))
                return guid;
            if (kind is "execution" && await uow.GetDb().Executions.AnyAsync(x => x.ExecutionId == guid, ct).ConfigureAwait(false))
                return guid;
            return null;
        }

        var row = await uow.GetDb().DisplayIds.FirstOrDefaultAsync(x => x.Kind == kind && x.DisplayId == idOrUuid, ct)
            .ConfigureAwait(false);
        return row?.ResourceId;
    }

    private static async Task<string?> GetDisplayIdCoreAsync(
        ICoreUnitOfWork uow,
        string kind,
        string idOrUuid,
        CancellationToken ct)
    {
        if (!Guid.TryParse(idOrUuid, out var guid))
            return idOrUuid;

        return await uow.GetDb().DisplayIds.AsNoTracking()
            .Where(x => x.Kind == kind && x.ResourceId == guid)
            .Select(x => x.DisplayId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    private static async Task<IReadOnlyDictionary<Guid, string>> GetDisplayIdsCoreAsync(
        ICoreUnitOfWork uow,
        string kind,
        IEnumerable<Guid> resourceIds,
        CancellationToken ct)
    {
        var ids = resourceIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        var pairs = await uow.GetDb().DisplayIds.AsNoTracking()
            .Where(x => x.Kind == kind && ids.Contains(x.ResourceId))
            .Select(x => new { x.ResourceId, x.DisplayId })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return pairs.ToDictionary(x => x.ResourceId, x => x.DisplayId);
    }

    private static string GenerateDisplayId()
    {
        var bytes = new byte[Length];
        RandomNumberGenerator.Fill(bytes);
        var result = new char[Length];
        for (var i = 0; i < Length; i++)
            result[i] = Chars[bytes[i] % Chars.Length];
        return new string(result);
    }
}
