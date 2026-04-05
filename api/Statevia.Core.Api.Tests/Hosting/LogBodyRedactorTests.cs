using Statevia.Core.Api.Hosting;

namespace Statevia.Core.Api.Tests.Hosting;

/// <summary>STV-403: ログ用本文マスクの単体テスト。</summary>
public sealed class LogBodyRedactorTests
{
    [Fact]
    public void Redact_TruncatesLongString()
    {
        var s = new string('x', 20);
        var r = LogBodyRedactor.Redact(s, 10);
        Assert.Contains("[truncated]", r);
        Assert.True(r.Length < s.Length + 20);
    }

    [Fact]
    public void Redact_QueryParameterPasswordMasked()
    {
        var r = LogBodyRedactor.Redact("?user=1&password=secret&x=y", 200);
        Assert.Contains("password=[redacted]", r);
        Assert.DoesNotContain("secret", r);
    }

    [Fact]
    public void Redact_JsonPasswordMasked()
    {
        var json = """{"password":"abc","name":"ok"}""";
        var r = LogBodyRedactor.Redact(json, 500);
        Assert.Contains("[redacted]", r);
        Assert.DoesNotContain("abc", r);
        Assert.Contains("ok", r);
    }
}
