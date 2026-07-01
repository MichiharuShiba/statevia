using System.Collections;
using Statevia.Core.Engine.Definition;

namespace Statevia.Service.Api.Application.Actions.Validation;

/// <summary>
/// <see cref="StateInputDefinition.Values"/> を実行時と同じ論理 input ツリーへ正規化する。
/// </summary>
/// <remarks>
/// <para>
/// Loader は YAML をフラットな <c>Values</c> 辞書として保持する。キーの表記は次の 2 系統がある。
/// </para>
/// <list type="bullet">
/// <item><description>ドットキー — <c>ship.address</c> → <c>Values["ship.address"]</c></description></item>
/// <item><description>ネスト map — <c>ship: { address: "x" }</c> → <c>Values["ship"].Literal</c> が子 map</description></item>
/// </list>
/// <para>
/// 実行時は <see cref="StateInputEvaluator"/> が <c>SetByDottedKey</c> で同一ツリーに組み立てる。
/// Compiler の schema 検証も同じ論理ツリーに揃えてから <c>ActionInputSchemaValidator</c> が再帰検証する（フェーズ F2）。
/// </para>
/// <para>
/// 正規化の流れ: 各 <c>Values</c> エントリをノード化 → ルート辞書へマージ → 衝突時は fail-fast（422）。
/// 同等 YAML の例:
/// </para>
/// <code>
/// input:
///   ship.address: "東京都"
///   ship.contact.email: "a@example.com"
/// </code>
/// <para>と</para>
/// <code>
/// input:
///   ship:
///     address: "東京都"
///     contact:
///       email: "a@example.com"
/// </code>
/// <para>はマージ後に同じツリーになる。一方、<c>ship: "scalar"</c> と <c>ship.address: "x"</c> の併記は衝突する。</para>
/// </remarks>
internal static class ActionInputTreeNormalizer
{
    /// <summary>正規化後の input ツリーノード（リーフまたはオブジェクトのいずれか）。</summary>
    internal sealed class NormalizedInputNode
    {
        private NormalizedInputNode(
            StateInputValueDefinition? leaf,
            Dictionary<string, NormalizedInputNode>? children)
        {
            Leaf = leaf;
            Children = children;
        }

        /// <summary>リーフ値（path / literal）。オブジェクトノードでは null。</summary>
        public StateInputValueDefinition? Leaf { get; }

        /// <summary>子プロパティ。リーフノードでは null。</summary>
        public IReadOnlyDictionary<string, NormalizedInputNode>? Children { get; }

        /// <summary>オブジェクトノードかどうか。</summary>
        public bool IsObject => Children is not null;

        /// <summary>リーフノードを構築する。</summary>
        /// <param name="value">入力値定義。</param>
        /// <returns>リーフノード。</returns>
        public static NormalizedInputNode FromLeaf(StateInputValueDefinition value) =>
            new(value, null);

        /// <summary>オブジェクトノードを構築する。</summary>
        /// <param name="children">子ノード辞書。</param>
        /// <returns>オブジェクトノード。</returns>
        public static NormalizedInputNode FromObject(Dictionary<string, NormalizedInputNode> children) =>
            new(null, children);
    }

    /// <summary>正規化エラー（jsonPath とメッセージ）。機微値は含めない（IO-14）。</summary>
    /// <param name="JsonPath">階層 jsonPath（例: <c>$.input.ship</c>）。</param>
    /// <param name="Message">エラーメッセージ。</param>
    internal readonly record struct NormalizationError(string JsonPath, string Message);

