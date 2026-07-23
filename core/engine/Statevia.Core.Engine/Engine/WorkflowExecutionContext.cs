using System.Globalization;

using Statevia.Core.Engine.Definition;

namespace Statevia.Core.Engine.Engine;

/// <summary>
/// 1 実行インスタンスの実行時データ（Execution Context）を保持する。
/// </summary>
/// <remarks>
/// <para>
/// 定義 YAML の <c>input</c> / <c>output</c> パス式（SimpleJsonPath）の評価根となる。
/// トップレベルは <c>input</c> / <c>output</c> / <c>states</c> / <c>vars</c> / <c>sys</c> 固定。
/// </para>
/// <para>
/// 型名は <see cref="System.Threading.ExecutionContext"/> との衝突を避けるため
/// <c>WorkflowExecutionContext</c> とする（概念名は Execution Context）。
/// </para>
/// <para>
/// <c>vars</c> は <see cref="SetVar"/> で読み書き可能。<c>sys</c> は評価時スナップショットの読み取り専用ランタイム情報。
/// </para>
/// </remarks>
public sealed class WorkflowExecutionContext
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyObject =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    private readonly object _lock = new();
    private readonly Dictionary<string, object?> _states = new(StringComparer.OrdinalIgnoreCase);
    private object? _workflowOutput = EmptyObject;
    private object? _vars = EmptyObject;
    private readonly string _executionId;
    private readonly string _definitionName;

    /// <summary>ワークフロー開始時に設定した input（不変）。</summary>
    public object? Input { get; }

    /// <summary>
    /// 開始 input と実行メタデータで Context を生成する。
    /// </summary>
    /// <param name="input"><see cref="Abstractions.IExecutionEngine.Start"/> に渡された開始 input。</param>
    /// <param name="executionId">実行インスタンス ID（<c>$.sys.execution.id</c>）。</param>
    /// <param name="definitionName">コンパイル済み定義名（<c>$.sys.definition.name</c>）。</param>
    /// <returns>初期化済み Context。<c>states</c> / <c>vars</c> は空。</returns>
    public static WorkflowExecutionContext Create(
        object? input,
        string executionId = "",
        string definitionName = "") =>
        new(input, executionId, definitionName);

    private WorkflowExecutionContext(object? input, string executionId, string definitionName)
    {
        Input = input;
        _executionId = executionId ?? string.Empty;
        _definitionName = definitionName ?? string.Empty;
    }

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
    /// <remarks>
    /// 辞書ツリーは隔離コピーする。同一参照を <see cref="SetVar"/> した場合でも、
    /// 後続のネスト代入で履歴が壊れない。
    /// </remarks>
    /// <param name="stateName">定義上の状態名。</param>
    /// <param name="output">状態の成功 output（失敗時は呼ばない想定）。</param>
    public void SetStateOutput(string stateName, object? output)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        lock (_lock)
        {
            // vars への後続ネスト代入や呼び出し元の破壊的更新から履歴を守るため、辞書ツリーは隔離コピーする。
            _states[stateName] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["output"] = CloneForIsolation(output)
            };
        }
    }

    /// <summary>
    /// <c>$.vars</c> 配下へ値を代入する。中間オブジェクトは自動生成し、既存キーは上書きする。
    /// </summary>
    /// <remarks>
    /// 代入値の辞書ツリーは隔離コピーする。<see cref="SetStateOutput"/> と参照を共有しない。
    /// </remarks>
    /// <param name="varsPath"><c>$.vars</c> または <c>$.vars.&lt;seg&gt;…</c>（SimpleJsonPath）。</param>
    /// <param name="value">代入する値。</param>
    /// <exception cref="ArgumentException"><paramref name="varsPath"/> が <c>$.vars</c> 配下でない、または不正なとき。</exception>
    public void SetVar(string varsPath, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(varsPath);
        if (!SimpleJsonPath.TryGetSegments(varsPath, out var segments)
            || segments.Count == 0
            || !segments[0].Equals(ExecutionContextKeys.Vars, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"vars path must be under $.{ExecutionContextKeys.Vars}: {varsPath}",
                nameof(varsPath));
        }

        lock (_lock)
        {
            // states 記録や呼び出し元オブジェクトと参照を共有しないよう、代入値は隔離コピーする。
            var isolated = CloneForIsolation(value);
            if (segments.Count == 1)
            {
                _vars = isolated ?? EmptyObject;
                return;
            }

            var root = EnsureVarsDictionary();
            SetNestedValue(root, segments, startIndex: 1, isolated);
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
                [ExecutionContextKeys.Vars] = SnapshotObject(_vars),
                [ExecutionContextKeys.Sys] = BuildSysSnapshot()
            };
        }
    }

    private Dictionary<string, object?> EnsureVarsDictionary()
    {
        // EmptyObject は共有センチネルのため書き換えない（並列テスト間の汚染を防ぐ）。
        if (_vars is Dictionary<string, object?> existing
            && !ReferenceEquals(existing, EmptyObject))
        {
            return existing;
        }

        var created = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (_vars is IReadOnlyDictionary<string, object?> readOnly
            && !ReferenceEquals(readOnly, EmptyObject))
        {
            foreach (var (key, entry) in readOnly)
            {
                created[key] = entry;
            }
        }

        _vars = created;
        return created;
    }

    private static void SetNestedValue(
        Dictionary<string, object?> root,
        IReadOnlyList<string> segments,
        int startIndex,
        object? value)
    {
        var current = root;
        for (var i = startIndex; i < segments.Count - 1; i++)
        {
            var segment = segments[i];
            if (!current.TryGetValue(segment, out var next) || next is not Dictionary<string, object?> nextDict)
            {
                nextDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                current[segment] = nextDict;
            }

            current = nextDict;
        }

        current[segments[^1]] = value;
    }

    private static object? SnapshotObject(object? value) =>
        CloneForIsolation(value) ?? EmptyObject;

    /// <summary>
    /// 辞書ツリーを再帰コピーし、スカラー等はそのまま返す。Context 内の states / vars 間の参照共有を防ぐ。
    /// </summary>
    private static object? CloneForIsolation(object? value)
    {
        if (value is null || ReferenceEquals(value, EmptyObject))
        {
            return value;
        }

        if (value is Dictionary<string, object?> dict)
        {
            var clone = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, entry) in dict)
            {
                clone[key] = CloneForIsolation(entry);
            }

            return clone;
        }

        if (value is IReadOnlyDictionary<string, object?> readOnly)
        {
            var clone = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, entry) in readOnly)
            {
                clone[key] = CloneForIsolation(entry);
            }

            return clone;
        }

        return value;
    }

    private Dictionary<string, object?> BuildSysSnapshot()
    {
        var now = DateTimeOffset.Now;
        var utcNow = DateTimeOffset.UtcNow;
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["now"] = now.ToString("O", CultureInfo.InvariantCulture),
            ["today"] = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["utcNow"] = utcNow.ToString("O", CultureInfo.InvariantCulture),
            ["execution"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = _executionId
            },
            ["definition"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = _definitionName
            }
        };
    }
}
