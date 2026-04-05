using Statevia.Core.Api.Hosting;

namespace Statevia.Core.Api.Tests.Hosting;

/// <summary>STV-403: 応答 tracestate マージの単体テスト。</summary>
public sealed class TracestateHelperTests
{
    [Fact]
    public void Merge_ReplacesExistingVendorMember()
    {
        var existing = "foo=1,st@statevia=old,bar=2";
        var merged = TracestateHelper.Merge(existing, TracestateHelper.StateviaVendorKey, "newval");
        Assert.Contains("foo=1", merged, StringComparison.Ordinal);
        Assert.Contains("bar=2", merged, StringComparison.Ordinal);
        Assert.Contains("st@statevia=newval", merged, StringComparison.Ordinal);
        Assert.DoesNotContain("old", merged, StringComparison.Ordinal);
    }

    [Fact]
    public void Merge_AppendsWhenNoExistingHeader()
    {
        var merged = TracestateHelper.Merge(null, TracestateHelper.StateviaVendorKey, "x");
        Assert.Equal("st@statevia=x", merged);
    }

    [Fact]
    public void Merge_TruncatesToMaxHeaderChars()
    {
        var longVal = new string('a', 400);
        var merged = TracestateHelper.Merge(null, TracestateHelper.StateviaVendorKey, longVal);
        Assert.True(merged.Length <= TracestateHelper.MaxHeaderChars);
    }
}
