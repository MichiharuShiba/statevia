namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>状態が 1 件以上あることを検証する。</summary>
internal sealed class RequiredStatesRule : IValidationRule
{
    /// <inheritdoc />
    public string RuleId => "Structural.AtLeastOneState";

    /// <inheritdoc />
    public ValidationPhase Phase => ValidationPhase.Structural;

    /// <inheritdoc />
    public int Weight => 10;

    /// <inheritdoc />
    public void Validate(
        WorkflowDefinition definition,
        DefinitionValidationContext context,
        ICollection<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(issues);
        if (definition.States.Count == 0)
        {
            issues.Add(new ValidationIssue(RuleId, "At least one state is required."));
        }
    }
}
