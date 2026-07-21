namespace Statevia.Core.Engine.Engine;

/// <summary>
/// 1 実行インスタンスの実行時データ（Execution Context）を保持する。
/// </summary>
/// <remarks>
/// <para>
/// 定義 YAML の <c>input</c> パス式（SimpleJsonPath）の評価根となる。
/// トップレベルは <c>input</c> / <c>output</c> / <c>states</c> / <c>vars</c> / <c>sys</c> 固定。
/// </para>
/// <para>
/// 型名は <see cref="System.Threading.ExecutionContext"/> との衝突を避けるため
/// <c>WorkflowExecutionContext</c> とする（概念名は Execution Context）。
/// </para>
/// <para>
/// Phase 1 では <c>vars</c> / <c>sys</c> は空オブジェクトの予約のみ。読み書き API は提供しない。
/// </para>
/// </remarks>
public sealed class WorkflowExecutionContext
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyObject =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    private readonly object _lock = new();
    private readonly Dictionary<string, object?> _states = new(StringComparer.OrdinalIgnoreCase);
    private object? _workflowOutput = EmptyObject;

    /// <summary>ワークフロー開始時に設定した input（不変）。</summary>
    public object? Input { get; }

    /// <summary>
    /// 開始 input を設定した Context を生成する。
    /// </summary>
    /// <param name="input"><see cref="Abstractions.IExecutionEngine.Start"/> に渡された開始 input。</param>
    /// <returns>初期化済み Context。<c>states</c> は空、<c>output</c> / <c>vars</c> / <c>sys</c> は空オブジェクト。</returns>
    public static WorkflowExecutionContext Create(object? input) => new(input);

    private WorkflowExecutionContext(object? input) => Input = input;

    /// <summary>指定状態が完了済み（output 記録済み）か。</summary>
    /// <param name="stateName">定義上の状態名。</param>
    /// <returns>完了済みのとき true。</returns>
    public bool HasStateOutput(string stateName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        lock (_lock)
        {
            return _states.ContainsKey(stateName);
        }
    }

    /// <summary>
    /// 状態完了時の output を <c>states.&lt;StateName&gt;.output</c> として記録する。
    /// </summary>
    /// <param name="stateName">定義上の状態名。</param>
    /// <param name="output">状態の成功 output（失敗時は呼ばない想定）。</param>
    public void SetStateOutput(string stateName, object? output)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        lock (_lock)
        {
            _states[stateName] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["output"] = output
            };
        }
    }

    /// <summary>
    /// ワークフロー終端時の最終 output を設定する（通常 1 回）。
    /// </summary>
    /// <param name="output">終端 State の output。</param>
    public void SetWorkflowOutput(object? output)
    {
        lock (_lock)
        {
            _workflowOutput = output ?? EmptyObject;
        }
    }

    /// <summary>
    /// SimpleJsonPath 評価用に Context 全体を辞書としてスナップショットする。
    /// </summary>
    /// <returns>
    /// <c>input</c> / <c>output</c> / <c>states</c> / <c>vars</c> / <c>sys</c> を持つ読み取り用ツリー。
    /// </returns>
    public IReadOnlyDictionary<string, object?> ToPathRoot()
    {
        lock (_lock)
        {
            var statesCopy = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, entry) in _states)
            {
                statesCopy[name] = entry;
            }

            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                [ExecutionContextKeys.Input] = Input,
                [ExecutionContextKeys.Output] = _workflowOutput,
                [ExecutionContextKeys.States] = statesCopy,
                [ExecutionContextKeys.Vars] = EmptyObject,
                [ExecutionContextKeys.Sys] = EmptyObject
            };
        }
    }
}
