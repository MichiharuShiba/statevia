using Statevia.Actions.Abstractions.Execution;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>Action Host 経由の OutOfProcess 実行 Backend。</summary>
/// <remarks>Engine 内部状態（<c>StateContext</c>）は渡さず、リクエストと入力のみを Host へ委譲する。</remarks>
internal sealed class OutOfProcessBackend : IActionExecutionBackend
{
    private readonly IActionHostExecutionClient _client;

    /// <summary>OutOfProcess Backend を構築する。</summary>
    /// <param name="client">Action Host 実行クライアント。</param>
    public OutOfProcessBackend(IActionHostExecutionClient client) =>
        _client = client;

    /// <inheritdoc />
    public ActionExecutionMode Mode => ActionExecutionMode.OutOfProcess;

    /// <inheritdoc />
    public string ProviderKey => "action-host";

    /// <inheritdoc />
    public Task<ActionExecutionResult> ExecuteAsync(
        ActionBackendInvocation invocation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        var hostRequest = ActionExecutionRuntimeInputMapper.WithRuntimeInput(
            invocation.Request,
            invocation.RuntimeInput);
        return _client.ExecuteAsync(hostRequest, cancellationToken);
    }
}
