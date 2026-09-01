namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>Fork 領域スナップショットをコンテキストへ載せる（指摘は出さない）。</summary>
internal sealed class ForkRegionSnapshotRule : IValidationRule
{
    /// <inheritdoc />
    public string RuleId => "ForkRegion.Snapshot";

    /// <inheritdoc />
    public ValidationPhase Phase => ValidationPhase.ForkRegion;

    /// <inheritdoc />
    public int Weight => 0;

    /// <inheritdoc />
    public void Validate(
        WorkflowDefinition definition,
        DefinitionValidationContext context,
        ICollection<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);
        context.ForkRegion = ForkRegionSnapshotBuilder.Build(definition);
        _ = issues;
    }
}

/// <summary>領域外から枝 Body への侵入。</summary>
internal sealed class ForkRegionIngressRule : IValidationRule
{
    /// <inheritdoc />
    public string RuleId => "ForkRegion.IngressFromOutside";

    /// <inheritdoc />
    public ValidationPhase Phase => ValidationPhase.ForkRegion;

    /// <inheritdoc />
    public int Weight => 10;

    /// <inheritdoc />
    public void Validate(
        WorkflowDefinition definition,
        DefinitionValidationContext context,
        ICollection<ValidationIssue> issues)
    {
        if (context.ForkRegion is not { } snap)
        {
            return;
        }

        ForkRegionRuleSupport.AddAll(
            issues,
            snap.Edges
                .Where(e => !ForkRegionRuleSupport.IsLegalForkEntry(snap, e))
                .Where(e => ForkRegionRuleSupport.LocateBody(snap, e.To) is not null
                    && ForkRegionRuleSupport.LocateBody(snap, e.From) is null)
                .Select(e => new ValidationIssue(
                    RuleId,
                    $"Fork region ingress from '{e.From}' into '{e.To}'.",
                    e.To)));
    }
}

/// <summary>Join 経由なしの領域外脱出（Failed 辺を含む。Failed は Join 供給に数えない）。</summary>
internal sealed class ForkRegionEgressRule : IValidationRule
{
    /// <inheritdoc />
    public string RuleId => "ForkRegion.EgressWithoutJoin";

    /// <inheritdoc />
    public ValidationPhase Phase => ValidationPhase.ForkRegion;

    /// <inheritdoc />
    public int Weight => 20;

    /// <inheritdoc />
    public void Validate(
        WorkflowDefinition definition,
        DefinitionValidationContext context,
        ICollection<ValidationIssue> issues)
    {
        if (context.ForkRegion is not { } snap)
        {
            return;
        }

        ForkRegionRuleSupport.AddAll(
            issues,
            snap.Edges
                .Where(e => !ForkRegionRuleSupport.IsLegalForkEntry(snap, e))
                .Select(e => (Edge: e, From: ForkRegionRuleSupport.LocateBody(snap, e.From)))
                .Where(x => x.From is { } from
                    && !ForkRegionRuleSupport.IsLegalExit(snap, from.Pair, from.BranchHead, x.Edge.To))
                .Select(x => new ValidationIssue(
                    RuleId,
                    $"Fork region egress from '{x.Edge.From}' to '{x.Edge.To}' without join '{x.From!.Value.Pair.JoinState}'.",
                    x.Edge.From)));
    }
}

/// <summary>他枝 Body 横断（同一 Fork 兄弟を含む）。</summary>
internal sealed class ForkRegionCrossBranchRule : IValidationRule
{
    /// <inheritdoc />
    public string RuleId => "ForkRegion.CrossBranch";

    /// <inheritdoc />
    public ValidationPhase Phase => ValidationPhase.ForkRegion;

    /// <inheritdoc />
    public int Weight => 30;

