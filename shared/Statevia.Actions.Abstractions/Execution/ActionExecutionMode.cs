namespace Statevia.Actions.Abstractions.Execution;

/// <summary>Action の実行モード。</summary>
public enum ActionExecutionMode
{
    /// <summary>Core-API プロセス内で <see cref="Statevia.Core.Engine.Abstractions.IStateExecutor"/> を実行する。</summary>
    InProcess,

    /// <summary>Action Host 経由の別プロセス実行（Phase 3）。</summary>
    OutOfProcess,

    /// <summary>リモート実行（将来）。</summary>
    Remote,

    /// <summary>コンテナ隔離実行（Phase 4）。</summary>
    Container,

    /// <summary>WASM サンドボックス実行（Phase 4）。</summary>
    Wasm,
}
