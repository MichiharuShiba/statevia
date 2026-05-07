using System.Linq;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.FSM;
using Statevia.Core.Engine.Join;
using Xunit;

namespace Statevia.Core.Engine.Tests.Join;

public class ForkJoinTests
{
    /// <summary>allOf の依存が全て Completed になったときに RecordFact が Join 状態名を返すことを検証する。</summary>
    [Fact]
    public void RecordFact_ReturnsJoinState_WhenAllDependenciesComplete()
    {
        // Arrange
        var def = CreateDefinitionWithJoin();
        var tracker = new JoinTracker(def);

        // Act
        var first = tracker.RecordFact("Prepare", "Completed", "data1");

        // Assert（1 件目では allOf が揃わない）
        Assert.Null(first);

        // Act
        var second = tracker.RecordFact("AskUser", "Completed", true);

        // Assert（2 件目で Join 状態名が返る）
        Assert.Equal("Join1", second);
    }

    /// <summary>
    /// allOf 完了後、GetJoinInputs のキーが JoinTable の依存状態名集合と一致することを検証する（合流前の出力が Join 解決用に集約されていること）。
    /// </summary>
    [Fact]
    public void GetJoinInputs_KeysMatchJoinTableDependencies_WhenAllCompleted()
    {
        // Arrange
        var def = CreateDefinitionWithJoin();
        var tracker = new JoinTracker(def);
        var expectedDeps = def.JoinTable["Join1"].ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        tracker.RecordFact("Prepare", Fact.Completed, "prepared");
        tracker.RecordFact("AskUser", Fact.Completed, true);
        var inputs = tracker.GetJoinInputs("Join1");

        // Assert
        var actualKeys = inputs.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.True(expectedDeps.SetEquals(actualKeys));
    }

    /// <summary>GetJoinInputs が Join の allOf に含まれる状態の出力を全て返すことを検証する。</summary>
    [Fact]
    public void GetJoinInputs_ReturnsAllOutputs()
    {
        // Arrange
        var def = CreateDefinitionWithJoin();
        var tracker = new JoinTracker(def);
        tracker.RecordFact("Prepare", "Completed", "prepared");
        tracker.RecordFact("AskUser", "Completed", true);

        // Act
        var inputs = tracker.GetJoinInputs("Join1");

        // Assert
        Assert.Equal(2, inputs.Count);
        Assert.Equal("prepared", inputs["Prepare"]);
        Assert.True((bool)(inputs["AskUser"] ?? false));
    }

    /// <summary>GetJoinSourceNodeIds が allOf の依存順で実行ノード ID を返すことを検証する。</summary>
    [Fact]
    public void GetJoinSourceNodeIds_ReturnsDependencyNodeIdsInOrder()
    {
        // Arrange
        var def = CreateDefinitionWithJoin();
        var tracker = new JoinTracker(def);
        tracker.RecordFact("AskUser", Fact.Completed, true, "node-b");
        tracker.RecordFact("Prepare", Fact.Completed, "prepared", "node-a");

        // Act
        var sourceNodeIds = tracker.GetJoinSourceNodeIds("Join1");

        // Assert（JoinTable は Prepare, AskUser の順）
        Assert.Equal(2, sourceNodeIds.Count);
        Assert.Equal("node-a", sourceNodeIds[0]);
        Assert.Equal("node-b", sourceNodeIds[1]);
    }

    /// <summary>存在しない Join 状態で GetJoinInputs を呼ぶと空の辞書が返ることを検証する。</summary>
    [Fact]
    public void GetJoinInputs_ReturnsEmpty_WhenJoinStateNotFound()
    {
        // Arrange
        var def = CreateDefinitionWithJoin();
        var tracker = new JoinTracker(def);

        // Act
        var inputs = tracker.GetJoinInputs("UnknownJoin");

        // Assert
        Assert.NotNull(inputs);
        Assert.Empty(inputs);
    }

    /// <summary>RecordFact で Failed/Cancelled を記録しても Join 状態は返さず、続行することを検証する。</summary>
    [Fact]
    public void RecordFact_ReturnsNull_WhenFactIsFailedOrCancelled()
    {
        // Arrange
        var def = CreateDefinitionWithJoin();
        var tracker = new JoinTracker(def);

        // Act
        var result1 = tracker.RecordFact("Prepare", Fact.Failed, null);
        var result2 = tracker.RecordFact("AskUser", Fact.Cancelled, null);

        // Assert
        Assert.Null(result1);
        Assert.Null(result2);
    }

    /// <summary>GetJoinInputs は Fact が Completed のものだけを返すことを検証する。</summary>
    [Fact]
    public void GetJoinInputs_FiltersNonCompleted()
    {
        // Arrange
        var def = CreateDefinitionWithJoin();
        var tracker = new JoinTracker(def);
        tracker.RecordFact("Prepare", "Completed", "prepared");
        tracker.RecordFact("AskUser", Fact.Failed, null);

        // Act
        var inputs = tracker.GetJoinInputs("Join1");

        // Assert
        Assert.Single(inputs);
        Assert.Equal("prepared", inputs["Prepare"]);
    }

    /// <summary>Join 状態に Completed が 1 件もない場合、GetJoinInputs は空の辞書を返すことを検証する。</summary>
    [Fact]
    public void GetJoinInputs_ReturnsEmpty_WhenJoinStateHasNoCompletedFacts()
    {
        // Arrange
        var def = CreateDefinitionWithJoin();
        var tracker = new JoinTracker(def);
        tracker.RecordFact("Prepare", Fact.Failed, null);
        tracker.RecordFact("AskUser", Fact.Cancelled, null);

        // Act
        var inputs = tracker.GetJoinInputs("Join1");

        // Assert
        Assert.NotNull(inputs);
        Assert.Empty(inputs);
    }

    /// <summary>TryBeginJoinExecution は allOf が揃う前は false、揃った後に1回だけ true を返すことを検証する。</summary>
    [Fact]
    public void TryBeginJoinExecution_ReturnsTrueOnlyOnce_WhenDependenciesCompleted()
    {
        // Arrange
        var def = CreateDefinitionWithJoin();
        var tracker = new JoinTracker(def);

        // allOf が揃う前
        Assert.False(tracker.TryBeginJoinExecution("Join1"));

        tracker.RecordFact("Prepare", Fact.Completed, "prepared");
        Assert.False(tracker.TryBeginJoinExecution("Join1"));

        tracker.RecordFact("AskUser", Fact.Completed, true);

        // 揃った後に最初の1回だけ true
        Assert.True(tracker.TryBeginJoinExecution("Join1"));
        Assert.False(tracker.TryBeginJoinExecution("Join1"));
    }

    private static CompiledWorkflowDefinition CreateDefinitionWithJoin() => new()
    {
        Name = "Test",
        Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>(),
        ForkTable = new Dictionary<string, IReadOnlyList<string>>(),
        JoinTable = new Dictionary<string, IReadOnlyList<string>> { ["Join1"] = new[] { "Prepare", "AskUser" } },
        WaitTable = new Dictionary<string, string>(),
        InitialState = "Start",
        StateExecutorFactory = null!
    };
}
