

namespace Statevia.Service.Api.Tests.Application.Security;

/// <summary><see cref="ProjectAccessRolePolicy"/> の検証。</summary>
public sealed class ProjectAccessRolePolicyTests
{
    /// <summary>Executor は Reader 以上の最小ロールを満たす。</summary>
    [Fact]
    public void MeetsMinimum_ExecutorForReader_ReturnsTrue()
    {
        // Act
        var meets = ProjectAccessRolePolicy.MeetsMinimum(ProjectAccessRole.Executor, ProjectAccessRole.Reader);

        // Assert
        Assert.True(meets);
    }

    /// <summary>Reader は Executor 最小ロールを満たさない。</summary>
    [Fact]
    public void MeetsMinimum_ReaderForExecutor_ReturnsFalse()
    {
        // Act
        var meets = ProjectAccessRolePolicy.MeetsMinimum(ProjectAccessRole.Reader, ProjectAccessRole.Executor);

        // Assert
        Assert.False(meets);
    }

    /// <summary>永続化文字列を role に変換できる。</summary>
    [Theory]
    [InlineData("reader", ProjectAccessRole.Reader)]
    [InlineData("EXECUTOR", ProjectAccessRole.Executor)]
    [InlineData("publisher", ProjectAccessRole.Publisher)]
    [InlineData("admin", ProjectAccessRole.Admin)]
    public void TryParse_ValidValue_ReturnsTrue(string value, ProjectAccessRole expected)
    {
        // Act
        var parsed = ProjectAccessRolePolicy.TryParse(value, out var role);

        // Assert
        Assert.True(parsed);
        Assert.Equal(expected, role);
    }

    /// <summary>空文字は解析に失敗する。</summary>
    [Fact]
    public void TryParse_EmptyValue_ReturnsFalse()
    {
        // Act
        var parsed = ProjectAccessRolePolicy.TryParse(string.Empty, out var role);

        // Assert
        Assert.False(parsed);
        Assert.Null(role);
    }

    /// <summary>未定義文字列は解析に失敗し role は null になる。</summary>
    [Fact]
    public void TryParse_InvalidValue_ReturnsFalseAndNull()
    {
        // Act
        var parsed = ProjectAccessRolePolicy.TryParse("not-a-role", out var role);

        // Assert
        Assert.False(parsed);
        Assert.Null(role);
    }

    /// <summary>storage 値は小文字 snake 形式。</summary>
    [Theory]
    [InlineData(ProjectAccessRole.Reader, "reader")]
    [InlineData(ProjectAccessRole.Executor, "executor")]
    [InlineData(ProjectAccessRole.Publisher, "publisher")]
    [InlineData(ProjectAccessRole.Admin, "admin")]
    public void ToStorageValue_ReturnsExpectedString(ProjectAccessRole role, string expected)
    {
        // Act
        var value = ProjectAccessRolePolicy.ToStorageValue(role);

        // Assert
        Assert.Equal(expected, value);
    }

    /// <summary>Publisher 以上のロール一覧を返す。</summary>
    [Fact]
    public void RolesAtOrAbove_Publisher_IncludesPublisherAndAdmin()
    {
        // Act
        var roles = ProjectAccessRolePolicy.RolesAtOrAbove(ProjectAccessRole.Publisher);

        // Assert
        Assert.Equal(new[] { ProjectAccessRole.Publisher, ProjectAccessRole.Admin }, roles);
    }
}
