using System.Collections;
using Statevia.Core.Engine.Definition;

namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>
/// レベル 1 検証：状態名・参照の整合性、自己遷移禁止、未定義状態参照の検出。
/// </summary>
public static class Level1Validator
{
    /// <summary>ワークフロー定義を検証し、エラー一覧を返します。</summary>
    public static ValidationResult Validate(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var errors = new List<string>();
        if (definition.States.Count == 0)
        {
            errors.Add("At least one state is required.");
            return new ValidationResult(errors);
        }

        var stateNames = new HashSet<string>(definition.States.Keys, StringComparer.OrdinalIgnoreCase);
        var terminalTransitionCount = 0;

        foreach (var (stateName, stateDef) in definition.States)
        {
            ValidateStateName(stateName, errors);
            ValidateActionAndWait(stateName, stateDef, errors);
            ValidateTransitions(stateName, stateDef, stateNames, errors, ref terminalTransitionCount);
            ValidateJoin(stateDef, stateNames, errors);
            ValidateStateInput(stateName, stateDef, errors);
        }

        if (terminalTransitionCount == 0)
        {
            errors.Add("At least one terminal transition (end: true) is required.");
        }

        return new ValidationResult(errors);
    }

    /// <summary>状態名が空でないことを検証する。</summary>
    /// <param name="stateName">検証対象の状態名。</param>
    /// <param name="errors">検出したエラーメッセージの蓄積先。</param>
    private static void ValidateStateName(string stateName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(stateName))
        {
            errors.Add("State name cannot be empty.");
        }
    }

    /// <summary>同一状態で <c>action</c> と <c>wait</c> が同時指定されていないことを検証する。</summary>
    /// <param name="stateName">検証対象の状態名。</param>
    /// <param name="stateDef">状態定義。</param>
    /// <param name="errors">検出したエラーメッセージの蓄積先。</param>
    private static void ValidateActionAndWait(string stateName, StateDefinition stateDef, List<string> errors)
    {
        if (stateDef.Wait != null && !string.IsNullOrWhiteSpace(stateDef.Action))
        {
            errors.Add($"State '{stateName}' cannot specify both wait and action.");
        }
    }

    /// <summary>状態の <c>on</c> 遷移定義を走査し、各 fact の遷移ツリーを検証する。</summary>
    /// <param name="stateName">検証対象の状態名。</param>
    /// <param name="stateDef">状態定義。</param>
    /// <param name="stateNames">定義済み状態名の集合（参照先検証用）。</param>
    /// <param name="errors">検出したエラーメッセージの蓄積先。</param>
    /// <param name="terminalTransitionCount"><c>end: true</c> 遷移の件数（ワークフロー全体で集計）。</param>
    private static void ValidateTransitions(
        string stateName,
        StateDefinition stateDef,
        HashSet<string> stateNames,
        List<string> errors,
        ref int terminalTransitionCount)
    {
        if (stateDef.On == null)
        {
            return;
        }

        foreach (var (fact, trans) in stateDef.On)
        {
            ValidateTransitionTree(
                stateName,
                $"on.{fact}",
                trans,
                stateNames,
                errors,
                isDefaultTransition: false,
                ref terminalTransitionCount);
        }
    }

    /// <summary>1 件の遷移定義（ネスト含む）の形状と参照先を再帰的に検証する。</summary>
    /// <param name="stateName">遷移を所有する状態名（自己遷移検出用）。</param>
    /// <param name="transitionPath">エラーメッセージ用の遷移パス（例: <c>on.Completed</c>、<c>on.Completed.cases[0]</c>）。</param>
    /// <param name="trans">検証対象の遷移定義。null のときは何もしない。</param>
    /// <param name="stateNames">定義済み状態名の集合。</param>
    /// <param name="errors">検出したエラーメッセージの蓄積先。</param>
    /// <param name="isDefaultTransition"><c>default</c> 配下の遷移を検証しているとき true。</param>
    /// <param name="terminalTransitionCount"><c>end: true</c> 遷移の件数（ワークフロー全体で集計）。</param>
    private static void ValidateTransitionTree(
        string stateName,
        string transitionPath,
        TransitionDefinition? trans,
        HashSet<string> stateNames,
        List<string> errors,
        bool isDefaultTransition,
        ref int terminalTransitionCount)
    {
        if (trans is null)
        {
            return;
        }

        if (trans.End)
        {
            terminalTransitionCount++;
        }

        ValidateTransitionShape(transitionPath, trans, errors, isDefaultTransition);
        ValidateTransitionReferences(
            stateName,
            transitionPath,
            trans,
            stateNames,
            errors,
            isDefaultTransition,
            ref terminalTransitionCount);
    }

    /// <summary>遷移の構文形状（線形 next/fork/end と cases/default の組み合わせ）を検証する。</summary>
    /// <param name="transitionPath">エラーメッセージ用の遷移パス。</param>
    /// <param name="trans">検証対象の遷移定義。</param>
    /// <param name="errors">検出したエラーメッセージの蓄積先。</param>
    /// <param name="isDefaultTransition"><c>default</c> 配下の遷移を検証しているとき true。</param>
    private static void ValidateTransitionShape(
        string transitionPath,
        TransitionDefinition trans,
        List<string> errors,
        bool isDefaultTransition)
    {
        var hasNext = !string.IsNullOrWhiteSpace(trans.Next);
        var hasFork = trans.Fork is { Count: > 0 };
        var hasEnd = trans.End;
        var hasCases = trans.Cases is { Count: > 0 };
        var hasDefault = trans.Default is not null;
        var usesLinearForm = hasNext || trans.Fork is not null || hasEnd;
        var usesConditionalForm = trans.Cases is not null || hasDefault;
        var linearCount = (hasNext ? 1 : 0) + (hasFork ? 1 : 0) + (hasEnd ? 1 : 0);

        if (trans.Fork is { Count: 0 })
        {
            errors.Add($"Transition '{transitionPath}' has empty fork target list.");
        }

        if (trans.Cases is { Count: 0 })
        {
            errors.Add($"Transition '{transitionPath}' has empty cases list.");
        }

        if (isDefaultTransition)
        {
            ValidateDefaultTransitionShape(transitionPath, usesConditionalForm, linearCount, errors);
            return;
        }

        ValidateNonDefaultTransitionShape(
            transitionPath,
            hasNext,
            hasFork,
            hasEnd,
            hasCases,
            hasDefault,
            usesLinearForm,
            usesConditionalForm,
            linearCount,
            errors);
    }

    /// <summary><c>default</c> 遷移が線形形式のみで、next/fork/end のいずれか 1 つだけを持つことを検証する。</summary>
    /// <param name="transitionPath">エラーメッセージ用の遷移パス。</param>
    /// <param name="usesConditionalForm">cases または default が定義されているとき true。</param>
    /// <param name="linearCount">next / fork / end のうち定義されている件数。</param>
    /// <param name="errors">検出したエラーメッセージの蓄積先。</param>
    private static void ValidateDefaultTransitionShape(
        string transitionPath,
        bool usesConditionalForm,
        int linearCount,
        List<string> errors)
    {
        if (usesConditionalForm)
        {
            errors.Add($"Transition '{transitionPath}' must not include cases/default inside default transition.");
        }

        if (linearCount != 1)
        {
            errors.Add($"Transition '{transitionPath}' must define exactly one of next/fork/end.");
        }
    }

    /// <summary>通常遷移（default 以外）の形状制約を検証する。</summary>
    /// <param name="transitionPath">エラーメッセージ用の遷移パス。</param>
    /// <param name="hasNext"><c>next</c> が定義されているとき true。</param>
    /// <param name="hasFork">非空の <c>fork</c> が定義されているとき true。</param>
    /// <param name="hasEnd"><c>end: true</c> が定義されているとき true。</param>
    /// <param name="hasCases">非空の <c>cases</c> が定義されているとき true。</param>
    /// <param name="hasDefault"><c>default</c> が定義されているとき true。</param>
    /// <param name="usesLinearForm">next / fork / end のいずれかが定義されているとき true。</param>
    /// <param name="usesConditionalForm">cases または default が定義されているとき true。</param>
    /// <param name="linearCount">next / fork / end のうち定義されている件数。</param>
    /// <param name="errors">検出したエラーメッセージの蓄積先。</param>
    private static void ValidateNonDefaultTransitionShape(
        string transitionPath,
        bool hasNext,
        bool hasFork,
        bool hasEnd,
        bool hasCases,
        bool hasDefault,
        bool usesLinearForm,
        bool usesConditionalForm,
        int linearCount,
        List<string> errors)
    {
        if (usesLinearForm && usesConditionalForm)
        {
            errors.Add($"Transition '{transitionPath}' cannot mix next/fork/end with cases/default.");
        }

        if (!usesLinearForm && !usesConditionalForm)
        {
            errors.Add($"Transition '{transitionPath}' must define next/fork/end or cases/default.");
        }

        if (usesLinearForm && !usesConditionalForm && linearCount != 1)
        {
            errors.Add($"Transition '{transitionPath}' must define exactly one of next/fork/end.");
        }

        if (hasCases && !hasDefault)
        {
            errors.Add($"Transition '{transitionPath}' requires default when cases are defined.");
        }

        if (!hasCases && hasDefault)
        {
            errors.Add($"Transition '{transitionPath}' cannot define default without cases.");
        }

        if (hasEnd && (hasNext || hasFork))
        {
            errors.Add($"Transition '{transitionPath}' cannot combine end: true with next/fork.");
        }
    }

    /// <summary>遷移の参照先（next / fork / cases / default）を検証し、必要に応じて再帰する。</summary>
    /// <param name="stateName">遷移を所有する状態名。</param>
    /// <param name="transitionPath">エラーメッセージ用の遷移パス。</param>
    /// <param name="trans">検証対象の遷移定義。</param>
    /// <param name="stateNames">定義済み状態名の集合。</param>
    /// <param name="errors">検出したエラーメッセージの蓄積先。</param>
    /// <param name="isDefaultTransition"><c>default</c> 配下の遷移を検証しているとき true。</param>
    /// <param name="terminalTransitionCount"><c>end: true</c> 遷移の件数（ワークフロー全体で集計）。</param>
    private static void ValidateTransitionReferences(
        string stateName,
        string transitionPath,
        TransitionDefinition trans,
        HashSet<string> stateNames,
        List<string> errors,
        bool isDefaultTransition,
        ref int terminalTransitionCount)
    {
        if (!string.IsNullOrWhiteSpace(trans.Next))
        {
            ValidateNextPointer(stateName, trans.Next, stateNames, errors);
        }

        if (trans.Fork is not null)
        {
            ValidateForkTransition(transitionPath, trans.Fork, stateNames, errors);
        }

        if (trans.Cases is not null)
        {
            ValidateTransitionCases(
                stateName,
                transitionPath,
                trans.Cases,
                stateNames,
                errors,
                ref terminalTransitionCount);
        }

        if (trans.Default is not null)
        {
            ValidateTransitionTree(
                stateName,
                $"{transitionPath}.default",
                trans.Default,
                stateNames,
                errors,
                isDefaultTransition: true,
                ref terminalTransitionCount);
        }
    }

    /// <summary>条件分岐 <c>cases</c> の各要素の <c>when</c> とネスト遷移を検証する。</summary>
    /// <param name="stateName">遷移を所有する状態名。</param>
    /// <param name="transitionPath">エラーメッセージ用の遷移パス。</param>
    /// <param name="cases">検証対象の case 一覧。</param>
    /// <param name="stateNames">定義済み状態名の集合。</param>
    /// <param name="errors">検出したエラーメッセージの蓄積先。</param>
    /// <param name="terminalTransitionCount"><c>end: true</c> 遷移の件数（ワークフロー全体で集計）。</param>
    private static void ValidateTransitionCases(
        string stateName,
        string transitionPath,
        IReadOnlyList<TransitionCaseDefinition> cases,
        HashSet<string> stateNames,
        List<string> errors,
        ref int terminalTransitionCount)
    {
        for (var i = 0; i < cases.Count; i++)
        {
            var transitionCase = cases[i];
            ValidateCondition(transitionPath, i, transitionCase.When, errors);
            ValidateTransitionTree(
                stateName,
                $"{transitionPath}.cases[{i}]",
                transitionCase.Transition,
                stateNames,
                errors,
                isDefaultTransition: false,
                ref terminalTransitionCount);
        }
    }

    /// <summary><c>next</c> 先が自己遷移でなく、定義済み状態を指すことを検証する。</summary>
    /// <param name="stateName">遷移を所有する状態名。</param>
    /// <param name="next"><c>next</c> で指定された状態名。</param>
    /// <param name="stateNames">定義済み状態名の集合。</param>
    /// <param name="errors">検出したエラーメッセージの蓄積先。</param>
    private static void ValidateNextPointer(string stateName, string? next, HashSet<string> stateNames, List<string> errors)
    {
        if (next == null)
        {
            return;
        }

        if (next.Equals(stateName, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Self-transition not allowed: {stateName} -> {stateName}");
        }

        if (!stateNames.Contains(next))
        {
            errors.Add($"Reference to unknown state: {next}");
        }
    }

    /// <summary><c>fork</c> の各分岐先が定義済み状態であることを検証する。</summary>
    /// <param name="transitionPath">エラーメッセージ用の遷移パス。</param>
    /// <param name="forkStates">fork 先の状態名一覧。</param>
    /// <param name="stateNames">定義済み状態名の集合。</param>
    /// <param name="errors">検出したエラーメッセージの蓄積先。</param>
    private static void ValidateForkTransition(
        string transitionPath,
        IReadOnlyList<string> forkStates,
        HashSet<string> stateNames,
        List<string> errors)
    {
        foreach (var forkState in forkStates.Where(fs => !stateNames.Contains(fs)))
        {
            errors.Add($"Fork references unknown state: {forkState} (transition '{transitionPath}')");
        }
    }

    /// <summary>条件式 <c>when</c> の path / op / value の妥当性を検証する。</summary>
    /// <param name="transitionPath">エラーメッセージ用の遷移パス。</param>
    /// <param name="caseIndex">cases 配列内のインデックス。</param>
    /// <param name="condition">検証対象の条件式。</param>
    /// <param name="errors">検出したエラーメッセージの蓄積先。</param>
    private static void ValidateCondition(
        string transitionPath,
        int caseIndex,
        ConditionExpressionDefinition condition,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(condition.Path) || !SimpleJsonPath.IsValid(condition.Path))
        {
            errors.Add($"Transition '{transitionPath}.cases[{caseIndex}]' has invalid when.path: {condition.Path}");
        }

        if (string.IsNullOrWhiteSpace(condition.Op))
        {
            errors.Add($"Transition '{transitionPath}.cases[{caseIndex}]' requires non-empty when.op.");
            return;
        }

        if (!ConditionExpressionOperatorNormalizer.TryNormalize(condition.Op, out var op))
        {
            errors.Add(
                $"Transition '{transitionPath}.cases[{caseIndex}]' has unsupported when.op: '{condition.Op}'.");
            return;
        }

        switch (op)
        {
            case "EXISTS":
                if (condition.Value is not null)
                {
                    errors.Add(
                        $"Transition '{transitionPath}.cases[{caseIndex}]' with op 'exists' must not define value.");
                }

                break;
            case "BETWEEN":
                if (!TryGetCollectionItems(condition.Value, out var range) || range.Count != 2)
                {
                    errors.Add($"Transition '{transitionPath}.cases[{caseIndex}]' with op 'between' requires two-element array value.");
                }

                break;
            case "IN":
                if (!TryGetCollectionItems(condition.Value, out _))
                {
                    errors.Add($"Transition '{transitionPath}.cases[{caseIndex}]' with op 'in' requires array value.");
                }

                break;
        }
    }

    /// <summary>演算子 <c>between</c> / <c>in</c> 用に、値が列挙可能なコレクションかどうかを判定する。</summary>
    /// <param name="value">条件式の <c>value</c>。</param>
    /// <param name="items">列挙できた要素。失敗時は空リスト。</param>
    /// <returns>文字列以外の列挙可能オブジェクトとして解釈できたとき true。</returns>
    private static bool TryGetCollectionItems(object? value, out List<object?> items)
    {
        items = new List<object?>();
        if (value is null || value is string || value is not IEnumerable enumerable)
        {
            return false;
        }

        foreach (var item in enumerable)
        {
            items.Add(item);
        }

        return true;
    }

    /// <summary><c>join.allOf</c> の各依存状態が定義済みであることを検証する。</summary>
    /// <param name="stateDef">状態定義。</param>
    /// <param name="stateNames">定義済み状態名の集合。</param>
    /// <param name="errors">検出したエラーメッセージの蓄積先。</param>
    private static void ValidateJoin(StateDefinition stateDef, HashSet<string> stateNames, List<string> errors)
    {
        if (stateDef.Join == null)
        {
            return;
        }

        foreach (var joinState in stateDef.Join.AllOf.Where(js => !stateNames.Contains(js)))
        {
            errors.Add($"Join references unknown state: {joinState}");
        }
    }

    /// <summary>状態の <c>input</c> 指定（path または values）の妥当性を検証する。</summary>
    /// <param name="stateName">検証対象の状態名。</param>
    /// <param name="stateDef">状態定義。</param>
    /// <param name="errors">検出したエラーメッセージの蓄積先。</param>
    private static void ValidateStateInput(string stateName, StateDefinition stateDef, List<string> errors)
    {
        var m = stateDef.Input;
        if (m == null)
        {
            return;
        }

        if (m.Path != null)
        {
            if (!SimpleJsonPath.IsValid(m.Path))
            {
                errors.Add($"input.path is invalid for state '{stateName}': {m.Path}");
            }
            return;
        }

        if (m.Values == null || m.Values.Count == 0)
        {
            errors.Add($"input must define path or values: {stateName}");
            return;
        }

        foreach (var (key, valueDef) in m.Values)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add($"input key cannot be empty: {stateName}");
                continue;
            }

            if (valueDef.Path != null && !SimpleJsonPath.IsValid(valueDef.Path))
            {
                errors.Add($"input.path is invalid for state '{stateName}' key '{key}': {valueDef.Path}");
            }
        }
    }

}

/// <summary>検証結果。エラー一覧と有効フラグを保持します。</summary>
public sealed class ValidationResult
{
    /// <summary>検出されたエラーメッセージの一覧。</summary>
    public IReadOnlyList<string> Errors { get; }
    /// <summary>エラーが 0 件の場合 true。</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// 検証で収集したエラーメッセージで結果を構築する。
    /// </summary>
    /// <param name="errors">エラーメッセージの一覧。</param>
    public ValidationResult(IReadOnlyList<string> errors) => Errors = errors;
}
