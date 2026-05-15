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
