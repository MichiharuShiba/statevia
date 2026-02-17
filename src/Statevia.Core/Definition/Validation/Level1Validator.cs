namespace Statevia.Core.Definition.Validation;

/// <summary>
/// レベル 1 検証：状態名・参照の整合性、自己遷移禁止、未定義状態参照の検出。
/// </summary>
public sealed class Level1Validator
{
    /// <summary>ワークフロー定義を検証し、エラー一覧を返します。</summary>
    public static ValidationResult Validate(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var errors = new List<string>();
        if (definition.States.Count == 0) { errors.Add("At least one state is required."); return new ValidationResult(errors); }
        var stateNames = new HashSet<string>(definition.States.Keys, StringComparer.OrdinalIgnoreCase);

        foreach (var (stateName, stateDef) in definition.States)
        {
            if (string.IsNullOrWhiteSpace(stateName)) { errors.Add("State name cannot be empty."); continue; }
            if (stateDef.On != null)
            {
                foreach (var (fact, trans) in stateDef.On)
                {
                    if (trans.Next != null)
                    {
                        if (trans.Next.Equals(stateName, StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add($"Self-transition not allowed: {stateName} -> {stateName}");
                        }
                        if (!stateNames.Contains(trans.Next))
                        {
                            errors.Add($"Reference to unknown state: {trans.Next}");
                        }
                    }
                    if (trans.Fork != null)
                    {
                        foreach (var forkState in trans.Fork.Where(fs => !stateNames.Contains(fs)))
                        {
                            errors.Add($"Fork references unknown state: {forkState}");
                        }
                    }
                }
            }
            if (stateDef.Join != null)
            {
                foreach (var joinState in stateDef.Join.AllOf.Where(js => !stateNames.Contains(js)))
                {
                    errors.Add($"Join references unknown state: {joinState}");
                }
            }
        }
        return new ValidationResult(errors);
    }
}

/// <summary>検証結果。エラー一覧と有効フラグを保持します。</summary>
public sealed class ValidationResult
{
    /// <summary>検出されたエラーメッセージの一覧。</summary>
    public IReadOnlyList<string> Errors { get; }
    /// <summary>エラーが 0 件の場合 true。</summary>
    public bool IsValid => Errors.Count == 0;
    public ValidationResult(IReadOnlyList<string> errors) => Errors = errors;
}
