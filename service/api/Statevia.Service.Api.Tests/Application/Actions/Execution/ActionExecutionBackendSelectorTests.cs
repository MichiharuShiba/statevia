using Microsoft.Extensions.Options;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Service.Api.Application.Actions.Execution;

namespace Statevia.Service.Api.Tests.Application.Actions.Execution;

/// <summary><see cref="ActionExecutionBackendSelector"/> の単体テスト。</summary>
public sealed class ActionExecutionBackendSelectorTests
{
    private sealed class FakeBackend : IActionExecutionBackend
    {
        public FakeBackend(ActionExecutionMode mode, string providerKey)
        {
            Mode = mode;
            ProviderKey = providerKey;
        }

        public ActionExecutionMode Mode { get; }

        public string ProviderKey { get; }

        public Task<ActionExecutionResult> ExecuteAsync(
            ActionBackendInvocation invocation,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ActionExecutionResult { Success = true });
    }

    private static ActionExecutionContext Context() =>
        new(ActionExecutionTestSupport.DefaultTenantId.ToString("D"), "Development", DeploymentProfile: null);

    private static ActionExecutionBackendSelector CreateSut(
        IEnumerable<IActionExecutionBackend> backends,
        ExecutionPolicyOptions? options = null) =>
        new(backends, Options.Create(options ?? new ExecutionPolicyOptions()));

    /// <summary>Mode に単一登録なら自動選択する。</summary>
    [Fact]
    public void TryResolve_SingleBackendForMode_Resolves()
    {
        // Arrange
        var backend = new FakeBackend(ActionExecutionMode.InProcess, "in-process");
        var sut = CreateSut([backend]);

        // Act
        var resolved = sut.TryResolve(ActionExecutionMode.InProcess, Context(), out var selected);

        // Assert
        Assert.True(resolved);
        Assert.Same(backend, selected);
    }

    /// <summary>Backend 未登録の Mode は解決失敗。</summary>
    [Fact]
    public void TryResolve_NoBackendForMode_ReturnsFalse()
    {
        // Arrange
        var sut = CreateSut([new FakeBackend(ActionExecutionMode.InProcess, "in-process")]);

        // Act
        var resolved = sut.TryResolve(ActionExecutionMode.Container, Context(), out var selected);

        // Assert
        Assert.False(resolved);
        Assert.Null(selected);
    }

    /// <summary>同一 Mode に複数登録で ProviderKey 未指定なら fail-safe で解決失敗。</summary>
    [Fact]
    public void TryResolve_MultipleBackendsWithoutProviderKey_ReturnsFalse()
    {
        // Arrange
        var sut = CreateSut(
        [
            new FakeBackend(ActionExecutionMode.Container, "docker"),
            new FakeBackend(ActionExecutionMode.Container, "k8s"),
        ]);

        // Act
        var resolved = sut.TryResolve(ActionExecutionMode.Container, Context(), out var selected);

        // Assert
        Assert.False(resolved);
        Assert.Null(selected);
    }

    /// <summary>同一 Mode に複数登録でも ProviderKey 指定で該当実装を選択する。</summary>
    [Fact]
    public void TryResolve_MultipleBackendsWithProviderKey_ResolvesMatching()
    {
        // Arrange
        var docker = new FakeBackend(ActionExecutionMode.Container, "docker");
        var k8s = new FakeBackend(ActionExecutionMode.Container, "k8s");
        var options = new ExecutionPolicyOptions();
        options.Backends[ActionExecutionMode.Container.ToString()] = "k8s";
        var sut = CreateSut([docker, k8s], options);

        // Act
        var resolved = sut.TryResolve(ActionExecutionMode.Container, Context(), out var selected);

        // Assert
        Assert.True(resolved);
        Assert.Same(k8s, selected);
    }

    /// <summary>ProviderKey 指定が該当なしなら解決失敗。</summary>
    [Fact]
    public void TryResolve_ProviderKeyNotFound_ReturnsFalse()
    {
        // Arrange
        var options = new ExecutionPolicyOptions();
        options.Backends[ActionExecutionMode.Container.ToString()] = "firecracker";
        var sut = CreateSut(
            [
                new FakeBackend(ActionExecutionMode.Container, "docker"),
                new FakeBackend(ActionExecutionMode.Container, "k8s"),
            ],
            options);

        // Act
        var resolved = sut.TryResolve(ActionExecutionMode.Container, Context(), out var selected);

        // Assert
        Assert.False(resolved);
        Assert.Null(selected);
    }
}
