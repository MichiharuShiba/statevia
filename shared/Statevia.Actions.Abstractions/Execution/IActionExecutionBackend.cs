namespace Statevia.Actions.Abstractions.Execution;

/// <summary><see cref="ActionExecutionMode"/> を満たす具体実行実装。</summary>
/// <remarks>
/// <para>1 つの Mode（実行時の隔離レベル・セキュリティモデル契約）に対し、複数の Backend 実装を登録できる。</para>
/// <para>選択は Platform 側の Backend セレクタが担う。Engine（FSM）は本契約に依存しない。</para>
/// </remarks>
public interface IActionExecutionBackend
{
    /// <summary>この Backend が満たす実行モード（隔離契約）。</summary>
    ActionExecutionMode Mode { get; }

    /// <summary>
    /// 同一 Mode 内で実装を識別するキー（MVP）。
    /// 将来は構造化された Capability 記述へ発展する余地を残す。
    /// </summary>
    string ProviderKey { get; }

    /// <summary>統一呼び出し DTO に基づき Action を実行する。</summary>
    /// <param name="invocation">実行に必要な情報。各 Backend は必要分のみ参照する。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>実行結果。</returns>
    Task<ActionExecutionResult> ExecuteAsync(ActionBackendInvocation invocation, CancellationToken cancellationToken);
}
