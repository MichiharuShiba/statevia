using Microsoft.EntityFrameworkCore;

using Statevia.Infrastructure.Persistence;

namespace Statevia.Service.Api.Services;

/// <summary>ログイン失敗ロックを共有表へ保存する。</summary>
/// <param name="dbContextFactory">CoreDbContext 工場。</param>
/// <param name="timeProvider">現在時刻。テストで固定する。</param>
/// <param name="idGenerator">失敗行の UUID。</param>
/// <param name="logger">RecordFailure / Reset 失敗時のログ。</param>
/// <remarks>
/// 件数上限と追い出しは持たない。窓外 attempts は書き込み時に DELETE する。
/// <see cref="IsLocked"/> の失敗は呼び出し側へ投げ、ログインを通さない。
/// </remarks>
internal sealed class LoginFailureLockStore(
    IDbContextFactory<CoreDbContext> dbContextFactory,
    TimeProvider timeProvider,
    IIdGenerator idGenerator,
    ILogger<LoginFailureLockStore> logger) : ILoginFailureLockStore
{
    /// <summary>ロックに必要な窓内失敗回数。</summary>
    internal const int MaxFailedAttempts = 5;

    /// <summary>失敗を数える窓。</summary>
    internal static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);

    /// <summary>閾値到達後のロック時間。</summary>
    internal static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    /// <inheritdoc />
    public bool IsLocked(string tenantKey, string username)
    {
        using var db = dbContextFactory.CreateDbContext();
        var lockRow = db.LoginFailureLocks.Find(tenantKey, username);
        if (lockRow?.LockedUntil is not { } lockedUntil)
            return false;

        return ToUtc(lockedUntil) > timeProvider.GetUtcNow().UtcDateTime;
    }

    /// <inheritdoc />
    public void RecordFailure(string tenantKey, string username)
    {
        try
        {
            RecordFailureCore(tenantKey, username);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.RecordFailureFailed(ex, tenantKey, username);
        }
    }

    /// <inheritdoc />
    public void Reset(string tenantKey, string username)
    {
        try
        {
            using var db = dbContextFactory.CreateDbContext();
            var lockRow = db.LoginFailureLocks.Find(tenantKey, username);
            if (lockRow is null)
                return;

            db.LoginFailureLocks.Remove(lockRow);
            db.SaveChanges();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.ResetFailed(ex, tenantKey, username);
        }
    }

    private void RecordFailureCore(string tenantKey, string username)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var windowStart = nowUtc - FailureWindow;

        using var db = dbContextFactory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction();
        var lockRow = EnsureLockRow(db, tenantKey, username);
        if (lockRow.LockedUntil is { } lockedUntil && ToUtc(lockedUntil) > nowUtc)
        {
            transaction.Commit();
            return;
        }

        db.LoginFailureAttempts
            .Where(attempt =>
                attempt.TenantKey == tenantKey
                && attempt.Username == username
                && attempt.FailedAt <= windowStart)
            .ExecuteDelete();

        db.LoginFailureAttempts.Add(new LoginFailureAttemptRow
        {
            AttemptId = idGenerator.NewSequentialGuid(),
            TenantKey = tenantKey,
            Username = username,
            FailedAt = nowUtc
        });
        db.SaveChanges();

        var windowCount = db.LoginFailureAttempts.Count(attempt =>
            attempt.TenantKey == tenantKey
            && attempt.Username == username
            && attempt.FailedAt > windowStart);
        if (windowCount >= MaxFailedAttempts)
            lockRow.LockedUntil = nowUtc + LockDuration;

        db.SaveChanges();
        transaction.Commit();
    }

    /// <summary>窓外の失敗と、ロック切れかつ窓内失敗なしの主体行を DELETE する。</summary>
    /// <remarks>ログイン経路からは呼ばない。HostedService の定期実行専用。</remarks>
    internal void PurgeExpired()
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var windowStart = nowUtc - FailureWindow;

        using var db = dbContextFactory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction();
        db.LoginFailureAttempts
            .Where(attempt => attempt.FailedAt <= windowStart)
            .ExecuteDelete();
        db.LoginFailureLocks
            .Where(lockRow =>
                (lockRow.LockedUntil == null || lockRow.LockedUntil < nowUtc)
                && !db.LoginFailureAttempts.Any(attempt =>
                    attempt.TenantKey == lockRow.TenantKey
                    && attempt.Username == lockRow.Username
                    && attempt.FailedAt > windowStart))
            .ExecuteDelete();
        transaction.Commit();
    }

    /// <summary>親行を点取得し、無ければ INSERT する。同時 INSERT は既存行を読み直す。</summary>
    private static LoginFailureLockRow EnsureLockRow(CoreDbContext db, string tenantKey, string username)
    {
        var existing = db.LoginFailureLocks.Find(tenantKey, username);
        if (existing is not null)
            return existing;

        var created = new LoginFailureLockRow
        {
            TenantKey = tenantKey,
            Username = username
        };
        db.LoginFailureLocks.Add(created);
        try
        {
            db.SaveChanges();
            return created;
        }
        catch (DbUpdateException)
        {
            db.Entry(created).State = EntityState.Detached;
            return db.LoginFailureLocks.Find(tenantKey, username)
                ?? throw new InvalidOperationException("Login failure lock row was not found after a concurrent insert.");
        }
    }

    private static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
