using Statevia.Core.Api.Application.Actions;
using Statevia.Core.Api.Application.Actions.Resolution;
using Statevia.Core.Engine.Definition;

namespace Statevia.Core.Api.Tests.Application.Actions.Resolution;

public sealed class ActionNameResolverTests
{
    private static WorkflowDefinition CreateDefinition(
        IReadOnlyDictionary<string, string>? modules,
        params (string Name, string? Action)[] states)
    {
        var stateMap = states.ToDictionary(
            s => s.Name,
            s => new StateDefinition
            {
                Action = s.Action,
                On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Completed"] = new TransitionDefinition { End = true },
                },
            },
            StringComparer.OrdinalIgnoreCase);

        return new WorkflowDefinition
        {
            Name = "W",
            Modules = modules,
            States = stateMap,
        };
    }

    /// <summary>builtin 短名 noop は canonical FQCN に正規化される。</summary>
    [Fact]
    public void Resolve_BuiltinShortNameNoop_NormalizesToCanonical()
    {
        // Arrange
        var def = CreateDefinition(null, ("A", "noop"));

        // Act
        var resolved = ActionNameResolver.Resolve(def);

        // Assert
        Assert.Equal(WellKnownActionIds.NoOpCanonical, resolved.States["A"].Action);
    }

    /// <summary>既に FQCN の action はそのまま保持される。</summary>
    [Fact]
    public void Resolve_FullyQualifiedAction_KeepsAsIs()
    {
        // Arrange
        const string fqcn = "statevia.action.builtin.rest";
        var def = CreateDefinition(null, ("A", fqcn));

        // Act
        var resolved = ActionNameResolver.Resolve(def);

        // Assert
        Assert.Equal(fqcn, resolved.States["A"].Action);
    }

    /// <summary>module alias と actionName は ModuleId 付き canonical ID に解決される。</summary>
    [Fact]
    public void Resolve_ModuleAliasActionName_PrefixesModuleId()
    {
        // Arrange
        var modules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["mail"] = "com.company.mail",
        };
        var def = CreateDefinition(modules, ("A", "mail.send"));

        // Act
        var resolved = ActionNameResolver.Resolve(def);

        // Assert
        Assert.Equal("com.company.mail.send", resolved.States["A"].Action);
    }

    /// <summary>複数 module alias があっても alias.actionName で一意に解決される。</summary>
    [Fact]
    public void Resolve_MultipleModuleAliases_ResolvesByAlias()
    {
        // Arrange
        var modules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["mail"] = "com.company.mail",
            ["crm"] = "com.company.crm",
        };
        var def = CreateDefinition(modules, ("A", "crm.sync"));

        // Act
        var resolved = ActionNameResolver.Resolve(def);

        // Assert
        Assert.Equal("com.company.crm.sync", resolved.States["A"].Action);
    }

    /// <summary>未定義 module alias は Validation Error になる。</summary>
    [Fact]
    public void Resolve_UnknownModuleAlias_Throws()
    {
        // Arrange
        var def = CreateDefinition(null, ("A", "mail.send"));

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => ActionNameResolver.Resolve(def));

        Assert.Contains("Unknown module alias 'mail'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>ドットなし短名で builtin でも module alias でもない action は unknown エラーになる。</summary>
    [Fact]
    public void Resolve_NonBuiltinShortNameWithoutDot_ThrowsUnknown()
    {
        // Arrange
        var def = CreateDefinition(null, ("A", "trade"));

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => ActionNameResolver.Resolve(def));

        Assert.Contains("Unknown action 'trade'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>action 省略状態は implicit noop canonical ID になる。</summary>
    [Fact]
    public void Resolve_OmittedAction_AppliesImplicitNoop()
    {
        // Arrange
        var def = CreateDefinition(null, ("A", null));

        // Act
        var resolved = ActionNameResolver.Resolve(def);

        // Assert
        Assert.Equal(WellKnownActionIds.NoOpCanonical, resolved.States["A"].Action);
    }

    /// <summary>join のみの状態は action 解決の対象外である。</summary>
    [Fact]
    public void Resolve_JoinOnlyState_LeavesActionNull()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "W",
            States = new Dictionary<string, StateDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["Join1"] = new StateDefinition
                {
                    Join = new JoinDefinition { All = ["A", "B"] },
                    On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Joined"] = new TransitionDefinition { End = true },
                    },
                },
            },
        };

        // Act
        var resolved = ActionNameResolver.Resolve(def);

        // Assert
        Assert.Null(resolved.States["Join1"].Action);
    }

    /// <summary>wait のみの状態は action 解決の対象外である。</summary>
    [Fact]
    public void Resolve_WaitOnlyState_LeavesActionNull()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "W",
            States = new Dictionary<string, StateDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["W"] = new StateDefinition
                {
                    Wait = new WaitDefinition { Event = "resume" },
                    On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Completed"] = new TransitionDefinition { End = true },
                    },
                },
            },
        };

        // Act
        var resolved = ActionNameResolver.Resolve(def);

        // Assert
        Assert.Null(resolved.States["W"].Action);
    }
}
