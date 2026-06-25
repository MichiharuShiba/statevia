using Statevia.Actions.Abstractions.Execution;

namespace Statevia.Core.Api.Application.Actions.Execution;

/// <summary>Action Host 経由の OutOfProcess 実行 Backend。</summary>
internal sealed class OutOfProcessBackend
{
    private readonly IActionHostExecutionClient _client;

    /// <summary>OutOfProcess Backend を構築する。</summary>
    /// <param name="client">Action Host 実行クライアント。</param>
    public OutOfProcessBackend(IActionHostExecutionClient client) =>
        _client = client;

    /// <summary>Action Host に実行を委譲する。</summary>
    /// <param name="request">Platform 実行リクエスト。</param>
    /// <param name="runtimeInput">Engine が解決した入力。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>実行結果。</returns>
    public Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionRequest request,
        object? runtimeInput,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hostRequest = ActionExecutionRuntimeInputMapper.WithRuntimeInput(request, runtimeInput);
        return _client.ExecuteAsync(hostRequest, cancellationToken);
    }
}
