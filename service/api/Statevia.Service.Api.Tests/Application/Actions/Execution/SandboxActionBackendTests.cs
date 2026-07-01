using Microsoft.Extensions.Options;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Service.Api.Application.Actions.Execution;

namespace Statevia.Service.Api.Tests.Application.Actions.Execution;

/// <summary>サンドボックス系 Backend（<see cref="ContainerActionBackend"/> / <see cref="WasmActionBackend"/>）の単体テスト。</summary>
public sealed class SandboxActionBackendTests
{
    /// <summary>Container Backend は Container 契約を公開する。</summary>
    [Fact]
    public void ContainerMetadata_ExposesContainerContract()
    {
        // Arrange
        var sut = new ContainerActionBackend([], CreateOptions(new SandboxOptions()));

        // Act / Assert
        Assert.Equal(ActionExecutionMode.Container, sut.Mode);
        Assert.Equal("container", sut.ProviderKey);
    }

    /// <summary>Wasm Backend は Wasm 契約を公開する。</summary>
    [Fact]
    public void WasmMetadata_ExposesWasmContract()
    {
        // Arrange
        var sut = new WasmActionBackend([], CreateOptions(new SandboxOptions()));

        // Act / Assert
        Assert.Equal(ActionExecutionMode.Wasm, sut.Mode);
        Assert.Equal("wasm", sut.ProviderKey);
    }

    /// <summary>Provider 未指定（既定）の Container 実行は SandboxRuntimeNotConfigured で失敗する。</summary>
    [Fact]
    public async Task Container_WhenProviderNotConfigured_ReturnsSandboxRuntimeNotConfigured()
    {
        // Arrange
        var sut = new ContainerActionBackend([], CreateOptions(new SandboxOptions()));

        // Act
        var result = await sut.ExecuteAsync(CreateInvocation(), CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("SandboxRuntimeNotConfigured", result.ErrorCode);
    }

    /// <summary>Provider 指定済みでも該当ランタイム未登録なら SandboxRuntimeNotConfigured。</summary>
    [Fact]
    public async Task Container_WhenRuntimeNotRegistered_ReturnsSandboxRuntimeNotConfigured()
    {
        // Arrange
        var sandbox = new SandboxOptions { ContainerProvider = "missing" };
        var sut = new ContainerActionBackend(
            [new RecordingSandboxRuntime("docker")],
            CreateOptions(sandbox));

        // Act
        var result = await sut.ExecuteAsync(CreateInvocation(), CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("SandboxRuntimeNotConfigured", result.ErrorCode);
    }

    /// <summary>Container は設定された ProviderKey のランタイムへ入力・上限を渡して委譲する。</summary>
    [Fact]
    public async Task Container_WhenRuntimeConfigured_DelegatesWithRuntimeInputAndLimits()
    {
        // Arrange
        var runtime = new RecordingSandboxRuntime("docker");
        var sandbox = new SandboxOptions
        {
            ContainerProvider = "docker",
            CpuLimit = 0.5,
            MemoryLimitMiB = 256,
            TimeoutSeconds = 30,
        };
        var sut = new ContainerActionBackend([runtime], CreateOptions(sandbox));

        // Act
        var result = await sut.ExecuteAsync(CreateInvocation(runtimeInput: new { value = 1 }), CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(runtime.LastRequest);
        Assert.NotNull(runtime.LastRequest!.Input);
        Assert.NotNull(runtime.LastLimits);
        Assert.Equal(0.5, runtime.LastLimits!.CpuLimit);
        Assert.Equal(256, runtime.LastLimits.MemoryLimitMiB);
        Assert.Equal(TimeSpan.FromSeconds(30), runtime.LastLimits.Timeout);
    }

    /// <summary>Wasm は WasmProvider 設定に従ってランタイムへ委譲する。</summary>
    [Fact]
    public async Task Wasm_WhenRuntimeConfigured_DelegatesToRuntime()
    {
        // Arrange
        var runtime = new RecordingSandboxRuntime("wasmtime");
        var sandbox = new SandboxOptions { WasmProvider = "wasmtime" };
        var sut = new WasmActionBackend([runtime], CreateOptions(sandbox));

        // Act
        var result = await sut.ExecuteAsync(CreateInvocation(), CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(runtime.LastRequest);
    }

    private static IOptions<ExecutionPolicyOptions> CreateOptions(SandboxOptions sandbox) =>
        Options.Create(new ExecutionPolicyOptions { Sandbox = sandbox });

    private static ActionBackendInvocation CreateInvocation(object? runtimeInput = null)
    {
        var request = new ActionExecutionRequest
        {
            ExecutionId = "exec-sandbox-1",
            StateName = "A",
            ActionId = "test.module.action",
            TenantId = ActionExecutionTestSupport.DefaultTenantId.ToString("D"),
        };
        return new ActionBackendInvocation(request, runtimeInput);
    }

    private sealed class RecordingSandboxRuntime : IActionSandboxRuntime
    {
        public RecordingSandboxRuntime(string providerKey) => ProviderKey = providerKey;

        public string ProviderKey { get; }

        public ActionExecutionRequest? LastRequest { get; private set; }

        public SandboxLimits? LastLimits { get; private set; }

        public Task<ActionExecutionResult> RunAsync(
            ActionExecutionRequest request,
            SandboxLimits limits,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastLimits = limits;
            return Task.FromResult(new ActionExecutionResult { Success = true });
        }
    }
}
