using System.Text.Json;
using Statevia.Actions.Abstractions.Execution;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>Engine ランタイム入力と Platform DTO の相互変換。</summary>
internal static class ActionExecutionRuntimeInputMapper
{
    /// <summary>実行リクエストにランタイム入力を反映する。</summary>
    /// <param name="request">元の実行リクエスト。</param>
    /// <param name="runtimeInput">Engine が解決した入力。</param>
    /// <returns>入力を付与したリクエスト。</returns>
    public static ActionExecutionRequest WithRuntimeInput(
        ActionExecutionRequest request,
        object? runtimeInput)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (runtimeInput is null)
        {
            return request;
        }

        return request with
        {
            Input = ToJsonElement(runtimeInput),
        };
    }

    /// <summary>OutOfProcess 結果の出力を Engine 向けオブジェクトへ変換する。</summary>
    /// <param name="result">Platform 実行結果。</param>
    /// <returns>Engine へ返す出力。</returns>
    public static object? ToRuntimeOutput(ActionExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.RuntimeOutput is not null)
        {
            return result.RuntimeOutput;
        }

        return result.Output;
    }

    private static JsonElement ToJsonElement(object runtimeInput)
    {
        if (runtimeInput is JsonElement jsonElement)
        {
            return jsonElement;
        }

        return JsonSerializer.SerializeToElement(runtimeInput);
    }
}
