namespace Statevia.Core.Definition.Validation;

/// <summary>
/// Level1Validator と Level2Validator を集約し、ワークフロー定義を一括検証します。
/// Level1（状態名・参照の整合性）→ Level2（到達可能性・循環 Join）の順で実行します。
/// </summary>
public static class DefinitionValidator
{
    /// <summary>ワークフロー定義を検証し、エラー一覧を返します。Level1 通過後に Level2 を実行します。</summary>
    public static ValidationResult Validate(WorkflowDefinition definition)
    {
        var level1Result = Level1Validator.Validate(definition);
        if (!level1Result.IsValid)
        {
            return level1Result;
        }

        var level2Result = Level2Validator.Validate(definition);
        if (!level2Result.IsValid)
        {
            return level2Result;
        }

        return level1Result;
    }
}
