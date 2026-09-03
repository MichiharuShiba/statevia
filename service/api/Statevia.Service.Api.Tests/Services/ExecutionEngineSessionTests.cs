using Statevia.Core.Application.Services;
using Statevia.Core.Engine.Abstractions;
using System.Text.Json;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="ExecutionEngineSession.TryParseRuntimeCheckpoint"/> の hydrate 用検証。</summary>
public sealed class ExecutionEngineSessionTests
{
    /// <summary>空・空白・seed 空オブジェクトは不正。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{}")]
    [InlineData("null")]
    public void TryParseRuntimeCheckpoint_WhenEmptyOrSeed_ReturnsFalse(string? checkpointJson)
    {
        // Arrange / Act
        var ok = ExecutionEngineSession.TryParseRuntimeCheckpoint(checkpointJson, out _, out var parseError);

        // Assert
        Assert.False(ok);
        Assert.Null(parseError);
    }

    /// <summary>不正 JSON は false と parseError を返す。</summary>
    [Fact]
    public void TryParseRuntimeCheckpoint_WhenInvalidJson_ReturnsFalseWithParseError()
    {
        // Arrange / Act
        var ok = ExecutionEngineSession.TryParseRuntimeCheckpoint("{not-json", out _, out var parseError);

        // Assert
        Assert.False(ok);
        Assert.NotNull(parseError);
        Assert.IsType<JsonException>(parseError);
    }

    /// <summary>必須フィールド欠落は false と parseError を返す。</summary>
    [Fact]
    public void TryParseRuntimeCheckpoint_WhenRequiredFieldsMissing_ReturnsFalseWithParseError()
    {
        // Arrange
        const string incomplete = """{"schemaVersion":1,"executionId":"e1"}""";

        // Act
        var ok = ExecutionEngineSession.TryParseRuntimeCheckpoint(incomplete, out _, out var parseError);

        // Assert
        Assert.False(ok);
        Assert.NotNull(parseError);
        Assert.IsType<JsonException>(parseError);
    }

    /// <summary>正規断面（空 Active/Wait 含む）は hydrate 可能。</summary>
    [Fact]
    public void TryParseRuntimeCheckpoint_WhenValidMinimalCheckpoint_ReturnsTrue()
    {
        // Arrange
        var checkpoint = CreateMinimalCheckpoint();
        var json = JsonSerializer.Serialize(checkpoint, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        var ok = ExecutionEngineSession.TryParseRuntimeCheckpoint(json, out var parsed, out var parseError);

        // Assert
        Assert.True(ok);
        Assert.Null(parseError);
        Assert.Equal("exec-1", parsed.ExecutionId);
        Assert.Equal("def", parsed.DefinitionName);
        Assert.Empty(parsed.ActiveStates);
        Assert.Empty(parsed.PendingWaits);
    }

    private static ExecutionRuntimeCheckpoint CreateMinimalCheckpoint() =>
        new()
        {
            ExecutionId = "exec-1",
            DefinitionName = "def",
            ActiveStates = [],
            StateAttempts = new Dictionary<string, int>(StringComparer.Ordinal),
            StateOutputs = new Dictionary<string, JsonElement?>(StringComparer.Ordinal),
            AppliedPublishClientEventIds = [],
            AppliedCancelClientEventIds = [],
            Context = new CheckpointContextData
            {
                States = new Dictionary<string, JsonElement?>(StringComparer.Ordinal)
            },
            Graph = new CheckpointGraphData
            {
                Nodes = [],
                Edges = []
            },
            Join = new CheckpointJoinData
            {
                JoinStateResults = new Dictionary<string, IReadOnlyDictionary<string, CheckpointJoinObserved>>(
                    StringComparer.OrdinalIgnoreCase),
                JoinSourceNodeIds = new Dictionary<string, IReadOnlyDictionary<string, string>>(
                    StringComparer.OrdinalIgnoreCase),
                StartedJoins = []
            },
            PendingWaits = []
        };
}
