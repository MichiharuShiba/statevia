using Statevia.Core.Api.Persistence.Repositories;
using Statevia.Core.Api.Services;

namespace Statevia.Core.Api.Tests.Infrastructure;

/// <summary>本番と同構成の Repository スタックを生成する。</summary>
internal static class TestRepositoryFactory
{
    /// <summary>project 認可付き <see cref="DefinitionRepository"/>。</summary>
    public static DefinitionRepository CreateDefinitionRepository() =>
        new(new ProjectAuthorizationService(new ProjectRepository()));
}
