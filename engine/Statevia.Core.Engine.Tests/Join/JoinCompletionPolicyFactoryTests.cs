using Statevia.Core.Engine.Join;
using Xunit;

namespace Statevia.Core.Engine.Tests.Join;

/// <summary><see cref="JoinCompletionPolicyFactory"/> の検証と例外。</summary>
public class JoinCompletionPolicyFactoryTests
{
    private readonly JoinCompletionPolicyFactory _factory = new();

    /// <summary>AllOf と非空依存でポリシーが生成されることを検証する。</summary>
    [Fact]
    public void Create_AllOf_ReturnsPolicy()
    {
        // Arrange
        var dependencies = new[] { "A", "B" };

        // Act
        var policy = _factory.Create("Join1", JoinConditionKind.AllOf, dependencies);

        // Assert
        Assert.NotNull(policy);
        Assert.IsType<AllOfJoinCompletionPolicy>(policy);
    }

    /// <summary>join 名が空白のとき <see cref="ArgumentException"/> になることを検証する。</summary>
    [Fact]
    public void Create_EmptyJoinStateName_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            _factory.Create("  ", JoinConditionKind.AllOf, new[] { "A" }));
    }

    /// <summary>依存が空のとき <see cref="ArgumentException"/> になることを検証する。</summary>
    [Fact]
    public void Create_EmptyDependencies_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            _factory.Create("Join1", JoinConditionKind.AllOf, Array.Empty<string>()));
    }

    /// <summary>未対応の Join 種別で <see cref="NotSupportedException"/> になることを検証する。</summary>
    [Fact]
    public void Create_UnsupportedConditionKind_ThrowsNotSupported()
    {
        // Act / Assert
        Assert.Throws<NotSupportedException>(() =>
            _factory.Create("Join1", (JoinConditionKind)99, new[] { "A" }));
    }
}
