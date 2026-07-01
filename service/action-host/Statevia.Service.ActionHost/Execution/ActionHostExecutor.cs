using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Service.ActionHost.Execution;
using Statevia.Service.ActionHost.Modules;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.ActionHost.Execution;

/// <summary>登録済み Module Action を OutOfProcess 向けに実行する。</summary>
internal sealed class ActionHostExecutor
{
    private static readonly EmptyEventProvider EmptyEvents = new();
    private static readonly EmptyStateStore EmptyStore = new();

    private readonly ActionHostActionRegistry _registry;
    private readonly ILogger<ActionHostExecutor> _logger;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="registry">Action レジストリ。</param>
    /// <param name="logger">ログ。</param>
    public ActionHostExecutor(ActionHostActionRegistry registry, ILogger<ActionHostExecutor> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    /// <summary>gRPC 契約相当のリクエストで Action を実行する。</summary>
    /// <param name="request">実行リクエスト。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>実行結果。</returns>
    public async Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_registry.TryGet(request.ActionId, out var registration) || registration is null)
        {
            return Failure("UnknownAction", $"Unknown action '{request.ActionId}'.");
        }

        if (request.Deadline is { } deadline && deadline <= DateTimeOffset.UtcNow)
        {
            return Failure("DeadlineExceeded", "Action execution deadline has passed.");
        }

        using var linkedCts = CreateCancellationSource(request.Deadline, cancellationToken);
        var stateContext = new StateContext
        {
            Events = EmptyEvents,
            Store = EmptyStore,
            ExecutionId = request.ExecutionId,
            StateName = request.StateName,
            Logger = NullLogger.Instance,
        };

        try
        {
            var runtimeOutput = await registration.Executor
                .ExecuteAsync(stateContext, request.Input, linkedCts.Token)
                .ConfigureAwait(false);

            return new ActionExecutionResult
            {
                Success = true,
                Output = SerializeOutput(runtimeOutput),
            };
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
            return Failure("Cancelled", "Action execution was cancelled or timed out.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NotSupportedException)
        {
            ActionHostExecutorLog.ExecutionFailed(_logger, ex, request.ActionId, request.ExecutionId);
            return Failure("ActionExecutionFailed", ex.Message);
        }
    }

    private static CancellationTokenSource CreateCancellationSource(
        DateTimeOffset? deadline,
        CancellationToken cancellationToken)
    {
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (deadline is { } deadlineValue)
        {
            var remaining = deadlineValue - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                linked.Cancel();
            }
            else
            {
                linked.CancelAfter(remaining);
            }
        }

        return linked;
    }

    private static JsonElement? SerializeOutput(object? output)
    {
        if (output is null)
        {
            return null;
        }

        if (output is JsonElement jsonElement)
        {
            return jsonElement;
        }

        var json = JsonSerializer.SerializeToElement(output);
        return json;
    }

    private static ActionExecutionResult Failure(string code, string message) =>
        new()
        {
            Success = false,
            ErrorCode = code,
            ErrorMessage = message,
        };
}

internal static partial class ActionHostExecutorLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Action execution failed for {ActionId} (ExecutionId={ExecutionId})")]
    public static partial void ExecutionFailed(ILogger logger, Exception exception, string actionId, string executionId);
}
