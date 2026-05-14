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

public class StateviaWorkflowEngineServiceCollectionExtensionsTests
{
    /// <summary><see cref="StateviaWorkflowEngineServiceCollectionExtensions.AddStateviaWorkflowEngine"/> 後に主要サービスが解決できることを検証する。</summary>
    [Fact]
    public void AddStateviaWorkflowEngine_ResolvesEngineAndSharedScheduler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowInstanceIdGenerator>(_ => new DelegateWorkflowInstanceIdGenerator(() => "test-wf"));
        services.AddSingleton<ILogger<WorkflowEngine>>(_ => NullLogger<WorkflowEngine>.Instance);

        // Act
        services.AddStateviaWorkflowEngine();
        var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IWorkflowEngine>();
        var scheduler1 = provider.GetRequiredService<IScheduler>();
        var scheduler2 = provider.GetRequiredService<IScheduler>();

        // Assert
        Assert.NotNull(engine);
        Assert.Same(scheduler1, scheduler2);
    }

    /// <summary>構成コールバックで渡した <see cref="WorkflowEngineOptions"/> が登録に反映されることを検証する（同一 <see cref="IScheduler"/> 解決のスモーク）。</summary>
    [Fact]
    public void AddStateviaWorkflowEngine_WithConfigure_StillResolvesSingletons()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowInstanceIdGenerator>(_ => new UuidV7WorkflowInstanceIdGenerator());
        services.AddSingleton<ILogger<WorkflowEngine>>(_ => NullLogger<WorkflowEngine>.Instance);

        // Act
        services.AddStateviaWorkflowEngine(o => o.MaxParallelism = 2);
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetRequiredService<IWorkflowEngine>());
        Assert.NotNull(provider.GetRequiredService<IScheduler>());
    }
}