    /// <summary>
    /// <paramref name="values"/> を論理ツリーへマージする。
    /// </summary>
    /// <param name="values">状態 input の values マップ。</param>
    /// <returns>ルートオブジェクトノードと正規化エラー。エラーが 1 件でもあれば呼び出し側は検証を打ち切る。</returns>
    public static (NormalizedInputNode Root, IReadOnlyList<NormalizationError> Errors) Normalize(
        IReadOnlyDictionary<string, StateInputValueDefinition>? values)
    {
        var root = new Dictionary<string, NormalizedInputNode>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<NormalizationError>();

        if (values is null || values.Count == 0)
        {
            return (NormalizedInputNode.FromObject(root), errors);
        }

        // Values の列挙順は Loader 依存。マージはキー単位で決定的だが、衝突検出は先に処理したエントリを基準にする。
        foreach (var (key, valueDef) in values)
        {
            var node = CreateNodeFromValueDefinition(valueDef, errors, BuildJsonPath("$.input", key));
            if (node is null)
            {
                continue;
            }

            // ドットを含むキーは中間セグメントへオブジェクトを自動生成してから葉へ到達する（SetByDottedKey 相当）。
            if (key.Contains('.', StringComparison.Ordinal))
            {
                MergeDottedKey(root, key, node, errors, "$.input");
            }
            else
            {
                MergeTopLevelKey(root, key, node, errors, $"$.input.{key}");
            }
        }

        return (NormalizedInputNode.FromObject(root), errors);
    }

    /// <summary>
    /// 1 件の <see cref="StateInputValueDefinition"/> をツリーノードへ変換する。
    /// </summary>
    /// <remarks>
    /// <list type="number">
    /// <item><description><see cref="StateInputValueDefinition.Path"/> あり → リーフ（実行時評価対象の path 式）</description></item>
    /// <item><description><see cref="StateInputValueDefinition.Literal"/> が map → オブジェクトノード（ネスト YAML）。子キーも再帰的に処理し、子にドットキーがあれば同階層でマージする</description></item>
    /// <item><description>上記以外 → スカラーリテラルのリーフ</description></item>
    /// </list>
    /// </remarks>
    private static NormalizedInputNode? CreateNodeFromValueDefinition(
        StateInputValueDefinition valueDef,
        List<NormalizationError> errors,
        string jsonPath)
    {
        if (valueDef.Path is not null)
        {
            return NormalizedInputNode.FromLeaf(valueDef);
        }

        if (TryAsStringDictionary(valueDef.Literal, out var map))
        {
            // ship: { address: "x", contact: { email: "y" } } 形式をここで展開する。
            var children = new Dictionary<string, NormalizedInputNode>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, raw) in map)
            {
                var childDef = ParseRawInputValue(raw);
                var childNode = CreateNodeFromValueDefinition(childDef, errors, BuildJsonPath(jsonPath, key));
                if (childNode is null)
                {
                    continue;
                }

                if (key.Contains('.', StringComparison.Ordinal))
                {
                    MergeDottedKey(children, key, childNode, errors, jsonPath);
                }
                else
                {
                    MergeTopLevelKey(children, key, childNode, errors, BuildJsonPath(jsonPath, key));
                }
            }

