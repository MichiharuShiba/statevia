using Statevia.CoreEngine.Domain.Events;

namespace Statevia.CoreEngine.Application.Decide;

/// <summary>POST /internal/v1/decide のレスポンス。architecture.v2 §4.1。accepted なら events、rejected なら error。</summary>
public sealed record DecideResponse(
    bool Accepted,
    string? ExecutionId = null,
    IReadOnlyList<EventEnvelope>? Events = null,
    DecideError? Error = null);
