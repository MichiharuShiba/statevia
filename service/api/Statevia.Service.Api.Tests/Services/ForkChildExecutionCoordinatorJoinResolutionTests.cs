using Statevia.Core.Application.Contracts.Services;
using Statevia.Core.Application.Services;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="ForkChildExecutionCoordinator.ResolveJoinState"/> の解決規則。</summary>
public sealed class ForkChildExecutionCoordinatorJoinResolutionTests
{
    /// <summary>Join.all と分岐先頭集合が一致する Join を返す。</summary>
    [Fact]
    public void ResolveJoinState_ReturnsMatchingJoin()
    {
        // Arrange
        var definition = CreateDefinition(
            fork: ("Start", ["A", "B"]),
            join: ("Join1", ["A", "B"]));
        var branches = new[]
        {
            new ForkBranchExpansion("A", null),
            new ForkBranchExpansion("B", null)
        };

        // Act
        var joinState = ForkChildExecutionCoordinator.ResolveJoinState(definition, branches);

        // Assert
        Assert.Equal("Join1", joinState);
    }

    /// <summary>一致する Join が無いとき例外になる。</summary>
    [Fact]
    public void ResolveJoinState_Throws_WhenNoJoinMatches()
    {
        // Arrange
        var definition = CreateDefinition(
            fork: ("Start", ["A", "B"]),
            join: ("Join1", ["A", "C"]));
        var branches = new[]
        {
            new ForkBranchExpansion("A", null),
            new ForkBranchExpansion("B", null)
        };

        // Act
        var act = () => ForkChildExecutionCoordinator.ResolveJoinState(definition, branches);

        // Assert
        Assert.Throws<ForkJoinResolutionException>(act);
    }

    /// <summary>複数 Join が同一分岐集合に一致するとき曖昧として例外になる。</summary>
    [Fact]
    public void ResolveJoinState_Throws_WhenJoinIsAmbiguous()
    {
        // Arrange
        var definition = CreateDefinition(
            fork: ("Start", ["A", "B"]),
            joins:
            [
                ("Join1", ["A", "B"]),
                ("Join2", ["A", "B"])
            ]);
        var branches = new[]
        {
            new ForkBranchExpansion("A", null),
            new ForkBranchExpansion("B", null)
        };

        // Act
        var act = () => ForkChildExecutionCoordinator.ResolveJoinState(definition, branches);

        // Assert
        Assert.Throws<ForkJoinResolutionException>(act);
    }

    private static CompiledWorkflowDefinition CreateDefinition(
        (string State, string[] Branches) fork,
        (string State, string[] All) join) =>
        CreateDefinition(fork, [join]);

    private static CompiledWorkflowDefinition CreateDefinition(
        (string State, string[] Branches) fork,
        (string State, string[] All)[] joins) =>
        new()
        {
            Name = "ForkJoinResolve",
            InitialState = fork.State,
            Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>(StringComparer.OrdinalIgnoreCase),
            ForkTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [fork.State] = fork.Branches
            },
            JoinTable = joins.ToDictionary(
                j => j.State,
                j => (IReadOnlyList<string>)j.All,
                StringComparer.OrdinalIgnoreCase),
            WaitEventRouteTable = new Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>>(
                StringComparer.OrdinalIgnoreCase),
            StateExecutorFactory = new DictionaryStateExecutorFactory(
                new Dictionary<string, IStateExecutor>(StringComparer.OrdinalIgnoreCase))
        };
}
