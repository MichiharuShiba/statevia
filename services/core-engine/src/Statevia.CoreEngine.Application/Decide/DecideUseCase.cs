using Statevia.CoreEngine.Application.Commands;
using Statevia.CoreEngine.Application.Guards;
using Statevia.CoreEngine.Domain.Events;
using Statevia.CoreEngine.Domain.Execution;
using Statevia.CoreEngine.Domain.Extensions;

namespace Statevia.CoreEngine.Application.Decide;

/// <summary>Decide(DecideRequest) → DecideResponse。basis.state + expectedVersion を受け、Guards で弾き、Command に応じて Event[] を生成。</summary>
public static class DecideUseCase
{
    /// <summary>リクエストを処理し、受理時は events を、拒否時は error を返す。</summary>
    public static DecideResponse Execute(DecideRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var state = request.Basis.Execution.ToDomain();
            var actor = request.Actor.ToDomain();
            var correlationId = request.CorrelationId;
            var command = request.Command;

            // Guards: コマンド種別に応じた整合性チェック（1.6 で拡張）
            var reject = TryReject(state, command.Type, command.ExecutionId, command.Payload);
            if (reject is not null)
                return new DecideResponse(false, command.ExecutionId, null, reject);

            // Command に応じて Event[] を生成
            var events = Dispatch(command.Type, state, actor, command.ExecutionId, command.Payload, correlationId);
            return new DecideResponse(true, command.ExecutionId, events, null);
        }
        catch (ArgumentException ex)
        {
            return new DecideResponse(
                false,
                request.Command.ExecutionId,
                null,
                new DecideError(DecideErrorCodes.InvalidInput, ex.Message, null));
        }
    }

    /// <summary>Guards で拒否すべき場合に DecideError を返す。通過時は null。</summary>
    private static DecideError? TryReject(
        ExecutionState state,
        string commandType,
        string executionId,
        IReadOnlyDictionary<string, object?>? payload)
    {
        // CreateExecution 以外は executionId が state と一致することを要求
        if (commandType != CommandTypes.CreateExecution && state.ExecutionId != executionId)
            return new DecideError(DecideErrorCodes.NotFound, "Execution not found", new Dictionary<string, object?> { ["executionId"] = executionId });

        return commandType switch
        {
            CommandTypes.StartExecution => RejectIfTerminalOrCancelRequested(state),
            CommandTypes.CancelExecution => null, // Cancel は終端でも空 events で受理
            CommandTypes.CreateExecution => null,
            _ => new DecideError(DecideErrorCodes.InvalidInput, $"Unknown command type: {commandType}", null),
        };
    }

    private static DecideError? RejectIfTerminalOrCancelRequested(ExecutionState state)
    {
        if (state.IsTerminal())
            return new DecideError(DecideErrorCodes.CommandRejected, "Execution is terminal", new Dictionary<string, object?> { ["status"] = state.Status.ToString() });
        if (state.IsCancelRequested())
            return new DecideError(DecideErrorCodes.CommandRejected, "Execution is cancel-requested", new Dictionary<string, object?> { ["cancelRequestedAt"] = state.CancelRequestedAt });
        return null;
    }

    private static IReadOnlyList<EventEnvelope> Dispatch(
        string commandType,
        ExecutionState state,
        Actor actor,
        string executionId,
        IReadOnlyDictionary<string, object?>? payload,
        string? correlationId)
    {
        payload ??= new Dictionary<string, object?>();

        return commandType switch
        {
            CommandTypes.CancelExecution => CancelExecutionHandler.Handle(
                state,
                actor,
                payload.TryGetValue("reason", out var r) && r is string s ? s : null,
                correlationId),
            CommandTypes.CreateExecution => CreateExecutionHandler.Handle(
                executionId,
                payload.TryGetValue("graphId", out var g) && g is string graphId ? graphId : "",
                actor,
                payload.TryGetValue("input", out var i) ? i : null,
                correlationId),
            CommandTypes.StartExecution => StartExecutionHandler.TryHandle(state, actor, correlationId)
                ?? Array.Empty<EventEnvelope>(),
            _ => Array.Empty<EventEnvelope>(),
        };
    }
}
