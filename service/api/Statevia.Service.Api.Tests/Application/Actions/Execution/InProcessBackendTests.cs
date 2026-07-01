using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Service.Api.Application.Actions.Execution;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Execution;

namespace Statevia.Service.Api.Tests.Application.Actions.Execution;

/// <summary><see cref="InProcessBackend"/> の単体テスト。</summary>
public sealed class InProcessBackendTests
{
    private sealed class EchoState : IState<object?, object?>
    {
        public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) =>
            Task.FromResult(input);
    }

    private static ActionRegistration CreateRegistration(bool withFactory = true)
    {
        var descriptor = new ActionDescriptor
        {
            ActionId = "test.action",
            ModuleId = "test.module",
            Version = "1.0.0",
            Visibility = ActionVisibility.Builtin,
        };
        var entry = withFactory
            ? new ActionCatalogEntry(InProcessFactory: _ => DefaultStateExecutor.Create(new EchoState()))
            : new ActionCatalogEntry(InProcessFactory: null!);
        return new ActionRegistration(descriptor, entry);
    }

    private static ActionBackendInvocation CreateInvocation(
        ActionRegistration registration,
        StateContext stateContext,
        object? runtimeInput)
    {
        var request = new ActionExecutionRequest
        {
            ExecutionId = "exec-1",
            StateName = "A",
            ActionId = registration.Descriptor.ActionId,
            TenantId = ActionExecutionTestSupport.DefaultTenantId.ToString("D"),
        };
        return new ActionBackendInvocation(request, runtimeInput, registration, stateContext);
    }

    private static StateContext CreateStateContext() => new()
    {
        Events = null!,
        Store = null!,
        ExecutionId = "exec-1",
        StateName = "A",
    };

    /// <summary>Mode と ProviderKey は InProcess 契約を公開する。</summary>
    [Fact]
    public void Metadata_ExposesInProcessContract()
    {
        // Arrange
        using var provider = new ServiceCollection().BuildServiceProvider();
        var sut = new InProcessBackend(provider);

        // Act / Assert
        Assert.Equal(ActionExecutionMode.InProcess, sut.Mode);
        Assert.Equal("in-process", sut.ProviderKey);
    }

    /// <summary>InProcessFactory 経由で状態実行器を呼び出し、結果を RuntimeOutput に詰める。</summary>
    [Fact]
    public async Task ExecuteAsync_WithFactory_ReturnsExecutorOutput()
    {
        // Arrange
        using var provider = new ServiceCollection().BuildServiceProvider();
        var sut = new InProcessBackend(provider);
        var invocation = CreateInvocation(CreateRegistration(), CreateStateContext(), runtimeInput: 7);

        // Act
        var result = await sut.ExecuteAsync(invocation, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(7, result.RuntimeOutput);
    }

    /// <summary>InProcessFactory 未設定は InvalidOperationException。</summary>
    [Fact]
    public async Task ExecuteAsync_WithoutFactory_Throws()
    {
        // Arrange
        using var provider = new ServiceCollection().BuildServiceProvider();
        var sut = new InProcessBackend(provider);
        var invocation = CreateInvocation(
            CreateRegistration(withFactory: false),
            CreateStateContext(),
            runtimeInput: null);

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(invocation, CancellationToken.None));
    }

    /// <summary>登録情報が無い呼び出しは InvalidOperationException。</summary>
    [Fact]
    public async Task ExecuteAsync_WithoutRegistration_Throws()
    {
        // Arrange
        using var provider = new ServiceCollection().BuildServiceProvider();
        var sut = new InProcessBackend(provider);
        var request = new ActionExecutionRequest
        {
            ExecutionId = "exec-1",
            StateName = "A",
            ActionId = "test.action",
            TenantId = ActionExecutionTestSupport.DefaultTenantId.ToString("D"),
        };
        var invocation = new ActionBackendInvocation(request, RuntimeInput: null);

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(invocation, CancellationToken.None));
    }

    /// <summary>状態コンテキストが無い呼び出しは InvalidOperationException。</summary>
    [Fact]
    public async Task ExecuteAsync_WithoutStateContext_Throws()
    {
        // Arrange
        using var provider = new ServiceCollection().BuildServiceProvider();
        var sut = new InProcessBackend(provider);
        var registration = CreateRegistration();
        var request = new ActionExecutionRequest
        {
            ExecutionId = "exec-1",
            StateName = "A",
            ActionId = registration.Descriptor.ActionId,
            TenantId = ActionExecutionTestSupport.DefaultTenantId.ToString("D"),
        };
        var invocation = new ActionBackendInvocation(request, RuntimeInput: null, registration);

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(invocation, CancellationToken.None));
    }
}
