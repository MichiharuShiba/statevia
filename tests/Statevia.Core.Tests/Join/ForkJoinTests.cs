using Statevia.Core.Abstractions;
using Statevia.Core.Definition;
using Statevia.Core.FSM;
using Statevia.Core.Join;
using Xunit;

namespace Statevia.Core.Tests.Join;

public class ForkJoinTests
{
    /// <summary>allOf の依存が全て Completed になったときに RecordFact が Join 状態名を返すことを検証する。</summary>
    [Fact]
    public void RecordFact_ReturnsJoinState_WhenAllDependenciesComplete()
    {
        // Arrange
        var def = CreateDefinitionWithJoin();
        var tracker = new JoinTracker(def);

        // Act / Assert: 1 件目ではまだ揃わないので null
        Assert.Null(tracker.RecordFact("Prepare", "Completed", "data1"));

        // Act / Assert: 2 件目で allOf が揃い Join1 が返る
        Assert.Equal("Join1", tracker.RecordFact("AskUser", "Completed", true));
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
