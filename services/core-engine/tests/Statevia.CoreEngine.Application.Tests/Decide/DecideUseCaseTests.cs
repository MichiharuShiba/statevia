using Statevia.CoreEngine.Application.Decide;
using Statevia.CoreEngine.Domain.Events;

namespace Statevia.CoreEngine.Application.Tests.Decide;

/// <summary>DecideUseCase.Execute の単体テスト。受理・拒否・Guard の動作を検証する。</summary>
public static class DecideUseCaseTests
{
    private static DecideRequest NewRequest(
        string commandType,
        string executionId,
        ExecutionStateDto execution,
        IReadOnlyDictionary<string, object?>? payload = null) =>
        new(
            RequestId: "req-1",
            TenantId: "t1",
            IdempotencyKey: "key-1",
            CorrelationId: "corr-1",
            Actor: new ActorDto("user", "u1"),
            Basis: new BasisDto("Execution", execution, 0),
            Command: new CommandDto(commandType, executionId, payload));

    private static ExecutionStateDto ActiveExecution(string executionId, string? cancelRequestedAt = null) =>
        new(
            ExecutionId: executionId,
            GraphId: "g1",
            Status: "ACTIVE",
            Nodes: new Dictionary<string, NodeStateDto>(),
            Version: 0,
            CancelRequestedAt: cancelRequestedAt,
            CanceledAt: null,
            FailedAt: null,
            CompletedAt: null);

    private static ExecutionStateDto TerminalExecution(string executionId, string status) =>
        new(
            ExecutionId: executionId,
            GraphId: "g1",
            Status: status,
            Nodes: new Dictionary<string, NodeStateDto>(),
            Version: 1,
            CancelRequestedAt: null,
            CanceledAt: status == "CANCELED" ? "2020-01-01T00:00:00Z" : null,
            FailedAt: status == "FAILED" ? "2020-01-01T00:00:00Z" : null,
            CompletedAt: status == "COMPLETED" ? "2020-01-01T00:00:00Z" : null);

    private static ExecutionStateDto EmptyExecution() =>
        new(
            ExecutionId: "",
            GraphId: "",
            Status: "ACTIVE",
            Nodes: new Dictionary<string, NodeStateDto>(),
            Version: 0,
            CancelRequestedAt: null,
            CanceledAt: null,
            FailedAt: null,
            CompletedAt: null);

    [Fact]
    public static void Execute_NullRequest_Throws()
    {
        // Arrange
        DecideRequest? request = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => DecideUseCase.Execute(request!));
    }

    [Fact]
    public static void Execute_CreateExecution_Accepted_ReturnsOneEvent()
    {
        // Arrange: 新規作成なので basis.execution は空でよい
        var executionId = "ex-new-1";
        var request = NewRequest(CommandTypes.CreateExecution, executionId, EmptyExecution(), new Dictionary<string, object?>
        {
            ["graphId"] = "graph-1",
            ["input"] = new { foo = "bar" },
        });

        // Act
        var response = DecideUseCase.Execute(request);

        // Assert
        Assert.True(response.Accepted);
        Assert.Equal(executionId, response.ExecutionId);
        Assert.NotNull(response.Events);
        var list = response.Events;
        Assert.Single(list);
        Assert.Equal(EventTypeConstants.ExecutionCreated, list[0].Type);
        Assert.Equal(executionId, list[0].ExecutionId);
        Assert.Null(response.Error);
    }

    [Fact]
    public static void Execute_StartExecution_Active_Accepted_ReturnsOneEvent()
    {
        // Arrange
        var executionId = "ex-1";
        var request = NewRequest(CommandTypes.StartExecution, executionId, ActiveExecution(executionId));

        // Act
        var response = DecideUseCase.Execute(request);

        // Assert
        Assert.True(response.Accepted);
        Assert.Equal(executionId, response.ExecutionId);
        Assert.NotNull(response.Events);
        Assert.Single(response.Events);
        Assert.Equal(EventTypeConstants.ExecutionStarted, response.Events[0].Type);
        Assert.Null(response.Error);
    }

    [Fact]
    public static void Execute_StartExecution_Terminal_Rejected()
    {
        // Arrange: COMPLETED の実行に対して StartExecution
        var executionId = "ex-2";
        var request = NewRequest(CommandTypes.StartExecution, executionId, TerminalExecution(executionId, "COMPLETED"));

        // Act
        var response = DecideUseCase.Execute(request);

        // Assert
        Assert.False(response.Accepted);
        Assert.NotNull(response.Error);
        Assert.Equal(DecideErrorCodes.CommandRejected, response.Error.Code);
        Assert.Contains("terminal", response.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(response.Events);
    }

    [Fact]
    public static void Execute_StartExecution_CancelRequested_Rejected()
    {
        // Arrange
        var executionId = "ex-3";
        var request = NewRequest(CommandTypes.StartExecution, executionId, ActiveExecution(executionId, "2020-01-01T00:00:00Z"));

        // Act
        var response = DecideUseCase.Execute(request);

        // Assert
        Assert.False(response.Accepted);
        Assert.NotNull(response.Error);
        Assert.Equal(DecideErrorCodes.CommandRejected, response.Error.Code);
        Assert.Contains("cancel", response.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public static void Execute_StartExecution_ExecutionIdMismatch_NotFound()
    {
        // Arrange: basis.execution の executionId と command.executionId が異なる
        var request = NewRequest(CommandTypes.StartExecution, "ex-other", ActiveExecution("ex-4"));

        // Act
        var response = DecideUseCase.Execute(request);

        // Assert
        Assert.False(response.Accepted);
        Assert.NotNull(response.Error);
        Assert.Equal(DecideErrorCodes.NotFound, response.Error.Code);
    }

    [Fact]
    public static void Execute_CancelExecution_Accepted_ReturnsOneEvent()
    {
        // Arrange
        var executionId = "ex-5";
        var request = NewRequest(
            CommandTypes.CancelExecution,
            executionId,
            ActiveExecution(executionId),
            new Dictionary<string, object?> { ["reason"] = "user requested" });

        // Act
        var response = DecideUseCase.Execute(request);

        // Assert
        Assert.True(response.Accepted);
        Assert.NotNull(response.Events);
        Assert.Single(response.Events);
        Assert.Equal(EventTypeConstants.ExecutionCancelRequested, response.Events[0].Type);
        Assert.Null(response.Error);
    }

    [Fact]
    public static void Execute_UnknownCommandType_Rejected_InvalidInput()
    {
        // Arrange
        var executionId = "ex-6";
        var request = NewRequest("UnknownCommand", executionId, ActiveExecution(executionId));

        // Act
        var response = DecideUseCase.Execute(request);

        // Assert
        Assert.False(response.Accepted);
        Assert.NotNull(response.Error);
        Assert.Equal(DecideErrorCodes.InvalidInput, response.Error.Code);
        Assert.Contains("Unknown command type", response.Error.Message, StringComparison.Ordinal);
    }
}
