using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Definition.Validation;
using Xunit;

namespace Statevia.Core.Engine.Tests.Definition;

/// <summary>Fork 領域検証（単一 Definition 原則）のシナリオ。</summary>
public class ForkRegionValidationTests
{
    /// <summary>標準 Fork-Join は合法のまま通る。</summary>
    [Fact]
    public void Validate_SimpleForkJoin_Passes()
    {
        // Arrange
        var def = CreateSimpleForkJoin();

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    /// <summary>枝先頭 Wait の events 経由で Join に着く定義は供給として合法。</summary>
    [Fact]
    public void Validate_WaitEventsFeedJoin_Passes()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "E1",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Fork("Left", "Wait2"),
                ["Left"] = Next("Join"),
                ["Wait2"] = Wait("Resume", "Right"),
                ["Right"] = Next("Join"),
                ["Join"] = Join(["Left", "Wait2"], "End"),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    /// <summary>領域外から枝先頭へ入る遷移は Ingress で拒否する。</summary>
    [Fact]
    public void Validate_IngressFromOutside_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "A1",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Fork("Left", "Right"),
                ["Left"] = Next("Join"),
                ["Right"] = Next("Join"),
                ["Join"] = Join(["Left", "Right"], "Decide"),
                ["Decide"] = Wait(("Finish", "End"), ("Rogue", "Left")),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.RuleId == "ForkRegion.IngressFromOutside");
        Assert.Contains(result.Issues, i => i.RuleId == "ForkRegion.BranchHeadExtraEntry");
    }

    /// <summary>枝内から Join 以外へ出る遷移は Egress で拒否する。</summary>
    [Fact]
    public void Validate_EgressWithoutJoin_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "A2",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Fork("Left", "Right"),
                ["Left"] = Next("End"),
                ["Right"] = Next("Join"),
                ["Join"] = Join(["Left", "Right"], "End"),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.RuleId == "ForkRegion.EgressWithoutJoin");
    }

    /// <summary>兄弟枝が Join 前に中間ノードを共有する定義は CrossBranch で拒否する。</summary>
    [Fact]
    public void Validate_SiblingBodyOverlap_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "A3",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Fork("Left", "Right"),
                ["Left"] = Next("Shared"),
                ["Right"] = Next("Shared"),
                ["Shared"] = Next("Join"),
                ["Join"] = Join(["Left", "Right"], "End"),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.RuleId == "ForkRegion.CrossBranch");
    }

    /// <summary>Join 後の Again で同一 Fork に戻る循環は合法。</summary>
    [Fact]
    public void Validate_CyclicReentryAfterJoin_Passes()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "Cyclic",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Next("Fork"),
                ["Fork"] = Fork("A", "B"),
                ["A"] = Next("Join"),
                ["B"] = Next("Join"),
                ["Join"] = Join(["A", "B"], "Decide"),
                ["Decide"] = Wait(("Again", "Fork"), ("Finish", "End")),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    /// <summary>error が枝内で Join に戻る定義は合法。</summary>
    [Fact]
    public void Validate_ErrorHandlerInsideBranch_Passes()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "D10A",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Fork("Left", "Work"),
                ["Left"] = Next("Join"),
                ["Work"] = new StateDefinition
                {
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new TransitionDefinition { Next = "Join" },
                        ["Failed"] = new TransitionDefinition { Next = "Recover" }
                    }
                },
                ["Recover"] = Next("Join"),
                ["Join"] = Join(["Left", "Work"], "End"),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    /// <summary>error が領域外へ出る定義は Egress で拒否する（Failed は Join 供給に数えない。領域外 Failed は拒否）。</summary>
    [Fact]
    public void Validate_ErrorToOutside_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "D10C",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Fork("Left", "Work"),
                ["Left"] = Next("Join"),
                ["Work"] = new StateDefinition
                {
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new TransitionDefinition { Next = "Join" },
                        ["Failed"] = new TransitionDefinition { Next = "End" }
                    }
                },
                ["Join"] = Join(["Left", "Work"], "End"),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.RuleId == "ForkRegion.EgressWithoutJoin");
    }

    /// <summary>Completed 辺が無く Failed だけ Join する枝は供給なし。</summary>
    [Fact]
    public void Validate_ErrorOnlySupply_FailsJoinNotFed()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "D10B",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Fork("Left", "Work"),
                ["Left"] = Next("Join"),
                ["Work"] = new StateDefinition
                {
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Failed"] = new TransitionDefinition { Next = "Join" }
                    }
                },
                ["Join"] = Join(["Left", "Work"], "End"),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.RuleId == "ForkRegion.JoinNotFed");
    }

    /// <summary>Wait の一方が Join・他方が領域外なら供給は立つが領域外 events は拒否する。</summary>
    [Fact]
    public void Validate_PartialWaitEventsOutside_FailsWaitTarget()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "D8",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Fork("Left", "Wait2"),
                ["Left"] = Next("Join"),
                ["Wait2"] = Wait(("Resume", "Join"), ("Timeout", "End")),
                ["Join"] = Join(["Left", "Wait2"], "End"),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.RuleId == "ForkRegion.WaitTargetOutside");
        Assert.DoesNotContain(result.Issues, i => i.RuleId == "ForkRegion.JoinNotFed");
    }

    /// <summary>3 段ネスト Fork-Join（ui-nested-fork 形）は合法のまま通る。</summary>
    [Fact]
    public void Validate_NestedForkJoin_Passes()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "NestedForkSample",
            States = new Dictionary<string, StateDefinition>
            {
                ["nest.start"] = Next("nest.outer.fork"),
                ["nest.outer.fork"] = Fork("nest.outer.fast", "nest.mid.fork"),
                ["nest.outer.fast"] = Next("nest.outer.join"),
                ["nest.mid.fork"] = Fork("nest.mid.fast", "nest.inner.fork"),
                ["nest.mid.fast"] = Next("nest.mid.join"),
                ["nest.inner.fork"] = Fork("nest.inner.a", "nest.inner.b"),
                ["nest.inner.a"] = Next("nest.inner.join"),
                ["nest.inner.b"] = Next("nest.inner.join"),
                ["nest.inner.join"] = Join(["nest.inner.a", "nest.inner.b"], "nest.mid.join"),
                ["nest.mid.join"] = Join(["nest.mid.fast", "nest.inner.fork"], "nest.outer.join"),
                ["nest.outer.join"] = Join(["nest.outer.fast", "nest.mid.fork"], "nest.notify"),
                ["nest.notify"] = Next("nest.end"),
                ["nest.end"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    /// <summary>枝先頭 action の先に内側 Fork があるネストは合法。</summary>
    [Fact]
    public void Validate_InnerForkAfterMidAction_Passes()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "D3InnerForkFromMidBranch",
            States = new Dictionary<string, StateDefinition>
            {
                ["start"] = Next("outer.fork"),
                ["outer.fork"] = Fork("outer.fast", "mid"),
                ["outer.fast"] = Next("outer.join"),
                ["mid"] = Next("inner.fork"),
                ["inner.fork"] = Fork("inner.a", "inner.b"),
                ["inner.a"] = Next("inner.join"),
                ["inner.b"] = Next("inner.join"),
                ["inner.join"] = Join(["inner.a", "inner.b"], "outer.join"),
                ["outer.join"] = Join(["outer.fast", "mid"], "end"),
                ["end"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    /// <summary>条件遷移（cases/default）経由の Join 供給は合法。</summary>
    [Fact]
    public void Validate_ConditionalEdgesFeedJoin_Passes()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "EdgesSupply",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Fork("Left", "Chooser"),
                ["Left"] = Next("Join"),
                ["Chooser"] = new StateDefinition
                {
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new TransitionDefinition
                        {
                            Cases =
                            [
                                new TransitionCaseDefinition
                                {
                                    When = new ConditionExpressionDefinition { Path = "$.x", Op = "gt", Value = 0 },
                                    Transition = new TransitionDefinition { Next = "Join" }
                                }
                            ],
                            Default = new TransitionDefinition { Next = "Join" }
                        }
                    }
                },
                ["Join"] = Join(["Left", "Chooser"], "End"),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    /// <summary>内側枝から外側兄弟への Failed 辺は NestedCross で拒否する。</summary>
    [Fact]
    public void Validate_InnerFailedToOuterSibling_FailsNestedCross()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "A4",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Fork("Fast", "InnerFork"),
                ["Fast"] = Next("OuterJoin"),
                ["InnerFork"] = Fork("A", "B"),
                ["A"] = new StateDefinition
                {
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new TransitionDefinition { Next = "InnerJoin" },
                        ["Failed"] = new TransitionDefinition { Next = "Fast" }
                    }
                },
                ["B"] = Next("InnerJoin"),
                ["InnerJoin"] = Join(["A", "B"], "OuterJoin"),
                ["OuterJoin"] = Join(["Fast", "InnerFork"], "End"),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.RuleId == "ForkRegion.NestedCrossBranch");
    }

    /// <summary>非入れ子の並列領域を横断する辺は NestedCross で拒否する。</summary>
    [Fact]
    public void Validate_ParallelRegionCross_FailsNestedCross()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "ParallelCross",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Fork("LeftFork", "RightFork"),
                ["LeftFork"] = Fork("L1", "L2"),
                ["RightFork"] = Fork("R1", "R2"),
                ["L1"] = new StateDefinition
                {
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new TransitionDefinition { Next = "LeftJoin" },
                        ["Failed"] = new TransitionDefinition { Next = "R1" }
                    }
                },
                ["L2"] = Next("LeftJoin"),
                ["R1"] = Next("RightJoin"),
                ["R2"] = Next("RightJoin"),
                ["LeftJoin"] = Join(["L1", "L2"], "End"),
                ["RightJoin"] = Join(["R1", "R2"], "End"),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.RuleId == "ForkRegion.NestedCrossBranch");
    }

    /// <summary>同一 Join を複数 Fork が供給する定義は AmbiguousJoin で拒否する。</summary>
    [Fact]
    public void Validate_TwoForksSameJoinAll_FailsAmbiguousJoin()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "Ambiguous",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Next("F1"),
                ["F1"] = Fork("A", "B"),
                ["A"] = Next("Join"),
                ["B"] = Next("Join"),
                ["Join"] = Join(["A", "B"], "Decide"),
                ["Decide"] = Wait(("Again", "F1"), ("Other", "F2"), ("Finish", "End")),
                ["F2"] = Fork("A", "B"),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.RuleId == "ForkRegion.AmbiguousJoin");
    }

    /// <summary>対応 Fork が無い Join は JoinNotFed で拒否する。</summary>
    [Fact]
    public void Validate_OrphanJoin_FailsJoinNotFed()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "OrphanJoin",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Next("Join"),
                ["Join"] = Join(["Start"], "End"),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.RuleId == "ForkRegion.JoinNotFed");
    }

    /// <summary>枝内 Subscribe が Join を供給する定義は合法。</summary>
    [Fact]
    public void Validate_WaitSubscribeFeedsJoin_Passes()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "SubscribeSupply",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Fork("Left", "Wait2"),
                ["Left"] = Next("Join"),
                ["Wait2"] = new StateDefinition
                {
                    Wait = new WaitDefinition
                    {
                        Subscribe =
                        [
                            new WaitSubscribeEntry { Topic = "orders", Next = "Join" }
                        ]
                    }
                },
                ["Join"] = Join(["Left", "Wait2"], "End"),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    /// <summary>枝内 Subscribe が領域外へ出る定義は WaitTarget で拒否する。</summary>
    [Fact]
    public void Validate_WaitSubscribeOutside_FailsWaitTarget()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "SubscribeOutside",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Fork("Left", "Wait2"),
                ["Left"] = Next("Join"),
                ["Wait2"] = new StateDefinition
                {
                    Wait = new WaitDefinition
                    {
                        Subscribe =
                        [
                            new WaitSubscribeEntry { Topic = "ok", Next = "Join" },
                            new WaitSubscribeEntry { Topic = "timeout", Next = "End" }
                        ]
                    }
                },
                ["Join"] = Join(["Left", "Wait2"], "End"),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.RuleId == "ForkRegion.WaitTargetOutside");
    }

    /// <summary>Input.Path の兄弟 <c>$.states</c> 参照は拒否する。</summary>
    [Fact]
    public void Validate_SiblingStatesLiteralOnInputPath_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "D1Path",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Fork("Left", "Right"),
                ["Left"] = new StateDefinition
                {
                    Input = new StateInputDefinition { Path = "$.states.Right.output" },
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new TransitionDefinition { Next = "Join" }
                    }
                },
                ["Right"] = Next("Join"),
                ["Join"] = Join(["Left", "Right"], "End"),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.RuleId == "ForkRegion.SiblingStateRef");
    }

    /// <summary>ブラケット形式の兄弟 <c>$.states['Right']</c> 参照は拒否する。</summary>
    [Fact]
    public void Validate_SiblingStatesBracketLiteral_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "D1Bracket",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Fork("Left", "Right"),
                ["Left"] = new StateDefinition
                {
                    Input = new StateInputDefinition
                    {
                        Values = new Dictionary<string, StateInputValueDefinition>
                        {
                            ["sibling"] = new() { Path = "$.states['Right'].output" }
                        }
                    },
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new TransitionDefinition { Next = "Join" }
                    }
                },
                ["Right"] = Next("Join"),
                ["Join"] = Join(["Left", "Right"], "End"),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.RuleId == "ForkRegion.SiblingStateRef");
    }

    /// <summary>二重引用ブラケットの兄弟参照も拒否する。</summary>
    [Fact]
    public void Validate_SiblingStatesDoubleQuoteBracket_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "D1DoubleQuote",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Fork("Left", "Right"),
                ["Left"] = new StateDefinition
                {
                    Input = new StateInputDefinition
                    {
                        Values = new Dictionary<string, StateInputValueDefinition>
                        {
                            ["sibling"] = new() { Path = "$.states[\"Right\"].output" }
                        }
                    },
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new TransitionDefinition { Next = "Join" }
                    }
                },
                ["Right"] = Next("Join"),
                ["Join"] = Join(["Left", "Right"], "End"),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.RuleId == "ForkRegion.SiblingStateRef");
    }

    /// <summary>対応 Join が無い Fork は MissingJoin で拒否する。</summary>
    [Fact]
    public void Validate_UnpairedFork_FailsMissingJoin()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "C3",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Fork("A", "B"),
                ["A"] = Next("End"),
                ["B"] = Next("End"),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.RuleId == "ForkRegion.MissingJoin");
    }

    /// <summary>Join 前の兄弟 <c>$.states</c> 参照は拒否する。</summary>
    [Fact]
    public void Validate_SiblingStatesLiteral_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "D1",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Fork("Left", "Right"),
                ["Left"] = new StateDefinition
                {
                    Input = new StateInputDefinition
                    {
                        Values = new Dictionary<string, StateInputValueDefinition>
                        {
                            ["sibling"] = new() { Path = "$.states.Right.output" }
                        }
                    },
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new TransitionDefinition { Next = "Join" }
                    }
                },
                ["Right"] = Next("Join"),
                ["Join"] = Join(["Left", "Right"], "End"),
                ["End"] = End()
            }
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.RuleId == "ForkRegion.SiblingStateRef");
    }

    private static WorkflowDefinition CreateSimpleForkJoin() =>
        new()
        {
            Name = "Simple",
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = Fork("A", "B"),
                ["A"] = Next("Join"),
                ["B"] = Next("Join"),
                ["Join"] = Join(["A", "B"], "End"),
                ["End"] = End()
            }
        };

    private static StateDefinition Fork(params string[] branches) =>
        new()
        {
            On = new Dictionary<string, TransitionDefinition>
            {
                ["Completed"] = new TransitionDefinition { Fork = branches }
            }
        };

    private static StateDefinition Next(string next) =>
        new()
        {
            On = new Dictionary<string, TransitionDefinition>
            {
                ["Completed"] = new TransitionDefinition { Next = next }
            }
        };

    private static StateDefinition End() =>
        new()
        {
            On = new Dictionary<string, TransitionDefinition>
            {
                ["Completed"] = new TransitionDefinition { End = true }
            }
        };

    private static StateDefinition Join(string[] all, string next) =>
        new()
        {
            Join = new JoinDefinition { All = all },
            On = new Dictionary<string, TransitionDefinition>
            {
                ["Joined"] = new TransitionDefinition { Next = next }
            }
        };

    private static StateDefinition Wait(string eventName, string next) =>
        Wait((eventName, next));

    private static StateDefinition Wait(params (string Event, string Next)[] events) =>
        new()
        {
            Wait = new WaitDefinition
            {
                Events = events.ToDictionary(e => e.Event, e => e.Next, StringComparer.OrdinalIgnoreCase)
            }
        };
}
