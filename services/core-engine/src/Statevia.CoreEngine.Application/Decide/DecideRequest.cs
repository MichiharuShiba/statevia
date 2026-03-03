namespace Statevia.CoreEngine.Application.Decide;

/// <summary>POST /internal/v1/decide のリクエスト。architecture.v2 §4.1。</summary>
public sealed record DecideRequest(
    string RequestId,
    string TenantId,
    string IdempotencyKey,
    string? CorrelationId,
    ActorDto Actor,
    BasisDto Basis,
    CommandDto Command);
