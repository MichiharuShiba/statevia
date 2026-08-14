namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>
/// 状態名・遷移形状・Wait・Join 参照・input/output・終端遷移など、旧 Level1 の状態グラフ検査。
/// </summary>
internal sealed class StateGraphRule : IValidationRule
{
    /// <inheritdoc />
    public string RuleId => "Structural.StateGraph";

    /// <inheritdoc />
    public ValidationPhase Phase => ValidationPhase.Structural;

    /// <inheritdoc />
    public int Weight => 20;

    /// <inheritdoc />
    public void Validate(
        WorkflowDefinition definition,
        DefinitionValidationContext context,
        ICollection<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(issues);
        if (context.StateNames.Count == 0)
        {
            return;
        }

        var errors = new List<string>();
        Level1Validator.CollectStateGraphErrors(definition, errors);
        foreach (var message in errors)
        {
            issues.Add(new ValidationIssue(RuleId, message));
        }
    }
}
