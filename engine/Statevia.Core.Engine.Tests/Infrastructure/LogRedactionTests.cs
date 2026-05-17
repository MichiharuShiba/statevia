using Statevia.Core.Engine.Infrastructure.Logging;
using Xunit;

namespace Statevia.Core.Engine.Tests.Infrastructure;

/// <summary>STV-408: 共通ログマスキングの単体テスト。</summary>
public sealed class LogRedactionTests
{
    /// <summary>ネストした機微キーがマスクされることを検証する。</summary>
    [Fact]
    public void Redact_NestedJsonSensitiveKeys_AreMasked()
    {
        // Arrange
        var json = """{"payload":{"password":"abc","nested":{"authorization":"Bearer xyz"}},"name":"ok"}""";

        // Act
        var redacted = LogRedaction.Redact(json, 500);

        // Assert
        Assert.DoesNotContain("abc", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer xyz", redacted, StringComparison.Ordinal);
        Assert.Contains("\"password\":\"[redacted]\"", redacted, StringComparison.Ordinal);
        Assert.Contains("\"authorization\":\"[redacted]\"", redacted, StringComparison.Ordinal);
        Assert.Contains("ok", redacted, StringComparison.Ordinal);
    }

    /// <summary>null / 空文字は空文字列を返すことを検証する。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Redact_NullOrEmpty_ReturnsEmpty(string? text)
    {
        // Act
        var redacted = LogRedaction.Redact(text, 100);

        // Assert
        Assert.Equal("", redacted);
    }

    /// <summary>クエリのみ（値なし）のとき "?" を維持することを検証する。</summary>
    [Fact]
    public void RedactQueryParameters_EmptyAfterQuestionMark_ReturnsQuestionMark()
    {
        // Act
        var redacted = LogRedaction.RedactQueryParameters("?");

        // Assert
        Assert.Equal("?", redacted);
    }

    /// <summary>先頭が ? でない文字列はそのまま返すことを検証する。</summary>
    [Fact]
    public void RedactQueryParameters_WithoutLeadingQuestion_ReturnsUnchanged()
    {
        // Act
        var redacted = LogRedaction.RedactQueryParameters("user=1&token=secret");

        // Assert
        Assert.Equal("user=1&token=secret", redacted);
    }

    /// <summary>= を含まないクエリ片はそのまま連結されることを検証する。</summary>
    [Fact]
    public void RedactQueryParameters_FlagWithoutEquals_IsPreserved()
    {
        // Act
        var redacted = LogRedaction.RedactQueryParameters("?verbose&user=1");

        // Assert
        Assert.Equal("?verbose&user=1", redacted);
    }

    /// <summary>クエリ文字列の機微キーがマスクされることを検証する。</summary>
    [Fact]
    public void Redact_QuerySensitiveKeys_AreMasked()
    {
        // Arrange
        const string query = "?user=1&accessToken=abc&secret=s3cr3t";

        // Act
        var redacted = LogRedaction.Redact(query, 200);

        // Assert
        Assert.Contains("accessToken=[redacted]", redacted, StringComparison.Ordinal);
        Assert.Contains("secret=[redacted]", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abc", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t", redacted, StringComparison.Ordinal);
    }
}
