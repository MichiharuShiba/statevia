using System.Text.Json;
using Statevia.Core.Api.Infrastructure;

namespace Statevia.Core.Api.Tests.Infrastructure;

/// <summary><see cref="JsonDeserialize"/> の復元ヘルパーテスト。</summary>
public sealed class JsonDeserializeTests
{
    /// <summary>有効な JSON は復元に成功する。</summary>
    [Fact]
    public void TryDeserialize_ReturnsTrue_ForValidJson()
    {
        // Arrange
        const string json = """{"name":"x"}""";

        // Act
        var ok = JsonDeserialize.TryDeserialize(json, JsonDeserialize.CaseInsensitiveDeserializeOptions, out JsonElement? value);

        // Assert
        Assert.True(ok);
        Assert.Equal("x", value!.Value.GetProperty("name").GetString());
    }

    /// <summary>不正な JSON は false を返し値は default。</summary>
    [Fact]
    public void TryDeserialize_ReturnsFalse_ForMalformedJson()
    {
        // Arrange
        const string json = "not-json";

        // Act
        var ok = JsonDeserialize.TryDeserialize(json, null, out JsonElement? value);

        // Assert
        Assert.False(ok);
        Assert.Null(value);
    }

    /// <summary>null 入力は ArgumentNullException。</summary>
    [Fact]
    public void TryDeserialize_Throws_WhenJsonNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            JsonDeserialize.TryDeserialize<JsonElement>(null!, null, out _));
    }
}
