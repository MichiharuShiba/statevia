using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Service.Api.Application.Actions;
using Statevia.Service.Api.Application.Actions.Catalog;
using Statevia.Service.Api.Application.Actions.Versioning;
using Statevia.Core.Engine.Definition;

namespace Statevia.Service.Api.Tests.Application.Actions.Versioning;

/// <summary><see cref="ModuleActionCompileBinder"/> の compile バインディング単体テスト。</summary>
public sealed class ModuleActionCompileBinderTests
{
    private static InMemoryActionCatalog CreateCatalogWithModuleVersions(
        string moduleId,
        IReadOnlyList<string> versions,
        string actionName = "echo")
    {
        var catalog = new InMemoryActionCatalog();
        foreach (var version in versions)
        {
            catalog.Register(
                new ActionDescriptor
                {
                    ActionId = $"{moduleId}.{actionName}",
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

    private static WorkflowDefinition CreateDefinition(
        IReadOnlyDictionary<string, string>? modules,
        params (string Name, StateDefinition State)[] states) =>
        new()
        {
            Name = "W",
            Modules = ParseModules(modules),
            States = states.ToDictionary(
                s => s.Name,
                s => s.State,
                StringComparer.OrdinalIgnoreCase),
        };

    private static IReadOnlyDictionary<string, ModuleImportReference>? ParseModules(
        IReadOnlyDictionary<string, string>? modules) =>
        modules?.ToDictionary(
            entry => entry.Key,
            entry => ModuleImportReference.ParseImportValue(entry.Value),
            StringComparer.OrdinalIgnoreCase);

    private static StateDefinition ActionState(string? action) =>
        new()
        {
            Action = action,
            On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["Completed"] = new TransitionDefinition { End = true },
            },
        };

    /// <summary>module alias 参照は compile 時に版をピンし、状態バインディングへ反映する。</summary>
    [Fact]
    public void Bind_WhenModuleAlias_ResolvesVersionAndStateBinding()
    {
        // Arrange
        var catalog = CreateCatalogWithModuleVersions("demo.module", ["1.0.0", "2.0.0"]);
        var definition = CreateDefinition(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mail"] = "demo.module@1",
            },
            ("A", ActionState("mail.echo")));

        // Act
        var result = ModuleActionCompileBinder.Bind(definition, catalog);

        // Assert
        Assert.Equal("1.0.0", result.ResolvedModules["mail"].ResolvedVersion);
        Assert.Equal("demo.module", result.ResolvedModules["mail"].ModuleId);
        Assert.Equal("demo.module.echo", result.StateActionBindings["A"].LogicalActionId);
        Assert.Equal("1.0.0", result.StateActionBindings["A"].ResolvedModuleVersion);
        Assert.Equal("demo.module", result.StateActionBindings["A"].ModuleId);
        Assert.Equal("echo", result.StateActionBindings["A"].ActionName);
    }

    /// <summary>同一 moduleId でも alias ごとに異なる版をピンできる。</summary>
    [Fact]
    public void Bind_WhenDifferentAliasesPinDifferentVersions_BindsPerState()
    {
        // Arrange
        var catalog = CreateCatalogWithModuleVersions("demo.module", ["1.0.0", "2.0.0"]);
        var definition = CreateDefinition(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mailV1"] = "demo.module@1.0.0",
                ["mailV2"] = "demo.module@2.0.0",
            },
            ("UseV1", ActionState("mailV1.echo")),
            ("UseV2", ActionState("mailV2.echo")));

        // Act
        var result = ModuleActionCompileBinder.Bind(definition, catalog);

        // Assert
        Assert.Equal("1.0.0", result.ResolvedModules["mailV1"].ResolvedVersion);
        Assert.Equal("2.0.0", result.ResolvedModules["mailV2"].ResolvedVersion);
        Assert.Equal("1.0.0", result.StateActionBindings["UseV1"].ResolvedModuleVersion);
        Assert.Equal("2.0.0", result.StateActionBindings["UseV2"].ResolvedModuleVersion);
        Assert.Equal("demo.module.echo", result.StateActionBindings["UseV1"].LogicalActionId);
        Assert.Equal("demo.module.echo", result.StateActionBindings["UseV2"].LogicalActionId);
    }

    /// <summary>caret レンジはロード済み版のうち条件を満たす最新安定版へ解決する。</summary>
    [Fact]
    public void Bind_WhenCaretRange_ResolvesHighestMatchingVersion()
    {
        // Arrange
        var catalog = CreateCatalogWithModuleVersions("demo.module", ["1.0.0", "1.4.2", "2.0.0"]);
        var definition = CreateDefinition(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mail"] = "demo.module@^1.2",
            },
            ("A", ActionState("mail.echo")));

        // Act
        var result = ModuleActionCompileBinder.Bind(definition, catalog);

        // Assert
        Assert.Equal("1.4.2", result.ResolvedModules["mail"].ResolvedVersion);
        Assert.Equal("1.4.2", result.StateActionBindings["A"].ResolvedModuleVersion);
    }

