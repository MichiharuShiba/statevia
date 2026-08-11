using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Core.Engine.Definition;
using Statevia.Service.Api.Application.Actions;
using Statevia.Service.Api.Application.Actions.Catalog;
using Statevia.Service.Api.Application.Actions.Versioning;
using Statevia.Service.Api.Application.Definition;

namespace Statevia.Service.Api.Tests.Application.Definition;

/// <summary><see cref="StoredDefinitionBindingResolver"/> の単体テスト。</summary>
public sealed class StoredDefinitionBindingResolverTests
{
    private const string CompiledJsonWithoutBindings = """
        {
          "name": "W",
          "initialState": "A",
          "transitions": {}
        }
        """;

    /// <summary>保存済みピン版がロード済みなら bindings をそのまま返す。</summary>
    [Fact]
    public void Resolve_WhenStoredBindingsPresentAndPinnedVersionLoaded_ReturnsStoredBindings()
    {
        // Arrange
        var catalog = CreateCatalog("demo.module", ["1.0.0"]);
        var definition = CreateDefinition(("A", ActionState("demo.module.echo")));
        var compiledJson = """
            {
              "name": "W",
              "initialState": "A",
              "transitions": {},
              "resolvedModules": {
                "mail": { "moduleId": "demo.module", "resolvedVersion": "1.0.0" }
              },
              "stateActionBindings": {
                "A": {
                  "logicalActionId": "demo.module.echo",
                  "resolvedModuleVersion": "1.0.0",
                  "moduleId": "demo.module",
                  "actionName": "echo"
                }
              }
            }
            """;

        // Act
        var result = StoredDefinitionBindingResolver.Resolve(definition, compiledJson, catalog);

        // Assert
        Assert.Equal("1.0.0", result.StateActionBindings["A"].ResolvedModuleVersion);
        Assert.Equal("demo.module", result.StateActionBindings["A"].ModuleId);
    }

    /// <summary>ピン版が未ロードなら MigrationRequired になる。</summary>
    [Fact]
    public void Resolve_WhenStoredPinnedVersionNotLoaded_ThrowsDefinitionMigrationRequiredException()
    {
        // Arrange
        var catalog = CreateCatalog("demo.module", ["1.0.0"]);
        var definition = CreateDefinition(("A", ActionState("demo.module.echo")));
        var compiledJson = """
            {
              "name": "W",
              "initialState": "A",
              "transitions": {},
              "stateActionBindings": {
                "A": {
                  "logicalActionId": "demo.module.echo",
                  "resolvedModuleVersion": "9.9.9",
                  "moduleId": "demo.module",
                  "actionName": "echo"
                }
              }
            }
            """;

        // Act + Assert
        var ex = Assert.Throws<DefinitionMigrationRequiredException>(
            () => StoredDefinitionBindingResolver.Resolve(definition, compiledJson, catalog));
        Assert.Contains("9.9.9", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>bindings が無いときは Legacy Bind にフォールバックする。</summary>
    [Fact]
    public void Resolve_WhenNoStoredBindings_FallsBackToLegacyBind()
    {
        // Arrange
        var catalog = CreateCatalog("demo.module", ["1.2.3"]);
        var definition = CreateDefinition(("A", ActionState("demo.module.echo")));

        // Act
        var result = StoredDefinitionBindingResolver.Resolve(
            definition,
            CompiledJsonWithoutBindings,
            catalog);

        // Assert
        Assert.Equal("1.2.3", result.StateActionBindings["A"].ResolvedModuleVersion);
    }

    /// <summary>Legacy で Module に複数版があると MigrationRequired になる。</summary>
    [Fact]
    public void Resolve_WhenLegacyFqcnHasMultipleVersions_ThrowsDefinitionMigrationRequiredException()
    {
        // Arrange
        var catalog = CreateCatalog("demo.module", ["1.0.0", "2.0.0"]);
        var definition = CreateDefinition(("A", ActionState("demo.module.echo")));

        // Act + Assert
        var ex = Assert.Throws<DefinitionMigrationRequiredException>(
            () => StoredDefinitionBindingResolver.Resolve(
                definition,
                CompiledJsonWithoutBindings,
                catalog));
        Assert.Contains("multiple loaded versions", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>引数検証: null / 空白で例外になる。</summary>
    [Fact]
    public void Resolve_InvalidArguments_Throw()
    {
        // Arrange
        var catalog = new InMemoryActionCatalog();
        var definition = CreateDefinition(("A", ActionState("noop")));

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => StoredDefinitionBindingResolver.Resolve(null!, CompiledJsonWithoutBindings, catalog));
        Assert.Throws<ArgumentException>(
            () => StoredDefinitionBindingResolver.Resolve(definition, "  ", catalog));
        Assert.Throws<ArgumentNullException>(
            () => StoredDefinitionBindingResolver.Resolve(definition, CompiledJsonWithoutBindings, null!));
    }

    private static InMemoryActionCatalog CreateCatalog(string moduleId, IReadOnlyList<string> versions)
    {
        var catalog = new InMemoryActionCatalog();
        foreach (var version in versions)
        {
            catalog.Register(
                new ActionDescriptor
                {
                    ActionId = $"{moduleId}.echo",
                    ModuleId = moduleId,
                    Version = version,
                    TrustLevel = ActionTrustLevel.Community,
                    Source = ActionSourceKind.Filesystem,
                    OwnerTenantId = "11111111-1111-1111-1111-111111111111",
                    Visibility = ActionVisibility.Tenant,
                },
                new ActionCatalogEntry(InProcessFactory: _ => throw new NotSupportedException()));
        }

        return catalog;
    }

    private static WorkflowDefinition CreateDefinition(params (string Name, StateDefinition State)[] states) =>
        new()
        {
            Name = "W",
            States = states.ToDictionary(s => s.Name, s => s.State, StringComparer.OrdinalIgnoreCase),
        };

    private static StateDefinition ActionState(string? action) =>
        new()
        {
            Action = action,
            On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["Completed"] = new TransitionDefinition { End = true },
            },
        };
}
