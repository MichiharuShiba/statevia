namespace Statevia.Core.Api.Application.Actions.Execution;

/// <summary>Execution Policy の設定。</summary>
internal sealed class ExecutionPolicyOptions
{
    /// <summary>設定セクション名。</summary>
    public const string SectionName = "Statevia:ExecutionPolicy";

    /// <summary>
    /// デプロイプロファイル（例: <c>saas-shared</c>）。
    /// 実行コンテキストの DeploymentProfile 未指定時の既定値。
    /// </summary>
    public string? DeploymentProfile { get; set; }

    /// <summary>
    /// Mode 別の Backend 選択指定（キー = <see cref="Statevia.Actions.Abstractions.Execution.ActionExecutionMode"/> 名、値 = ProviderKey）。
    /// 同一 Mode に複数 Backend が登録されている場合の明示選択に用いる。単一登録時は不要。
    /// </summary>
    public Dictionary<string, string> Backends { get; set; } = new();
}