    /// <inheritdoc />
    public void Validate(
        WorkflowDefinition definition,
        DefinitionValidationContext context,
        ICollection<ValidationIssue> issues)
    {
        if (context.ForkRegion is not { } snap)
        {
            return;
        }

        ForkRegionRuleSupport.AddAll(
            issues,
            snap.Pairs.SelectMany(pair => pair.BranchBodies
                .SelectMany(kv => kv.Value.Select(n => (pair.ForkState, Head: kv.Key, Node: n)))
                .GroupBy(static x => x.Node, StringComparer.OrdinalIgnoreCase)
                .Where(static g => g.Select(x => x.Head).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                .Select(g => new ValidationIssue(
                    RuleId,
                    $"Fork '{g.First().ForkState}' branch bodies share node '{g.Key}'.",
                    g.Key))));

        ForkRegionRuleSupport.AddAll(
            issues,
            snap.Edges
                .Select(e => (
                    Edge: e,
                    From: ForkRegionRuleSupport.LocateBody(snap, e.From),
                    To: ForkRegionRuleSupport.LocateBody(snap, e.To)))
                .Where(x => x.From is { } from && x.To is { } to
                    && string.Equals(from.Pair.ForkState, to.Pair.ForkState, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(from.BranchHead, to.BranchHead, StringComparison.OrdinalIgnoreCase))
                .Select(x => new ValidationIssue(
                    RuleId,
                    $"Cross-branch edge '{x.Edge.From}' → '{x.Edge.To}' in fork '{x.From!.Value.Pair.ForkState}'.",
                    x.Edge.From)));
    }
}

/// <summary>内側 Body と外側の別枝との横断。</summary>
internal sealed class ForkRegionNestedCrossRule : IValidationRule
{
    /// <inheritdoc />
    public string RuleId => "ForkRegion.NestedCrossBranch";

    /// <inheritdoc />
    public ValidationPhase Phase => ValidationPhase.ForkRegion;

    /// <inheritdoc />
    public int Weight => 40;

    /// <inheritdoc />
    public void Validate(
        WorkflowDefinition definition,
        DefinitionValidationContext context,
        ICollection<ValidationIssue> issues)
    {
        if (context.ForkRegion is not { } snap)
        {
            return;
        }

        ForkRegionRuleSupport.AddAll(issues, snap.Edges.SelectMany(e => IssuesFor(snap, e)));
    }

    /// <summary>1 辺について NestedCross 指摘を返す。</summary>
    private IEnumerable<ValidationIssue> IssuesFor(ForkRegionSnapshot snap, ControlEdge edge)
    {
        if (ForkRegionRuleSupport.LocateBody(snap, edge.From) is not { } from
            || ForkRegionRuleSupport.LocateBody(snap, edge.To) is not { } to)
        {
            return [];
        }

        if (string.Equals(from.Pair.ForkState, to.Pair.ForkState, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return IssuesForDistinctPairs(snap, edge, from, to);
    }

    /// <summary>異なる Fork ペア間の横断指摘。</summary>
    private IEnumerable<ValidationIssue> IssuesForDistinctPairs(
        ForkRegionSnapshot snap,
        ControlEdge edge,
        BodyLocation from,
        BodyLocation to)
    {
        var fromIsInner = ForkRegionSnapshotBuilder.SupplyReaches(
            snap.Outgoing, from.Pair.JoinState, to.Pair.JoinState);
        var toIsInner = ForkRegionSnapshotBuilder.SupplyReaches(
            snap.Outgoing, to.Pair.JoinState, from.Pair.JoinState);
        if (!fromIsInner && !toIsInner)
        {
            return
            [
                new ValidationIssue(
                    RuleId,
                    $"Nested or parallel region cross '{edge.From}' → '{edge.To}'.",
                    edge.From)
            ];
        }

        return InnerOuterIssues(snap, edge, from, to, fromIsInner, toIsInner);
    }

    /// <summary>入れ子の内外横断。</summary>
    private IEnumerable<ValidationIssue> InnerOuterIssues(
        ForkRegionSnapshot snap,
        ControlEdge edge,
        BodyLocation from,
        BodyLocation to,
        bool fromIsInner,
        bool toIsInner)
    {
        if (fromIsInner && !string.Equals(edge.To, from.Pair.ForkState, StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationIssue(
                RuleId,
                $"Inner region '{from.Pair.ForkState}' crosses to outer node '{edge.To}'.",
                edge.From);
        }

        if (toIsInner && !ForkRegionRuleSupport.IsLegalForkEntry(snap, edge)
            && !string.Equals(edge.From, to.Pair.ForkState, StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationIssue(
                RuleId,
                $"Outer node '{edge.From}' enters inner region '{to.Pair.ForkState}' at '{edge.To}'.",
                edge.From);
        }
    }
}

/// <summary>Join-all が供給経路を持たない。</summary>
internal sealed class ForkRegionJoinNotFedRule : IValidationRule
{
    /// <inheritdoc />
    public string RuleId => "ForkRegion.JoinNotFed";

    /// <inheritdoc />
    public ValidationPhase Phase => ValidationPhase.ForkRegion;

    /// <inheritdoc />
    public int Weight => 50;

    /// <inheritdoc />
    public void Validate(
        WorkflowDefinition definition,
        DefinitionValidationContext context,
        ICollection<ValidationIssue> issues)
    {
        if (context.ForkRegion is not { } snap)
        {
            return;
        }

        ForkRegionRuleSupport.AddAll(
            issues,
            snap.UnpairedJoins.Select(joinState => new ValidationIssue(
                RuleId,
                $"Join '{joinState}' is not paired with a fork branch set.",
                joinState)));

        ForkRegionRuleSupport.AddAll(
            issues,
            snap.Pairs.SelectMany(pair => pair.Branches
                .Where(head => !ForkRegionSnapshotBuilder.SupplyReaches(snap.Outgoing, head, pair.JoinState))
                .Select(head => new ValidationIssue(
                    RuleId,
                    $"Branch '{head}' does not supply join '{pair.JoinState}'.",
                    head))));
    }
}

/// <summary>非入れ子の複数 Fork が同一 Join を供給する。</summary>
internal sealed class ForkRegionAmbiguousJoinRule : IValidationRule
{
    /// <inheritdoc />
    public string RuleId => "ForkRegion.AmbiguousJoin";

    /// <inheritdoc />
    public ValidationPhase Phase => ValidationPhase.ForkRegion;

    /// <inheritdoc />
    public int Weight => 60;

    /// <inheritdoc />
    public void Validate(
        WorkflowDefinition definition,
        DefinitionValidationContext context,
        ICollection<ValidationIssue> issues)
    {
        if (context.ForkRegion is not { } snap)
        {
            return;
        }

        ForkRegionRuleSupport.AddAll(
            issues,
            snap.AmbiguousJoins.Select(item => new ValidationIssue(
                RuleId,
                $"Join '{item.JoinState}' is supplied by multiple forks: {string.Join(", ", item.ForkStates)}.",
                item.JoinState)));
    }
}

/// <summary>枝先頭への Fork 以外の入口。</summary>
internal sealed class ForkRegionBranchEntryRule : IValidationRule
{
    /// <inheritdoc />
    public string RuleId => "ForkRegion.BranchHeadExtraEntry";

    /// <inheritdoc />
    public ValidationPhase Phase => ValidationPhase.ForkRegion;

    /// <inheritdoc />
    public int Weight => 70;

    /// <inheritdoc />
    public void Validate(
        WorkflowDefinition definition,
        DefinitionValidationContext context,
        ICollection<ValidationIssue> issues)
    {
        if (context.ForkRegion is not { } snap)
        {
            return;
        }

        ForkRegionRuleSupport.AddAll(
            issues,
            snap.Pairs.SelectMany(pair => pair.Branches.SelectMany(head =>
                IncomingOf(snap, head)
                    .Where(e => !ForkRegionRuleSupport.IsLegalForkEntry(snap, e))
                    .Select(e => new ValidationIssue(
                        RuleId,
                        $"Branch head '{head}' has extra entry from '{e.From}'.",
                        head)))));
    }

    private static IReadOnlyList<ControlEdge> IncomingOf(ForkRegionSnapshot snap, string head) =>
        snap.Incoming.TryGetValue(head, out var incoming) ? incoming : [];
}

/// <summary>対応 Join が無い Fork。</summary>
internal sealed class ForkRegionMissingJoinRule : IValidationRule
{
    /// <inheritdoc />
    public string RuleId => "ForkRegion.MissingJoin";

    /// <inheritdoc />
    public ValidationPhase Phase => ValidationPhase.ForkRegion;

    /// <inheritdoc />
    public int Weight => 80;

    /// <inheritdoc />
    public void Validate(
        WorkflowDefinition definition,
        DefinitionValidationContext context,
        ICollection<ValidationIssue> issues)
    {
        if (context.ForkRegion is not { } snap)
        {
            return;
        }

        ForkRegionRuleSupport.AddAll(
            issues,
            snap.UnpairedForks.Select(forkState => new ValidationIssue(
                RuleId,
                $"Fork '{forkState}' has no matching join (branch set as join.all).",
                forkState)));
    }
}

/// <summary>Join 前の他枝 <c>$.states</c> リテラル。</summary>
internal sealed class ForkRegionSiblingStateRefRule : IValidationRule
{
    /// <inheritdoc />
    public string RuleId => "ForkRegion.SiblingStateRef";

    /// <inheritdoc />
    public ValidationPhase Phase => ValidationPhase.ForkRegion;

    /// <inheritdoc />
    public int Weight => 90;

    /// <inheritdoc />
    public void Validate(
        WorkflowDefinition definition,
        DefinitionValidationContext context,
        ICollection<ValidationIssue> issues)
    {
        if (context.ForkRegion is not { } snap)
        {
            return;
        }

        ForkRegionRuleSupport.AddAll(
            issues,
            definition.States
                .Select(kv => (Name: kv.Key, Def: kv.Value, Loc: ForkRegionRuleSupport.LocateBody(snap, kv.Key)))
                .Where(x => x.Loc is not null)
                .SelectMany(x => ForkRegionRuleSupport.EnumerateStatesPathRefs(x.Def)
                    .Select(referenced => (x.Name, Loc: x.Loc!.Value, Referenced: referenced))
                    .Where(y => ForkRegionRuleSupport.LocateBody(snap, y.Referenced) is { } other
                        && string.Equals(y.Loc.Pair.ForkState, other.Pair.ForkState, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(y.Loc.BranchHead, other.BranchHead, StringComparison.OrdinalIgnoreCase))
                    .Select(y => new ValidationIssue(
                        RuleId,
                        $"State '{y.Name}' references sibling '{y.Referenced}' before join.",
                        y.Name))));
    }
}

/// <summary>枝内 Wait の遷移先が領域外または他枝。</summary>
internal sealed class ForkRegionWaitTargetRule : IValidationRule
{
    /// <inheritdoc />
    public string RuleId => "ForkRegion.WaitTargetOutside";

    /// <inheritdoc />
    public ValidationPhase Phase => ValidationPhase.ForkRegion;

    /// <inheritdoc />
    public int Weight => 100;

    /// <inheritdoc />
    public void Validate(
        WorkflowDefinition definition,
        DefinitionValidationContext context,
        ICollection<ValidationIssue> issues)
    {
        if (context.ForkRegion is not { } snap)
        {
            return;
        }

        ForkRegionRuleSupport.AddAll(
            issues,
            snap.Edges
                .Where(static e => e.Kind is ControlEdgeKind.WaitEvent or ControlEdgeKind.Subscribe)
                .Select(e => (Edge: e, From: ForkRegionRuleSupport.LocateBody(snap, e.From)))
                .Where(x => x.From is { } from
                    && !ForkRegionRuleSupport.IsLegalExit(snap, from.Pair, from.BranchHead, x.Edge.To))
                .Select(x => new ValidationIssue(
                    RuleId,
                    $"Wait target '{x.Edge.To}' from '{x.Edge.From}' leaves fork region of '{x.From!.Value.Pair.ForkState}'.",
                    x.Edge.From)));
    }
}

/// <summary>Fork 領域ルールの共有判定。</summary>
internal static class ForkRegionRuleSupport
{
    /// <summary>Fork の branches 辺かどうか。</summary>
    public static bool IsLegalForkEntry(ForkRegionSnapshot snap, ControlEdge edge) =>
        snap.Pairs.Any(p =>
            string.Equals(p.ForkState, edge.From, StringComparison.OrdinalIgnoreCase)
            && p.Branches.Contains(edge.To, StringComparer.OrdinalIgnoreCase));

    /// <summary>ノードが属するペアと枝先頭。領域外なら null。</summary>
    public static BodyLocation? LocateBody(ForkRegionSnapshot snap, string node)
    {
        var hit = snap.Pairs
            .SelectMany(pair => pair.BranchBodies.Select(body => (pair, Head: body.Key, Body: body.Value)))
            .FirstOrDefault(x => x.Body.Contains(node));
        return hit.pair is null ? null : new BodyLocation(hit.pair, hit.Head);
    }

    /// <summary>指摘を蓄積する。</summary>
    public static void AddAll(ICollection<ValidationIssue> issues, IEnumerable<ValidationIssue> found)
    {
        foreach (var issue in found)
        {
            issues.Add(issue);
        }
    }

    /// <summary>同一 Body・当該 Join・入れ子 Fork 開始なら合法出口。</summary>
    public static bool IsLegalExit(ForkRegionSnapshot snap, ForkJoinPair pair, string fromHead, string to)
    {
        if (string.Equals(to, pair.JoinState, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (pair.BranchBodies.TryGetValue(fromHead, out var body) && body.Contains(to))
        {
            return true;
        }

        return snap.Pairs.Any(p =>
            string.Equals(p.ForkState, to, StringComparison.OrdinalIgnoreCase)
            && ForkRegionSnapshotBuilder.SupplyReaches(snap.Outgoing, p.JoinState, pair.JoinState));
    }

    /// <summary><c>input</c> 上の <c>$.states…</c> リテラルが指す状態名。</summary>
    public static IEnumerable<string> EnumerateStatesPathRefs(StateDefinition stateDef)
    {
        var fromPath = stateDef.Input?.Path is { } path
            ? ExtractStatesNames(path)
            : [];
        var fromValues = stateDef.Input?.Values?.Values
            .Select(static v => v.Path)
            .OfType<string>()
            .SelectMany(ExtractStatesNames) ?? [];
        return fromPath.Concat(fromValues);
    }

    private static IEnumerable<string> ExtractStatesNames(string path)
    {
        const string prefix = "$.states";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var rest = path[prefix.Length..];
        if (rest.StartsWith("['", StringComparison.Ordinal) || rest.StartsWith("[\"", StringComparison.Ordinal))
        {
            var quote = rest[1];
            var end = rest.IndexOf(quote, 2);
            if (end > 2)
            {
                yield return rest[2..end];
            }

            yield break;
        }

        if (rest.Length > 1 && rest[0] == '.')
        {
            var name = rest[1..].Split('.')[0];
            if (name.Length > 0)
            {
                yield return name;
            }
        }
    }
}
