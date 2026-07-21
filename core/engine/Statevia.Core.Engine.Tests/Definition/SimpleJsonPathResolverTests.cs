using System.Collections;
using System.Text.Json;
using Statevia.Core.Engine.Definition;
using Xunit;

namespace Statevia.Core.Engine.Tests.Definition;

public class SimpleJsonPathResolverTests
{
    /// <summary>空白パスはサポート外として解決され、元ソースを値として返すことを検証する。</summary>
    [Fact]
    public void Resolve_WhitespacePath_ReturnsUnsupportedWithoutWarning()
    {
        // Arrange
        var source = new Dictionary<string, object?> { ["x"] = 1 };

        // Act
        var r = SimpleJsonPathResolver.Resolve(source, "   ");

        // Assert
        Assert.False(r.IsSupportedPathExpression);
        Assert.False(r.Found);
        Assert.Same(source, r.Value);
        Assert.Null(r.WarningReason);
    }

    /// <summary><c>$</c> はルート全体を指し、辞書ソースで見つかったと判定されることを検証する。</summary>
    [Fact]
    public void Resolve_DollarPath_ReturnsRootValue()
    {
        // Arrange
        var source = new Dictionary<string, object?> { ["x"] = 1 };

        // Act
        var r = SimpleJsonPathResolver.Resolve(source, "$");

        // Assert
        Assert.True(r.IsSupportedPathExpression);
        Assert.True(r.Found);
        Assert.Same(source, r.Value);
        Assert.Null(r.WarningReason);
    }

    /// <summary><c>$</c> でソースが null のときは Found が false になることを検証する。</summary>
    [Fact]
    public void Resolve_DollarPath_WithNullSource_FoundIsFalse()
    {
        // Act
        var r = SimpleJsonPathResolver.Resolve(null, "$");

        // Assert
        Assert.True(r.IsSupportedPathExpression);
        Assert.False(r.Found);
        Assert.Null(r.Value);
    }

    /// <summary><c>$.</c> で始まらないパスは無視理由が付くことを検証する。</summary>
    [Fact]
    public void Resolve_NonDollarDotPath_ReturnsIgnoredReason()
    {
        // Arrange
        var source = new Dictionary<string, object?> { ["x"] = 1 };

        // Act
        var r = SimpleJsonPathResolver.Resolve(source, "x.y");

        // Assert
        Assert.False(r.IsSupportedPathExpression);
        Assert.Equal(SimpleJsonPathResolver.IgnoredNonDollarDotPath, r.WarningReason);
    }

    /// <summary>辞書のネストしたキーを解決できることを検証する。</summary>
    [Fact]
    public void Resolve_NestedDictionary_ReturnsLeafValue()
    {
        // Arrange
        var source = new Dictionary<string, object?>
        {
            ["a"] = new Dictionary<string, object?> { ["b"] = 42 }
        };

        // Act
        var r = SimpleJsonPathResolver.Resolve(source, "$.a.b");

        // Assert
        Assert.True(r.IsSupportedPathExpression);
        Assert.True(r.Found);
        Assert.Equal(42, r.Value);
    }

    /// <summary>存在しないセグメントでは PathSegmentMissing になることを検証する。</summary>
    [Fact]
    public void Resolve_MissingKey_ReturnsPathSegmentMissing()
    {
        // Arrange
        var source = new Dictionary<string, object?> { ["a"] = new Dictionary<string, object?>() };

        // Act
        var r = SimpleJsonPathResolver.Resolve(source, "$.a.missing");

        // Assert
        Assert.True(r.IsSupportedPathExpression);
        Assert.False(r.Found);
        Assert.Equal(SimpleJsonPathResolver.PathSegmentMissing, r.WarningReason);
    }

    /// <summary>中間がマッピングでないとき PathTraversalNotMapping になることを検証する。</summary>
    [Fact]
    public void Resolve_LeafIsNotMapping_ReturnsTraversalWarning()
    {
        // Arrange
        var source = new Dictionary<string, object?> { ["a"] = "text" };

        // Act
        var r = SimpleJsonPathResolver.Resolve(source, "$.a.b");

        // Assert
        Assert.True(r.IsSupportedPathExpression);
        Assert.False(r.Found);
        Assert.Equal(SimpleJsonPathResolver.PathTraversalNotMapping, r.WarningReason);
    }

    /// <summary><see cref="JsonElement"/> オブジェクトに対してプロパティトラバースできることを検証する。</summary>
    [Fact]
    public void Resolve_JsonElementObject_ReturnsPropertyValue()
    {
        // Arrange
        using var doc = JsonDocument.Parse("""{"outer":{"inner":99}}""");
        var root = doc.RootElement;

        // Act
        var r = SimpleJsonPathResolver.Resolve(root, "$.outer.inner");

        // Assert
        Assert.True(r.IsSupportedPathExpression);
        Assert.True(r.Found);
        var je = Assert.IsType<JsonElement>(r.Value);
        Assert.Equal(99, je.GetInt32());
    }

    /// <summary><see cref="IDictionary"/>（非 <see cref="IReadOnlyDictionary{TKey,TValue}"/>）でもセグメント解決できることを検証する。</summary>
    [Fact]
    public void Resolve_NonReadOnlyDictionary_TraversesSegments()
    {
        // Arrange
        IDictionary table = new Hashtable { ["a"] = new Hashtable { ["b"] = "leaf" } };

        // Act
        var r = SimpleJsonPathResolver.Resolve(table, "$.a.b");

        // Assert
        Assert.True(r.IsSupportedPathExpression);
        Assert.True(r.Found);
        Assert.Equal("leaf", r.Value);
    }

    /// <summary><see cref="JsonElement"/> で存在しないプロパティは PathSegmentMissing になることを検証する。</summary>
    [Fact]
    public void Resolve_JsonElementMissingProperty_ReturnsPathSegmentMissing()
    {
        // Arrange
        using var doc = JsonDocument.Parse("""{"a":{}}""");
        var root = doc.RootElement;

        // Act
        var r = SimpleJsonPathResolver.Resolve(root, "$.a.missing");

        // Assert
        Assert.True(r.IsSupportedPathExpression);
        Assert.False(r.Found);
        Assert.Equal(SimpleJsonPathResolver.PathSegmentMissing, r.WarningReason);
    }


    /// <summary>ブラケット引用キーでドット付きプロパティを解決できることを検証する。</summary>
    [Fact]
    public void Resolve_BracketQuotedKey_ReturnsLeafValue()
    {
        // Arrange
        var source = new Dictionary<string, object?>
        {
            ["states"] = new Dictionary<string, object?>
            {
                ["order.notify.customer"] = new Dictionary<string, object?>
                {
                    ["output"] = new Dictionary<string, object?> { ["ok"] = true }
                }
            }
        };

        // Act
        var r = SimpleJsonPathResolver.Resolve(source, "$.states['order.notify.customer'].output.ok");

        // Assert
        Assert.True(r.IsSupportedPathExpression);
        Assert.True(r.Found);
        Assert.Equal(true, r.Value);
    }
}
