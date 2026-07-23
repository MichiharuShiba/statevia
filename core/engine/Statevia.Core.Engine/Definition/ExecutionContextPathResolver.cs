using Statevia.Core.Engine.Engine;

namespace Statevia.Core.Engine.Definition;

/// <summary>
/// Execution Context を根とする SimpleJsonPath 解決。
/// </summary>
/// <remarks>
/// 未完了 State への <c>$.states.&lt;Name&gt;…</c> 参照は null と
/// <see cref="IncompleteStateOutput"/> 警告を返す。
/// <c>output</c> の書き込み先検証は <see cref="IsVarsWritePath"/>。
/// </remarks>
internal static class ExecutionContextPathResolver
{
    /// <summary>参照先 State がまだ完了していない。</summary>
    public const string IncompleteStateOutput = "IncompleteStateOutput";

    /// <summary>
    /// <paramref name="context"/> を評価根として <paramref name="path"/> を解決する。
    /// </summary>
    /// <param name="context">実行中 Context。</param>
    /// <param name="path">SimpleJsonPath（<c>$</c> または <c>$.…</c> / ブラケット）。</param>
    /// <returns>解決結果。</returns>
    public static SimpleJsonPathResolver.ResolveResult Resolve(WorkflowExecutionContext context, string path)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ResolveWithRoot(context.ToPathRoot(), path);
    }

    /// <summary>
    /// 事前取得した Context スナップショット <paramref name="root"/> を評価根として解決する。
    /// </summary>
    /// <remarks>
    /// 同一評価内で複数パスを解決する際、<see cref="WorkflowExecutionContext.ToPathRoot"/> を
    /// 1 度だけ取得して使い回すためのオーバーロード。解決ごとの再スナップショットによる
    /// <c>$.sys.now</c> 等の非一貫性と無駄な再確保を避ける。
    /// </remarks>
    /// <param name="root">Context スナップショット（<see cref="WorkflowExecutionContext.ToPathRoot"/> の戻り値）。</param>
    /// <param name="path">SimpleJsonPath（<c>$</c> または <c>$.…</c> / ブラケット）。</param>
    /// <returns>解決結果。</returns>
    public static SimpleJsonPathResolver.ResolveResult ResolveWithRoot(
        IReadOnlyDictionary<string, object?> root,
        string path)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (TryGetIncompleteStateReference(root, path, out _))
        {
            return new SimpleJsonPathResolver.ResolveResult(
                IsSupportedPathExpression: true,
                Found: false,
                Value: null,
                WarningReason: IncompleteStateOutput);
        }

        return SimpleJsonPathResolver.Resolve(root, path);
    }

    /// <summary>
    /// <paramref name="path"/> が <c>$.vars</c> 配下への書き込み先として正当か（Level1 の <c>output</c> 検証用）。
    /// </summary>
    /// <param name="path">検査対象パス。</param>
    /// <returns><c>$.vars</c> または <c>$.vars.&lt;seg&gt;…</c> のとき true。</returns>
    public static bool IsVarsWritePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !SimpleJsonPath.IsValid(path))
        {
            return false;
        }

        if (!SimpleJsonPath.TryGetSegments(path, out var segments) || segments.Count == 0)
        {
            return false;
        }

        return segments[0].Equals(ExecutionContextKeys.Vars, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetIncompleteStateReference(
        IReadOnlyDictionary<string, object?> root,
        string path,
        out string stateName)
    {
        stateName = string.Empty;
        if (!SimpleJsonPath.TryGetSegments(path, out var segments) || segments.Count < 2)
        {
            return false;
        }

        if (!segments[0].Equals(ExecutionContextKeys.States, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        stateName = segments[1];
        if (stateName.Length == 0)
        {
            return false;
        }

        // states スナップショットに当該 State キーが無ければ未完了（output 未記録）。
        return root.TryGetValue(ExecutionContextKeys.States, out var statesObj)
            && statesObj is IReadOnlyDictionary<string, object?> states
            && !states.ContainsKey(stateName);
    }
}
