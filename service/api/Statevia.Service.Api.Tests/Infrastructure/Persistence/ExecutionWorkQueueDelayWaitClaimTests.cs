using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.DependencyInjection;

namespace Statevia.Service.Api.Tests.Infrastructure.Persistence;

/// <summary>
/// DelayWait 排他 claim+enqueue の PostgreSQL スモーク（<c>STATEVIA_POSTGRES_SMOKE=1</c> 時のみ実行）。
/// </summary>
public sealed class ExecutionWorkQueueDelayWaitClaimTests
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=statevia;Username=statevia;Password=statevia";

    private static bool IsSmokeEnabled =>
        string.Equals(
            Environment.GetEnvironmentVariable("STATEVIA_POSTGRES_SMOKE"),
            "1",
            StringComparison.Ordinal);

    /// <summary>同一期限切れ DelayWait を並行 claim しても Resume は 1 件だけ投入される。</summary>
    [Fact]
    public async Task EnqueueExpiredDelayWaitResumesAsync_ConcurrentClaims_EnqueuesOnce()
    {
        if (!IsSmokeEnabled)
            return;

        // Arrange
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? DefaultConnectionString;
        var executionId = Guid.NewGuid();
        const string nodeId = "delay-1";
        var now = DateTime.UtcNow;

        var services = new ServiceCollection();
        services.AddSingleton<ITenantQueryFilterOptions>(DisabledTenantQueryFilterOptions.Instance);
        services.AddSingleton<ITenantContextAccessor>(NullTenantContextAccessor.Instance);
        services.AddStateviaInfrastructurePersistence(connectionString);
        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CoreDbContext>>();
            await using var db = await factory.CreateDbContextAsync();
            db.ExecutionWaits.Add(new ExecutionWaitRow
            {
                ExecutionId = executionId,
                NodeId = nodeId,
                WaitKind = ExecutionWaitKind.DelayWait,
                AllowedEvents = [ExecutionWaitEventNames.DelayCompleted],
                ExpiresAt = now.AddSeconds(-1),
                CreatedAt = now.AddMinutes(-1)
            });
            await db.SaveChangesAsync();
        }

        var queue = provider.CreateScope().ServiceProvider.GetRequiredService<IExecutionWorkQueue>();

        // Act
        var results = await Task.WhenAll(
            queue.EnqueueExpiredDelayWaitResumesAsync(now, 64, CancellationToken.None),
            queue.EnqueueExpiredDelayWaitResumesAsync(now, 64, CancellationToken.None),
            queue.EnqueueExpiredDelayWaitResumesAsync(now, 64, CancellationToken.None));

        // Assert
        Assert.Equal(1, results.Sum());

        await using (var scope = provider.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CoreDbContext>>();
            await using var db = await factory.CreateDbContextAsync();
            var remainingWaits = await db.ExecutionWaits
                .AsNoTracking()
                .CountAsync(wait => wait.ExecutionId == executionId);
            var resumeItems = await db.ExecutionWorkItems
                .AsNoTracking()
                .Where(item => item.ExecutionId == executionId && item.Kind == ExecutionWorkItemKinds.Resume)
                .ToListAsync();

            Assert.Equal(0, remainingWaits);
            Assert.Single(resumeItems);
            Assert.Contains(ExecutionWaitEventNames.DelayCompleted, resumeItems[0].Payload, StringComparison.Ordinal);
            Assert.Contains("\"mode\":\"event\"", resumeItems[0].Payload, StringComparison.Ordinal);
        }
    }
}
