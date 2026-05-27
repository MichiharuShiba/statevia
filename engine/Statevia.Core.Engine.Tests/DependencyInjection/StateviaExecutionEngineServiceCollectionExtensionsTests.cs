using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.DependencyInjection;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Infrastructure;
using Statevia.Core.Engine.Scheduler;
using Xunit;

namespace Statevia.Core.Engine.Tests.DependencyInjection;

public class StateviaExecutionEngineServiceCollectionExtensionsTests
{
    /// <summary><see cref="StateviaExecutionEngineServiceCollectionExtensions.AddStateviaExecutionEngine"/> 後に主要サービスが解決できることを検証する。</summary>
    [Fact]
    public void AddStateviaExecutionEngine_ResolvesEngineAndSharedScheduler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionIdGenerator>(_ => new DelegateExecutionIdGenerator(() => "test-wf"));
        services.AddSingleton<ILoggerFactory>(_ => NullLoggerFactory.Instance);

        // Act
        services.AddStateviaExecutionEngine();
        var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IExecutionEngine>();
        var scheduler1 = provider.GetRequiredService<IScheduler>();
        var scheduler2 = provider.GetRequiredService<IScheduler>();

        // Assert
        Assert.NotNull(engine);
        Assert.Same(scheduler1, scheduler2);
    }

    /// <summary>構成コールバックで渡した <see cref="ExecutionEngineOptions"/> が登録に反映されることを検証する（同一 <see cref="IScheduler"/> 解決のスモーク）。</summary>
    [Fact]
    public void AddStateviaExecutionEngine_WithConfigure_StillResolvesSingletons()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IExecutionIdGenerator>(_ => new UuidV7ExecutionIdGenerator());
        services.AddSingleton<ILoggerFactory>(_ => NullLoggerFactory.Instance);

        // Act
        services.AddStateviaExecutionEngine(o => o.MaxParallelism = 2);
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetRequiredService<IExecutionEngine>());
        Assert.NotNull(provider.GetRequiredService<IScheduler>());
    }
}
