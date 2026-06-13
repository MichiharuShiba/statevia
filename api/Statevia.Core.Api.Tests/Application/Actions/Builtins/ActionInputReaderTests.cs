using Statevia.Core.Api.Application.Actions.Builtins;

namespace Statevia.Core.Api.Tests.Application.Actions.Builtins;

/// <summary><see cref="ActionInputReader"/> の入力解釈テスト。</summary>
public sealed class ActionInputReaderTests
{
    /// <summary>duration 文字列を TimeSpan に変換できる。</summary>
    [Fact]
    public void ParseDurationString_Seconds_ReturnsTimeSpan()
    {
        // Act
        var duration = ActionInputReader.ParseDurationString("5s");

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(5), duration);
    }

    /// <summary>duration ミリ秒文字列を TimeSpan に変換できる。</summary>
    [Fact]
    public void ParseDurationString_Milliseconds_ReturnsTimeSpan()
    {
        // Act
        var duration = ActionInputReader.ParseDurationString("500ms");

        // Assert
        Assert.Equal(TimeSpan.FromMilliseconds(500), duration);
    }

    /// <summary>数値 duration フィールドをミリ秒として解釈する。</summary>
    [Fact]
    public void ParseDuration_NumberField_UsesMilliseconds()
    {
        // Arrange
        var fields = new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["duration"] = System.Text.Json.JsonSerializer.SerializeToElement(250),
        };

        // Act
        var duration = ActionInputReader.ParseDuration(fields);

        // Assert
        Assert.Equal(TimeSpan.FromMilliseconds(250), duration);
    }

    /// <summary>notify 短名相当の必須文字列を取得できる。</summary>
    [Fact]
    public void RequireString_MissingField_Throws()
    {
        // Arrange
        var fields = new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.OrdinalIgnoreCase);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => ActionInputReader.RequireString(fields, "url"));
    }
}
