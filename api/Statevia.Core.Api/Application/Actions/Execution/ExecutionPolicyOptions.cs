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
}
