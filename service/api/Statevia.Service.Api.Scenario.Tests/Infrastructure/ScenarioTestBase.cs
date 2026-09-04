using Xunit.Abstractions;

namespace Statevia.Service.Api.Scenario.Tests.Infrastructure;

/// <summary>
/// シナリオテストの基底クラス。
/// </summary>
/// <remarks>
/// Docker 未検出時に <see cref="Skip.IfNot"/> で全テストをスキップする共通処理を提供する。
/// </remarks>
public abstract class ScenarioTestBase
{
    /// <summary>フィクスチャ（コンテナ・ファクトリ・接続文字列）。</summary>
    protected PostgresScenarioFixture Fixture { get; }

    /// <summary>テスト出力ヘルパー。</summary>
    protected ITestOutputHelper Output { get; }

    /// <summary>シナリオテスト基底クラスを初期化する。</summary>
    /// <param name="fixture">PostgreSQL コンテナフィクスチャ。</param>
    /// <param name="output">xUnit テスト出力ヘルパー。</param>
    protected ScenarioTestBase(PostgresScenarioFixture fixture, ITestOutputHelper output)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(output);
        Fixture = fixture;
        Output = output;
    }

    /// <summary>
    /// Docker が利用できない場合にテストをスキップする。
    /// </summary>
    /// <remarks>
    /// 各テストメソッドの冒頭で呼ぶことで、Docker 未起動環境での Failed を防ぐ。
    /// </remarks>
    protected void SkipIfDockerUnavailable()
    {
        Skip.IfNot(Fixture.IsReady, Fixture.SkipReason ?? "Docker が利用できません。");
    }
}