            return NormalizedInputNode.FromObject(children);
        }

        return NormalizedInputNode.FromLeaf(valueDef);
    }

    /// <summary>
    /// YAML ネスト map 内の生値を <see cref="StateInputValueDefinition"/> に解釈する（Loader の <c>ParseStrictInputValue</c> と同趣旨）。
    /// </summary>
    private static StateInputValueDefinition ParseRawInputValue(object? raw)
    {
        if (raw is string text && IsPathExpression(text))
        {
            return new StateInputValueDefinition { Path = text };
        }

        if (TryAsStringDictionary(raw, out var nested))
        {
            return new StateInputValueDefinition { Literal = nested };
        }

        return new StateInputValueDefinition { Literal = raw };
    }

    /// <summary>
    /// ドット区切りキーを中間オブジェクトを辿りながら配置する（<c>SetByDottedKey</c> 相当）。
    /// </summary>
    /// <remarks>
    /// 例: <c>ship.contact.email</c> は <c>ship</c> → <c>contact</c> のオブジェクトを生成し、
    /// 最終セグメント <c>email</c> を <see cref="MergeTopLevelKey"/> で葉として設定する。
    /// 途中セグメントが既にリーフ（スカラー path/literal）の場合はネストできないため衝突とする。
    /// </remarks>
    private static void MergeDottedKey(
        Dictionary<string, NormalizedInputNode> root,
        string dottedKey,
        NormalizedInputNode node,
        List<NormalizationError> errors,
        string basePath)
    {
        var parts = dottedKey.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return;
        }

        var current = root;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            var segmentPath = BuildJsonPath(basePath, string.Join('.', parts, 0, i + 1));
            if (!current.TryGetValue(part, out var existing))
            {
                // 中間パスが未存在なら空オブジェクトを挿入して降りる。
                var created = new Dictionary<string, NormalizedInputNode>(StringComparer.OrdinalIgnoreCase);
                current[part] = NormalizedInputNode.FromObject(created);
                current = created;
                continue;
            }

            if (!existing.IsObject || existing.Children is not Dictionary<string, NormalizedInputNode> childDict)
            {
                errors.Add(new NormalizationError(
                    segmentPath,
                    $"Input normalization conflict: cannot set nested property under scalar or leaf at '{part}'."));
                return;
            }

            current = childDict;
        }

        var leafKey = parts[^1];
        var leafPath = BuildJsonPath(basePath, dottedKey);
        MergeTopLevelKey(current, leafKey, node, errors, leafPath);
    }

    /// <summary>
    /// 同一階層のキーへノードをマージする。オブジェクト同士は子を再帰マージする。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description>キー未存在 → そのまま挿入</description></item>
    /// <item><description>両方オブジェクト → 子辞書を再帰マージ（ネスト map とドットキーの併用を許容）</description></item>
    /// <item><description>リーフとオブジェクト、またはリーフ同士の重複 → 衝突（要件2 No.9）</description></item>
    /// </list>
    /// </remarks>
    private static void MergeTopLevelKey(
        Dictionary<string, NormalizedInputNode> root,
        string key,
        NormalizedInputNode node,
        List<NormalizationError> errors,
        string jsonPath)
    {
        if (!root.TryGetValue(key, out var existing))
        {
            root[key] = node;
            return;
        }

        if (existing.IsObject && node.IsObject
            && existing.Children is Dictionary<string, NormalizedInputNode> existingChildren
            && node.Children is Dictionary<string, NormalizedInputNode> incomingChildren)
        {
            foreach (var (childKey, childNode) in incomingChildren)
            {
                MergeTopLevelKey(
                    existingChildren,
                    childKey,
                    childNode,
                    errors,
                    BuildJsonPath(jsonPath, childKey));
            }

            return;
        }

        errors.Add(new NormalizationError(
            jsonPath,
            $"Input normalization conflict: duplicate or incompatible definitions for '{key}'."));
    }

    /// <summary>
    /// YAML / JSON 由来の map を <c>string → object?</c> 辞書として扱えるか判定する。
    /// </summary>
    /// <remarks>
    /// <see cref="Dictionary{TKey, TValue}"/> は <see cref="IReadOnlyDictionary{TKey, TValue}"/> より先に判定する（パターン到達性）。
    /// 非 string キーは無視する（Loader の <c>ToStringDict</c> 後を想定）。
    /// </remarks>
    private static bool TryAsStringDictionary(object? value, out IReadOnlyDictionary<string, object?> map)
    {
        switch (value)
        {
            case Dictionary<string, object?> dictionary:
                map = dictionary;
                return true;
            case IReadOnlyDictionary<string, object?> readOnly:
                map = readOnly;
                return true;
            case IDictionary generic:
            {
                var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (DictionaryEntry entry in generic)
                {
                    if (entry.Key is string key)
                    {
                        result[key] = entry.Value;
                    }
                }

                map = result;
                return true;
            }
            default:
                map = null!;
                return false;
        }
    }

    /// <summary>Compiler 422 用の階層 jsonPath を組み立てる（<c>$.input.ship.address</c> 形式）。</summary>
    private static string BuildJsonPath(string basePath, string segment) =>
        string.IsNullOrEmpty(segment) ? basePath : $"{basePath}.{segment}";

    /// <summary>Loader と同じ path 式判定（<c>$</c> または <c>$.</c> 始まり）。</summary>
    private static bool IsPathExpression(string value) =>
        value == "$" || value.StartsWith("$.", StringComparison.Ordinal);
}
