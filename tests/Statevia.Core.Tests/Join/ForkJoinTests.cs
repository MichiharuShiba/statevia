using Statevia.Core.Abstractions;
using Statevia.Core.Definition;
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
