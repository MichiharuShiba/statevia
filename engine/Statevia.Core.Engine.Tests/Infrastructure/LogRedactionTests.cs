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
        Assert.DoesNotContain("abc", redacted);
        Assert.DoesNotContain("Bearer xyz", redacted);
        Assert.Contains("\"password\":\"[redacted]\"", redacted);
        Assert.Contains("\"authorization\":\"[redacted]\"", redacted);
        Assert.Contains("ok", redacted);
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
        Assert.Contains("accessToken=[redacted]", redacted);
        Assert.Contains("secret=[redacted]", redacted);
        Assert.DoesNotContain("abc", redacted);
        Assert.DoesNotContain("s3cr3t", redacted);
    }
}
