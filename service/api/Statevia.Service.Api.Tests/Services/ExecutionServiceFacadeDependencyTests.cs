using Statevia.Core.Application.Services;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>
/// <see cref="ExecutionService"/> Facade が Repository 等を直接 DI せず、ドメインサービスのみに依存することを固定する。
/// </summary>
public sealed class ExecutionServiceFacadeDependencyTests
{
    /// <summary>コンストラクタ引数はドメインサービス型のみ。</summary>
    [Fact]
    public void Constructor_DependsOnlyOnDomainServices()
    {
        // Arrange
        var constructor = typeof(ExecutionService).GetConstructors().Single();
        var parameterTypes = constructor.GetParameters().Select(p => p.ParameterType).ToArray();

        // Act / Assert
        Assert.Equal(
            [
                typeof(ExecutionQueryService),
                typeof(ExecutionProjectionOrchestrator),
                typeof(ExecutionLifecycleCommandService),
                typeof(ExecutionWaitEventService),
                typeof(ExecutionCheckpointService),
                typeof(ExecutionOwnershipService),
                typeof(ExecutionRecoveryService)
            ],
            parameterTypes);
    }
}
