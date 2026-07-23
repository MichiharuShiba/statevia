using System.Globalization;

using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

/// <summary><see cref="WorkflowExecutionContext"/> の vars / sys 契約。</summary>
public class WorkflowExecutionContextTests
{
    /// <summary>SetVar でネストパスへ初回代入すると中間オブジェクトが自動生成されることを検証する。</summary>
    [Fact]
    public void SetVar_NestedPath_CreatesIntermediateObjects()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(null);

        // Act
        context.SetVar("$.vars.user.profile.email", "a@example.com");
        var root = context.ToPathRoot();
        var resolved = SimpleJsonPathResolver.Resolve(root, "$.vars.user.profile.email");

        // Assert
        Assert.True(resolved.Found);
        Assert.Equal("a@example.com", resolved.Value);
    }

    /// <summary>
    /// SetVar でネスト代入しても、先に SetStateOutput した同一オブジェクト参照の states 記録が壊れないことを検証する。
    /// </summary>
    [Fact]
    public void SetVar_NestedWrite_DoesNotMutatePreviouslyRecordedStateOutput()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(null);
        var shared = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "Alice"
        };
        context.SetStateOutput("A", shared);
        context.SetVar("$.vars.user", shared);

        // Act
        context.SetVar("$.vars.user.email", "a@example.com");
        var root = context.ToPathRoot();

        // Assert
        Assert.Equal("a@example.com", SimpleJsonPathResolver.Resolve(root, "$.vars.user.email").Value);
        Assert.Equal("Alice", SimpleJsonPathResolver.Resolve(root, "$.states.A.output.name").Value);
        Assert.False(SimpleJsonPathResolver.Resolve(root, "$.states.A.output.email").Found);
        Assert.False(shared.ContainsKey("email"));
    }

    /// <summary>同じ vars パスへの再代入が上書きになることを検証する。</summary>
    [Fact]
    public void SetVar_SamePath_OverwritesPreviousValue()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(null);
        context.SetVar("$.vars.x", 1);

        // Act
        context.SetVar("$.vars.x", 2);
        var resolved = SimpleJsonPathResolver.Resolve(context.ToPathRoot(), "$.vars.x");

        // Assert
        Assert.True(resolved.Found);
        Assert.Equal(2, resolved.Value);
    }

    /// <summary>$.vars 根への代入で vars 全体が置き換わることを検証する。</summary>
    [Fact]
    public void SetVar_RootPath_ReplacesEntireVars()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(null);
        context.SetVar("$.vars.old", true);

        // Act
        context.SetVar(
            "$.vars",
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["new"] = 9 });
        var root = context.ToPathRoot();

        // Assert
        Assert.False(SimpleJsonPathResolver.Resolve(root, "$.vars.old").Found);
        Assert.Equal(9, SimpleJsonPathResolver.Resolve(root, "$.vars.new").Value);
    }

    /// <summary>$.vars 以外への SetVar が ArgumentException になることを検証する。</summary>
    [Fact]
    public void SetVar_NonVarsPath_ThrowsArgumentException()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(null);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => context.SetVar("$.sys.now", "x"));
    }

    /// <summary>sys キー（時刻・execution・definition）が公開され、state は無いことを検証する。</summary>
    [Fact]
    public void ToPathRoot_ExposesSysKeys_WithoutState()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(null, executionId: "exec-1", definitionName: "DefA");
        var before = DateTimeOffset.Now.AddSeconds(-2);

        // Act
        var root = context.ToPathRoot();
        var after = DateTimeOffset.Now.AddSeconds(2);

        // Assert
        Assert.Equal("exec-1", SimpleJsonPathResolver.Resolve(root, "$.sys.execution.id").Value);
        Assert.Equal("DefA", SimpleJsonPathResolver.Resolve(root, "$.sys.definition.name").Value);
        Assert.False(SimpleJsonPathResolver.Resolve(root, "$.sys.state").Found);
        Assert.False(SimpleJsonPathResolver.Resolve(root, "$.sys.state.name").Found);

        var nowText = Assert.IsType<string>(SimpleJsonPathResolver.Resolve(root, "$.sys.now").Value);
        var utcText = Assert.IsType<string>(SimpleJsonPathResolver.Resolve(root, "$.sys.utcNow").Value);
        var today = Assert.IsType<string>(SimpleJsonPathResolver.Resolve(root, "$.sys.today").Value);
        Assert.True(DateTimeOffset.TryParse(nowText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var now));
        Assert.True(DateTimeOffset.TryParse(utcText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var utcNow));
        Assert.True(now >= before && now <= after);
        Assert.Equal(TimeSpan.Zero, utcNow.Offset);
        Assert.Equal(DateTimeOffset.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), today);
    }
}
