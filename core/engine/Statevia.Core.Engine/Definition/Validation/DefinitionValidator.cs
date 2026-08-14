namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>
/// <see cref="IValidationRule"/> 列をフェーズ順に実行するオーケストレータ。
/// Structural（旧 Level1）→ Reachability（旧 Level2）→ ForkRegion の順。先行フェーズ失敗時は後続をスキップする。
/// </summary>
public static class DefinitionValidator
{
    /// <summary>
    /// ワークフロー定義を検証し、エラー一覧を返します。
    /// </summary>
    /// <param name="definition">検証対象。</param>
    /// <returns>指摘付きの検証結果。</returns>
    public static ValidationResult Validate(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return ValidationPipeline.Run(definition, onlyPhase: null);
    }
}
