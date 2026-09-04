namespace Statevia.Service.Api.Scenario.Tests.Infrastructure;

/// <summary>
/// シナリオテスト用のワークフロー定義 YAML フィクスチャ。
/// </summary>
/// <remarks>
/// Cancel シナリオ用に <c>wait.event</c> で停止する最小ワークフローを提供する。
/// ポーリング中に Cancel することで <c>Cancelled</c> ステータスへ遷移させる。
/// </remarks>
public static class WorkflowYamlFixtures
{
    /// <summary>
    /// <c>wait.event</c> で無期限停止する最小ワークフロー定義 YAML を返す。
    /// </summary>
    /// <param name="nameSuffix">定義名に付与する一意サフィックス（Guid 推奨）。</param>
    /// <returns>YAML 文字列。</returns>
    public static string WaitWorkflow(string nameSuffix) => $"""
        workflow:
          name: scenario-wait-{nameSuffix}
        states:
          Start:
            on:
              Completed:
                next: WaitNode
          WaitNode:
            wait:
              event: ResumeEvt
            on:
              Completed:
                next: End
          End:
            on:
              Completed:
                end: true
        """;
}