    /// <summary>action 省略状態は implicit noop をバインドし、版ピンは不要。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Bind_WhenImplicitNoop_BindsToCanonicalWithoutVersion(string? action)
    {
        // Arrange
        var catalog = new InMemoryActionCatalog();
        var definition = CreateDefinition(null, ("A", ActionState(action)));

        // Act
        var result = ModuleActionCompileBinder.Bind(definition, catalog);

        // Assert
        Assert.Equal(WellKnownActionIds.NoOpCanonical, result.StateActionBindings["A"].LogicalActionId);
        Assert.Null(result.StateActionBindings["A"].ResolvedModuleVersion);
        Assert.Null(result.StateActionBindings["A"].ModuleId);
        Assert.Empty(result.ResolvedModules);
    }

    /// <summary>builtin 短名は canonical ID にバインドし、版ピンは不要。</summary>
    [Fact]
    public void Bind_WhenBuiltinShortName_BindsWithoutModuleVersion()
    {
        // Arrange
        var catalog = new InMemoryActionCatalog();
        var definition = CreateDefinition(null, ("A", ActionState("rest")));

        // Act
        var result = ModuleActionCompileBinder.Bind(definition, catalog);

        // Assert
        Assert.Equal(WellKnownActionIds.Rest, result.StateActionBindings["A"].LogicalActionId);
        Assert.Null(result.StateActionBindings["A"].ResolvedModuleVersion);
    }

    /// <summary>builtin FQCN はそのままバインドし、版ピンは不要。</summary>
    [Fact]
    public void Bind_WhenBuiltinFqcn_BindsWithoutModuleVersion()
    {
        // Arrange
        var catalog = new InMemoryActionCatalog();
        var definition = CreateDefinition(null, ("A", ActionState(WellKnownActionIds.Rest)));

        // Act
        var result = ModuleActionCompileBinder.Bind(definition, catalog);

        // Assert
        Assert.Equal(WellKnownActionIds.Rest, result.StateActionBindings["A"].LogicalActionId);
        Assert.Null(result.StateActionBindings["A"].ResolvedModuleVersion);
    }

