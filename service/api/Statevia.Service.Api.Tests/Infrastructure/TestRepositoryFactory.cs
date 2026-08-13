using Statevia.Infrastructure.Persistence.Repositories;

namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>本番と同構成の Repository スタックを生成する。</summary>
internal static class TestRepositoryFactory
{
    /// <summary>認可なしの <see cref="DefinitionRepository"/>（Infrastructure Persistence）。</summary>
    public static DefinitionRepository CreateDefinitionRepository() => new();
}
