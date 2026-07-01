using Statevia.Core.Actions.Abstractions.Execution;

namespace Statevia.Core.Actions.Abstractions.Catalog;

/// <summary>Action 作者が指定する実行ヒント。最終 Mode は Policy が決定する。</summary>
public sealed record ActionExecutionHints
{
    /// <summary>推奨実行モード（任意）。</summary>
    public ActionExecutionMode? PreferredMode { get; init; }

    /// <summary>隔離実行が必要か。</summary>
    public bool RequiresIsolation { get; init; }

    /// <summary>許可される実行モード集合（任意）。</summary>
    public IReadOnlySet<ActionExecutionMode>? AllowedModes { get; init; }
}
