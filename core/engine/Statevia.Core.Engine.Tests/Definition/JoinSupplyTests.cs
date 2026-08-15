using Statevia.Core.Engine.Definition.Validation;
using Xunit;

namespace Statevia.Core.Engine.Tests.Definition;

/// <summary><see cref="JoinSupply.Reaches"/> の障壁・自己到達・空後続。</summary>
public class JoinSupplyTests
{
    /// <summary>始点と終点が同じなら後続を辿らず true。</summary>
    [Fact]
    public void Reaches_WhenFromEqualsTo_ReturnsTrue()
    {
        // Arrange
        var successors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        // Act
        var reached = JoinSupply.Reaches(successors, "Join", "Join");

        // Assert
        Assert.True(reached);
    }

    /// <summary>空白の後続名は無視し、有効な経路があれば到達する。</summary>
    [Fact]
    public void Reaches_WhenNeighborIsWhitespace_SkipsAndFollowsValid()
    {
        // Arrange
        var successors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = ["", "  ", "Join"]
        };

        // Act
        var reached = JoinSupply.Reaches(successors, "A", "Join");

        // Assert
        Assert.True(reached);
    }

    /// <summary>他 Join 障壁を通過しないため外側 Join へ誤供給しない。</summary>
    [Fact]
    public void Reaches_WhenBarrierIsOtherJoin_DoesNotTraverse()
    {
        // Arrange
        var successors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["InnerA"] = ["InnerJoin"],
            ["InnerJoin"] = ["OuterJoin"]
        };
        var barriers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "InnerJoin", "OuterJoin" };

        // Act
        var reached = JoinSupply.Reaches(successors, "InnerA", "OuterJoin", barriers);

        // Assert
        Assert.False(reached);
    }

    /// <summary>障壁集合に終点自身が含まれていても終点到達は true。</summary>
    [Fact]
    public void Reaches_WhenTargetIsInBarrierSet_StillReturnsTrueOnHit()
    {
        // Arrange
        var successors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = ["Join"]
        };
        var barriers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Join" };

        // Act
        var reached = JoinSupply.Reaches(successors, "A", "Join", barriers);

        // Assert
        Assert.True(reached);
    }

    /// <summary>後続辞書に始点が無いときは到達しない。</summary>
    [Fact]
    public void Reaches_WhenFromHasNoSuccessors_ReturnsFalse()
    {
        // Arrange
        var successors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Other"] = ["Join"]
        };

        // Act
        var reached = JoinSupply.Reaches(successors, "A", "Join");

        // Assert
        Assert.False(reached);
    }

    /// <summary>null の供給辞書は ArgumentNullException。</summary>
    [Fact]
    public void Reaches_WhenSuccessorsIsNull_Throws()
    {
        // Arrange
        IReadOnlyDictionary<string, IReadOnlyList<string>> successors = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => JoinSupply.Reaches(successors, "A", "B"));
    }
}
