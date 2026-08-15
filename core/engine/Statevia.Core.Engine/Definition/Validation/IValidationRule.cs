namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>
/// 定義検証の 1 単位。オーケストレータはフェーズ順・Weight 順に列を回すだけとする。
/// </summary>
public interface IValidationRule
{
    /// <summary>安定 <c>code</c>。Fork 領域ルールは <c>ForkRegion.*</c>。</summary>
    string RuleId { get; }

    /// <summary>所属フェーズ。</summary>
    ValidationPhase Phase { get; }

    /// <summary>フェーズ内順（昇順。同値は <see cref="RuleId"/>）。</summary>
    int Weight { get; }

    /// <summary>
    /// 定義を検証し、検出した指摘を <paramref name="issues"/> へ追加する。
    /// </summary>
    /// <param name="definition">検証対象。</param>
    /// <param name="context">1 回の Validate で共有するスナップショット。</param>
    /// <param name="issues">指摘の蓄積先。</param>
    void Validate(
        WorkflowDefinition definition,
        DefinitionValidationContext context,
        ICollection<ValidationIssue> issues);
}
