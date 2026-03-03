namespace Statevia.CoreEngine.Application.Decide;

/// <summary>DecideRequest.basis。楽観ロック用の state + expectedVersion。architecture.v2 §4.1。</summary>
public sealed record BasisDto(
    string Kind,
    ExecutionStateDto Execution,
    int ExpectedVersion);
