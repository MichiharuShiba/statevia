namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>
/// 1 回の <see cref="DefinitionValidator.Validate"/> で共有するスナップショット。
/// ルールはこれを読むだけとし、同じグラフ走査を繰り返さない。
/// </summary>
/// <remarks>
/// <para>呼び出し元は <see cref="DefinitionValidator"/>。寿命は 1 回の Validate に閉じる。</para>
/// <para>Fork 領域集合・Join 供給は ForkRegion フェーズのルールが必要になった時点で載せる。</para>
/// </remarks>
public sealed class DefinitionValidationContext
{
    /// <summary>検証対象の定義。</summary>
    public WorkflowDefinition Definition { get; }

    /// <summary>状態名集合（OrdinalIgnoreCase）。</summary>
    public IReadOnlySet<string> StateNames { get; }

    /// <summary>初期状態。Reachability ルールが設定する。</summary>
    public string? InitialState { get; internal set; }

    /// <summary>初期状態から到達可能な状態。Reachability ルールが設定する。</summary>
    public IReadOnlySet<string>? ReachableStates { get; internal set; }

    /// <summary>Fork 領域スナップショット。ForkRegion フェーズ開始時に構築する。</summary>
    internal ForkRegionSnapshot? ForkRegion { get; set; }

    /// <summary>
    /// 定義からコンテキストを構築する。
    /// </summary>
    /// <param name="definition">検証対象。</param>
    public DefinitionValidationContext(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Definition = definition;
        StateNames = new HashSet<string>(definition.States.Keys, StringComparer.OrdinalIgnoreCase);
    }
}
