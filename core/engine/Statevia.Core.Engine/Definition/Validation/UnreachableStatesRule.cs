namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>初期状態から到達できない状態を検出する。</summary>
internal sealed class UnreachableStatesRule : IValidationRule
{
    /// <inheritdoc />
    public string RuleId => "Reachability.UnreachableStates";

    /// <inheritdoc />
    public ValidationPhase Phase => ValidationPhase.Reachability;

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

        var initialState = Level2Validator.FindInitialState(definition);
        context.InitialState = initialState;
        if (initialState == null)
        {
            issues.Add(new ValidationIssue(RuleId, "Could not determine initial state."));
            return;
        }

        var reachable = Level2Validator.ComputeReachableStates(definition, initialState);
        context.ReachableStates = reachable;
        var unreachable = context.StateNames.Except(reachable, StringComparer.OrdinalIgnoreCase).ToList();
        if (unreachable.Count > 0)
        {
            issues.Add(new ValidationIssue(
                RuleId,
                $"Unreachable states: {string.Join(", ", unreachable)}"));
        }
    }
}
