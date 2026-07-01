using System.Text.Json;
using Statevia.Actions.Abstractions.Execution;
using Statevia.Core.Api.Application.Actions.Execution;

namespace Statevia.Core.Api.Tests.Application.Actions.Execution;

/// <summary><see cref="ActionExecutionRuntimeInputMapper"/> の単体テスト。</summary>
public sealed class ActionExecutionRuntimeInputMapperTests
{
    /// <summary>ランタイム入力を JSON 入力としてリクエストへ反映する。</summary>
    [Fact]
    public void WithRuntimeInput_WhenObjectProvided_SetsInputJson()
    {
        // Arrange
        var request = new ActionExecutionRequest
        {
            ExecutionId = "exec-1",
            StateName = "Echo",
            ActionId = "test.module.echo",
            TenantId = "00000000-0000-4000-8000-000000000001",
        };

        // Act
        var mapped = ActionExecutionRuntimeInputMapper.WithRuntimeInput(
            request,
            new { message = "hello" });

        // Assert
        Assert.NotNull(mapped.Input);
        Assert.Equal("hello", mapped.Input.Value.GetProperty("message").GetString());
    }

    /// <summary>OutOfProcess 出力 JSON を Engine 向けオブジェクトへ変換する。</summary>
    [Fact]
    public void ToRuntimeOutput_WhenOutputJsonPresent_ReturnsJsonElement()
    {
        // Arrange
        using var document = JsonDocument.Parse("""{"ok":true}""");
        var result = new ActionExecutionResult
        {
            Success = true,
            Output = document.RootElement.Clone(),
        };

        // Act
        var runtimeOutput = ActionExecutionRuntimeInputMapper.ToRuntimeOutput(result);

        // Assert
        Assert.IsType<JsonElement>(runtimeOutput);
    }
}
