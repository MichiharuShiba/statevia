namespace Statevia.Service.Api.Scenario.Tests.Infrastructure;

/// <summary>
/// シナリオテスト群が共有する xUnit コレクション。
/// </summary>
/// <remarks>
/// 全シナリオテストクラスに <c>[Collection(nameof(PostgresScenarioCollection))]</c> を付与することで、
/// PostgreSQL コンテナを 1 インスタンスのみ起動する。
/// </remarks>
[CollectionDefinition(nameof(PostgresScenarioCollection))]
public sealed class PostgresScenarioCollection : ICollectionFixture<PostgresScenarioFixture>;
