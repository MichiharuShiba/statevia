namespace Statevia.Core.Definition.Validation;

/// <summary>
/// Level1Validator と Level2Validator を集約し、ワークフロー定義を一括検証します。
/// Level1（状態名・参照の整合性）→ Level2（到達可能性・循環 Join）の順で実行します。
/// </summary>
public sealed class DefinitionValidator
{
    private readonly Level1Validator _level1 = new();
    private readonly Level2Validator _level2 = new();

    /// <summary>ワークフロー定義を検証し、エラー一覧を返します。Level1 通過後に Level2 を実行します。</summary>
    public ValidationResult Validate(WorkflowDefinition definition)
    {
        var level1Result = _level1.Validate(definition);
        if (!level1Result.IsValid)
            return level1Result;

        var level2Result = _level2.Validate(definition);
        if (!level2Result.IsValid)
            return level2Result;

        return level1Result;
    }
}
