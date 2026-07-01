using Statevia.Service.Api.Persistence.Repositories;
using Statevia.Service.Api.Services;

namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>本番と同構成の Repository スタックを生成する。</summary>
internal static class TestRepositoryFactory
{
    /// <summary>project 認可付き <see cref="DefinitionRepository"/>。</summary>
    public static DefinitionRepository CreateDefinitionRepository() =>
        new(new ProjectAuthorizationService(new ProjectRepository()));
}
