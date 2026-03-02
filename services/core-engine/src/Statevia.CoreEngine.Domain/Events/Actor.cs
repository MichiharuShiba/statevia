namespace Statevia.CoreEngine.Domain.Events;

/// <summary>イベントを発行した主体。core-events-spec §1.1 / data-integration-contract に準拠。</summary>
/// <param name="Kind">主体の種別。</param>
/// <param name="Id">主体の識別子（任意）。</param>
public sealed record Actor(ActorKind Kind, string? Id = null);

/// <summary>Actor の種別。</summary>
public enum ActorKind
{
    System,
    User,
    Scheduler,
    External,
}
