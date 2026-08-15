namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>
/// <see cref="IValidationRule"/> 列をフェーズ順・Weight 順に実行する。
/// Structural / Reachability 失敗時は後続フェーズをスキップする。ForkRegion はフェーズ内全件収集。
/// </summary>
internal static class ValidationPipeline
{
    internal static readonly IReadOnlyList<IValidationRule> Rules = CreateRules();

    /// <summary>
    /// 指定フェーズだけ、または全フェーズを実行する。
    /// </summary>
    /// <param name="definition">検証対象。</param>
    /// <param name="onlyPhase">指定時はそのフェーズのみ（Level1/Level2 互換）。null なら全フェーズ。</param>
    /// <returns>検証結果。</returns>
    public static ValidationResult Run(WorkflowDefinition definition, ValidationPhase? onlyPhase)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var context = new DefinitionValidationContext(definition);
        var issues = new List<ValidationIssue>();
        var phases = onlyPhase is { } single
            ? [single]
            : new[] { ValidationPhase.Structural, ValidationPhase.Reachability, ValidationPhase.ForkRegion };

        foreach (var phase in phases)
        {
            var phaseIssues = new List<ValidationIssue>();
            foreach (var rule in RulesFor(phase))
            {
                rule.Validate(definition, context, phaseIssues);
            }

            issues.AddRange(phaseIssues);
            if (onlyPhase is null
                && phase != ValidationPhase.ForkRegion
                && phaseIssues.Count > 0)
            {
                break;
            }
        }

        return new ValidationResult(issues);
    }

    private static IEnumerable<IValidationRule> RulesFor(ValidationPhase phase) =>
        Rules
            .Where(r => r.Phase == phase)
            .OrderBy(static r => r.Weight)
            .ThenBy(static r => r.RuleId, StringComparer.Ordinal);

    private static IReadOnlyList<IValidationRule> CreateRules() =>
    [
        new RequiredStatesRule(),
        new StateGraphRule(),
        new UnreachableStatesRule(),
        new CircularJoinRule(),
        new ForkRegionSnapshotRule(),
        new ForkRegionIngressRule(),
        new ForkRegionEgressRule(),
        new ForkRegionCrossBranchRule(),
        new ForkRegionNestedCrossRule(),
        new ForkRegionJoinNotFedRule(),
        new ForkRegionAmbiguousJoinRule(),
        new ForkRegionBranchEntryRule(),
        new ForkRegionMissingJoinRule(),
        new ForkRegionSiblingStateRefRule(),
        new ForkRegionWaitTargetRule()
    ];
}
