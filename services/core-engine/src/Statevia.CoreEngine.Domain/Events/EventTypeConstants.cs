namespace Statevia.CoreEngine.Domain.Events;

/// <summary>固定イベント一覧。core-events-spec §2 / core-api event-types に準拠。ここにない type は発行禁止。</summary>
public static class EventTypeConstants
{
    // A. Execution Lifecycle
    public const string ExecutionCreated = "EXECUTION_CREATED";
    public const string ExecutionStarted = "EXECUTION_STARTED";
    public const string ExecutionCompleted = "EXECUTION_COMPLETED";
    public const string ExecutionArchived = "EXECUTION_ARCHIVED";

    // B. Execution Termination (Cancel/Fail)
    public const string ExecutionCancelRequested = "EXECUTION_CANCEL_REQUESTED";
    public const string ExecutionCanceled = "EXECUTION_CANCELED";
    public const string ExecutionFailRequested = "EXECUTION_FAIL_REQUESTED";
    public const string ExecutionFailed = "EXECUTION_FAILED";

    // C. Node Lifecycle
    public const string NodeCreated = "NODE_CREATED";
    public const string NodeReady = "NODE_READY";
    public const string NodeStarted = "NODE_STARTED";
    public const string NodeProgressReported = "NODE_PROGRESS_REPORTED";
    public const string NodeWaiting = "NODE_WAITING";
    public const string NodeResumeRequested = "NODE_RESUME_REQUESTED";
    public const string NodeResumed = "NODE_RESUMED";
    public const string NodeSucceeded = "NODE_SUCCEEDED";
    public const string NodeFailReported = "NODE_FAIL_REPORTED";
    public const string NodeFailed = "NODE_FAILED";

    // D. Node Cancellation
    public const string NodeCancelRequested = "NODE_CANCEL_REQUESTED";
    public const string NodeCanceled = "NODE_CANCELED";
    public const string NodeInterruptRequested = "NODE_INTERRUPT_REQUESTED";

    // E. Graph Control (Fork/Join)
    public const string ForkOpened = "FORK_OPENED";
    public const string JoinGateUpdated = "JOIN_GATE_UPDATED";
    public const string JoinPassed = "JOIN_PASSED";

    /// <summary>全 24 種のイベント type。検証・列挙用。</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        ExecutionCreated,
        ExecutionStarted,
        ExecutionCompleted,
        ExecutionArchived,
        ExecutionCancelRequested,
        ExecutionCanceled,
        ExecutionFailRequested,
        ExecutionFailed,
        NodeCreated,
        NodeReady,
        NodeStarted,
        NodeProgressReported,
        NodeWaiting,
        NodeResumeRequested,
        NodeResumed,
        NodeSucceeded,
        NodeFailReported,
        NodeFailed,
        NodeCancelRequested,
        NodeCanceled,
        NodeInterruptRequested,
        ForkOpened,
        JoinGateUpdated,
        JoinPassed,
    };

    /// <summary>指定された type が固定一覧に含まれるか。</summary>
    public static bool IsKnown(string type) => All.Contains(type);
}
