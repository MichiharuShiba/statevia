namespace Statevia.CoreEngine.Application.Decide;

/// <summary>DecideRequest.command。種別と payload。architecture.v2 §4.1。</summary>
public sealed record CommandDto(
    string Type,
    string ExecutionId,
    IReadOnlyDictionary<string, object?>? Payload = null);
