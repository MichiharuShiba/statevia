using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Services;

/// <summary>表示用 ID の実装。62 文字種・10 桁・衝突時再生成（U3）。</summary>
public sealed class DisplayIdServiceImpl : IDisplayIdService
{
    private static readonly char[] Chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
    private const int Length = 10;
    private readonly IDbContextFactory<CoreDbContext> _dbFactory;

    public DisplayIdServiceImpl(IDbContextFactory<CoreDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<string> AllocateAsync(string kind, Guid uuid, CancellationToken ct = default)
    {
        const int maxAttempts = 10;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var displayId = GenerateDisplayId();
            await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            var exists = await db.DisplayIds.AnyAsync(x => x.DisplayId == displayId, ct).ConfigureAwait(false);
            if (exists)
                continue;
            db.DisplayIds.Add(new DisplayIdRow
            {
                Kind = kind,
                DisplayId = displayId,
                ResourceId = uuid,
                CreatedAt = DateTime.UtcNow
            });
            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                return displayId;
            }
            catch (DbUpdateException)
            {
                // キー違反で衝突 → 再生成
            }
        }
        throw new InvalidOperationException($"Failed to allocate display_id for {kind} after {maxAttempts} attempts.");
    }

    public async Task<Guid?> ResolveAsync(string kind, string idOrUuid, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        if (Guid.TryParse(idOrUuid, out var guid))
        {
            var byDisplay = await db.DisplayIds.FirstOrDefaultAsync(x => x.Kind == kind && x.ResourceId == guid, ct).ConfigureAwait(false);
            if (byDisplay != null) return guid;
            if (kind == "definition" && await db.WorkflowDefinitions.AnyAsync(x => x.DefinitionId == guid, ct).ConfigureAwait(false)) return guid;
            if (kind == "workflow" && await db.Workflows.AnyAsync(x => x.WorkflowId == guid, ct).ConfigureAwait(false)) return guid;
            return null;
        }
        var row = await db.DisplayIds.FirstOrDefaultAsync(x => x.Kind == kind && x.DisplayId == idOrUuid, ct).ConfigureAwait(false);
        return row?.ResourceId;
    }

    public async Task<string?> GetDisplayIdAsync(string kind, string idOrUuid, CancellationToken ct = default)
    {
        if (!Guid.TryParse(idOrUuid, out var guid))
            return idOrUuid;

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var displayId = await db.DisplayIds.AsNoTracking()
            .Where(x => x.Kind == kind && x.ResourceId == guid)
            .Select(x => x.DisplayId)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return displayId;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayIdsAsync(string kind, IEnumerable<Guid> resourceIds, CancellationToken ct = default)
    {
        var ids = resourceIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var pairs = await db.DisplayIds.AsNoTracking()
            .Where(x => x.Kind == kind && ids.Contains(x.ResourceId))
            .Select(x => new { x.ResourceId, x.DisplayId })
            .ToListAsync(ct).ConfigureAwait(false);
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
