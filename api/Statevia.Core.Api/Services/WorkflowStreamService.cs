using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Statevia.Core.Api.Abstractions.Services;

namespace Statevia.Core.Api.Services;

/// <summary>
/// GET …/stream 用の SSE。グラフ JSON の変化を <see cref="GraphUpdated"/> 相当の JSON で送出する。
/// </summary>
public sealed class WorkflowStreamService
{
    /// <summary>
    /// 投影済みグラフ JSON の取得・比較ループ間隔（ms）。フェーズ 1 は 2 秒固定（`docs/statevia-data-integration-contract.md` の SSE 節と整合）。
    /// </summary>
    internal const int GraphPollingIntervalMilliseconds = 2000;

    private readonly IWorkflowService _workflows;
    private readonly IDisplayIdService _displayIds;

    public WorkflowStreamService(IWorkflowService workflows, IDisplayIdService displayIds)
    {
        _workflows = workflows;
        _displayIds = displayIds;
    }

    public async Task WriteStreamAsync(HttpResponse response, string tenantId, string idOrUuid, CancellationToken ct)
    {
        var uuid = await _displayIds.ResolveAsync("workflow", idOrUuid, ct).ConfigureAwait(false);
        if (uuid is null)
        {
            response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var displayId = await _displayIds.GetDisplayIdAsync("workflow", idOrUuid, ct).ConfigureAwait(false) ?? idOrUuid;

        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache, no-transform";
        response.Headers.Connection = "keep-alive";
        response.Headers["X-Accel-Buffering"] = "no";

        var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        string? lastHash = null;

        while (!ct.IsCancellationRequested)
        {
            string graphJson;
            try
            {
                graphJson = await _workflows.GetGraphJsonAsync(tenantId, idOrUuid, ct).ConfigureAwait(false);
            }
            catch
            {
                await Task.Delay(GraphPollingIntervalMilliseconds, ct).ConfigureAwait(false);
                continue;
            }

            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(graphJson)));
            if (!string.Equals(hash, lastHash, StringComparison.Ordinal))
            {
                lastHash = hash;
                var patchNodes = WorkflowViewMapper.MapGraphPatchNodes(graphJson);
                var payload = JsonSerializer.Serialize(
                    new
                    {
                        type = "GraphUpdated",
                        executionId = displayId,
                        patch = new { nodes = patchNodes }
                    },
                    jsonOpts);
                await response.WriteAsync($"data: {payload}\n\n", ct).ConfigureAwait(false);
                await response.Body.FlushAsync(ct).ConfigureAwait(false);
            }

            await Task.Delay(GraphPollingIntervalMilliseconds, ct).ConfigureAwait(false);
        }
    }
}
