using System.Globalization;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Xunit;

namespace Statevia.Core.Engine.Tests.Definition;

/// <summary>
/// <see cref="ExecutionContextPathResolver"/> の CallPath / 互換解決テスト。
/// </summary>
public class ExecutionContextPathResolverTests
{
    /// <summary><c>$.sys.now("yyyyMMdd")</c> が 8 桁数字になることを検証する。</summary>
    [Fact]
    public void Resolve_SysNowCallPath_ReturnsFormattedLocalDate()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(null, executionId: "e1", definitionName: "D");

        // Act
        var result = ExecutionContextPathResolver.Resolve(context, "$.sys.now(\"yyyyMMdd\")");

        // Assert
        Assert.True(result.IsSupportedPathExpression);
        Assert.True(result.Found);
        Assert.Null(result.WarningReason);
        var text = Assert.IsType<string>(result.Value);
        Assert.Matches(@"^\d{8}$", text);
        Assert.Equal(DateTimeOffset.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture), text);
    }

    /// <summary><c>$.sys.utcNow("yyyyMMdd")</c> が UTC 日付の 8 桁になることを検証する。</summary>
    [Fact]
    public void Resolve_SysUtcNowCallPath_ReturnsFormattedUtcDate()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(null, executionId: "e1", definitionName: "D");

        // Act
        var result = ExecutionContextPathResolver.Resolve(context, "$.sys.utcNow(\"yyyyMMdd\")");

        // Assert
        Assert.True(result.Found);
        var text = Assert.IsType<string>(result.Value);
        Assert.Matches(@"^\d{8}$", text);
        Assert.Equal(DateTimeOffset.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture), text);
    }

    /// <summary>引数なし <c>$.sys.now</c> が従来どおり ISO-8601 になることを検証する。</summary>
    [Fact]
    public void Resolve_SysNowProperty_RemainsIso8601Compatible()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(null, executionId: "e1", definitionName: "D");

        // Act
        var result = ExecutionContextPathResolver.Resolve(context, "$.sys.now");

        // Assert
        Assert.True(result.Found);
        var text = Assert.IsType<string>(result.Value);
        Assert.True(DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out _));
    }

    /// <summary>実行時に無効な書式は null と警告になることを検証する。</summary>
    [Fact]
    public void Resolve_InvalidFormat_ReturnsNullWithWarning()
    {
        // Arrange
        // 閉じられていない単引用符は .NET カスタム日時書式で FormatException になる。
        const string path = "$.sys.now(\"yyyy'\")";
        Assert.True(SysPathCall.IsValidCallPath(path));
        var context = WorkflowExecutionContext.Create(null, executionId: "e1", definitionName: "D");

        // Act
        var result = ExecutionContextPathResolver.Resolve(context, path);

        // Assert
        Assert.True(result.IsSupportedPathExpression);
        Assert.False(result.Found);
        Assert.Null(result.Value);
        Assert.Equal(ExecutionContextPathResolver.SysPathCallEvaluationFailed, result.WarningReason);
    }
}