    /// <summary>wait のみの状態は action バインディングの対象外。</summary>
    [Fact]
    public void Bind_WhenWaitOnlyState_ExcludesFromBindings()
    {
        // Arrange
        var catalog = CreateCatalogWithModuleVersions("demo.module", ["1.0.0"]);
        var definition = CreateDefinition(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mail"] = "demo.module@1.0.0",
            },
            ("W", new StateDefinition
            {
                Wait = new WaitDefinition { Event = "resume" },
                On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Completed"] = new TransitionDefinition { End = true },
                },
            }));

        // Act
        var result = ModuleActionCompileBinder.Bind(definition, catalog);

        // Assert
        Assert.DoesNotContain("W", result.StateActionBindings.Keys);
        Assert.Equal("1.0.0", result.ResolvedModules["mail"].ResolvedVersion);
    }

    /// <summary>join のみの状態は action バインディングの対象外。</summary>
    [Fact]
    public void Bind_WhenJoinOnlyState_ExcludesFromBindings()
    {
        // Arrange
        var catalog = CreateCatalogWithModuleVersions("demo.module", ["1.0.0"]);
        var definition = CreateDefinition(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mail"] = "demo.module@1.0.0",
            },
            ("Join1", new StateDefinition
            {
                Join = new JoinDefinition { All = ["A", "B"] },
                On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Joined"] = new TransitionDefinition { End = true },
                },
            }));

        // Act
        var result = ModuleActionCompileBinder.Bind(definition, catalog);

        // Assert
        Assert.DoesNotContain("Join1", result.StateActionBindings.Keys);
    }

    /// <summary>workflow.modules 未使用の FQCN は単一版ロード時に版をピンする。</summary>
    [Fact]
    public void Bind_WhenFqcnSingleLoadedVersion_PinsVersion()
    {
        // Arrange
        var catalog = CreateCatalogWithModuleVersions("demo.module", ["1.2.3"]);
        var definition = CreateDefinition(null, ("A", ActionState("demo.module.echo")));

        // Act
        var result = ModuleActionCompileBinder.Bind(definition, catalog);

        // Assert
        Assert.Equal("demo.module.echo", result.StateActionBindings["A"].LogicalActionId);
        Assert.Equal("1.2.3", result.StateActionBindings["A"].ResolvedModuleVersion);
        Assert.Equal("demo.module", result.StateActionBindings["A"].ModuleId);
        Assert.Equal("echo", result.StateActionBindings["A"].ActionName);
    }

    /// <summary>workflow.modules 未使用の FQCN で複数版ロード時は MigrationRequired 相当エラーになる。</summary>
    [Fact]
    public void Bind_WhenFqcnMultipleLoadedVersions_Throws()
    {
        // Arrange
        var catalog = CreateCatalogWithModuleVersions("demo.module", ["1.0.0", "2.0.0"]);
        var definition = CreateDefinition(null, ("A", ActionState("demo.module.echo")));

        // Act & Assert
        var ex = Assert.Throws<DefinitionMigrationRequiredException>(
            () => ModuleActionCompileBinder.Bind(definition, catalog));

        Assert.Contains("multiple loaded versions", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>import 対象 Module にロード済み版がないときは版解決に失敗する。</summary>
    [Fact]
    public void Bind_WhenModuleHasNoLoadedVersions_Throws()
    {
        // Arrange
        var catalog = new InMemoryActionCatalog();
        var definition = CreateDefinition(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mail"] = "demo.module@1.0.0",
            },
            ("A", ActionState("mail.echo")));

        // Act & Assert
        var ex = Assert.Throws<ModuleVersionResolutionException>(
            () => ModuleActionCompileBinder.Bind(definition, catalog));

        Assert.Contains("no loaded versions", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>未定義 module alias を action で参照すると失敗する。</summary>
    [Fact]
    public void Bind_WhenUnknownModuleAlias_Throws()
    {
        // Arrange
        var catalog = CreateCatalogWithModuleVersions("demo.module", ["1.0.0"]);
        var definition = CreateDefinition(null, ("A", ActionState("mail.echo")));

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => ModuleActionCompileBinder.Bind(definition, catalog));

        Assert.Contains("Unknown module alias 'mail'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>ドットなし短名で builtin でも module alias でもない action は unknown エラーになる。</summary>
    [Fact]
    public void Bind_WhenInvalidActionWithoutDot_Throws()
    {
        // Arrange
        var catalog = new InMemoryActionCatalog();
        var definition = CreateDefinition(null, ("A", ActionState("trade")));

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => ModuleActionCompileBinder.Bind(definition, catalog));

        Assert.Contains("Unknown action 'trade'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>複数状態をそれぞれ独立してバインドする。</summary>
    [Fact]
    public void Bind_WhenMultipleStates_BindsEachState()
    {
        // Arrange
        var catalog = CreateCatalogWithModuleVersions("demo.module", ["1.0.0"], actionName: "send");
        catalog.Register(
            new ActionDescriptor
            {
                ActionId = "other.module.sync",
                ModuleId = "other.module",
                Version = "3.0.0",
                TrustLevel = ActionTrustLevel.Community,
                Source = ActionSourceKind.Filesystem,
                OwnerTenantId = "11111111-1111-1111-1111-111111111111",
                Visibility = ActionVisibility.Tenant,
            },
            new ActionCatalogEntry(InProcessFactory: _ => throw new NotSupportedException()));

        var definition = CreateDefinition(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mail"] = "demo.module@1.0.0",
                ["crm"] = "other.module@3.0.0",
            },
            ("Send", ActionState("mail.send")),
            ("Sync", ActionState("crm.sync")),
            ("Noop", ActionState("noop")));

        // Act
        var result = ModuleActionCompileBinder.Bind(definition, catalog);

        // Assert
        Assert.Equal(3, result.StateActionBindings.Count);
        Assert.Equal("demo.module.send", result.StateActionBindings["Send"].LogicalActionId);
        Assert.Equal("1.0.0", result.StateActionBindings["Send"].ResolvedModuleVersion);
        Assert.Equal("other.module.sync", result.StateActionBindings["Sync"].LogicalActionId);
        Assert.Equal("3.0.0", result.StateActionBindings["Sync"].ResolvedModuleVersion);
        Assert.Equal(WellKnownActionIds.NoOpCanonical, result.StateActionBindings["Noop"].LogicalActionId);
    }

    /// <summary>import 文字列の moduleId@range を解析して版解決する。</summary>
    [Fact]
    public void Parse_WhenModuleIdWithRange_SplitsModuleIdAndRange()
    {
        // Act
        var reference = ModuleImportParser.Parse("mail", "demo.module@^1.2");

        // Assert
        Assert.Equal(new ModuleReference("mail", "demo.module", "^1.2"), reference);
    }

    /// <summary>@ 省略の import は LATEST（空レンジ）として扱う。</summary>
    [Fact]
    public void Parse_WhenModuleIdWithoutRange_UsesEmptyVersionRange()
    {
        // Act
        var reference = ModuleImportParser.Parse("mail", "demo.module");

        // Assert
        Assert.Equal(new ModuleReference("mail", "demo.module", string.Empty), reference);
    }

    /// <summary>LATEST 省略 import はロード済み最新安定版へ解決する。</summary>
    [Fact]
    public void Bind_WhenImportWithoutRange_ResolvesLatestStable()
    {
        // Arrange
        var catalog = CreateCatalogWithModuleVersions("demo.module", ["1.0.0", "1.5.0", "2.0.0-rc.1"]);
        var definition = CreateDefinition(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mail"] = "demo.module",
            },
            ("A", ActionState("mail.echo")));

        // Act
        var result = ModuleActionCompileBinder.Bind(definition, catalog);

        // Assert
        Assert.Equal("1.5.0", result.ResolvedModules["mail"].ResolvedVersion);
        Assert.Equal("1.5.0", result.StateActionBindings["A"].ResolvedModuleVersion);
    }
}
