namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>Join 同士の循環待機を検出する。</summary>
internal sealed class CircularJoinRule : IValidationRule
{
    /// <inheritdoc />
    public string RuleId => "Reachability.CircularJoin";

    /// <inheritdoc />
    public ValidationPhase Phase => ValidationPhase.Reachability;

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
        if (context.InitialState == null)
        {
            return;
        }

        foreach (var (stateName, stateDef) in definition.States)
        {
            if (stateDef.Join != null
                && Level2Validator.HasCircularJoin(
                    definition,
                    stateName,
                    stateDef.Join.All,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
            {
                issues.Add(new ValidationIssue(
                    RuleId,
                    $"Circular join detected involving: {stateName}",
                    stateName));
            }
        }
    }
}
