using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Statevia.Core.Api.Hosting.OpenApi;

/// <summary>
/// <c>GET /v1/executions/{{id}}/graph</c> の応答を ExecutionGraph 参照付きの弱い object スキーマにする。
/// </summary>
internal sealed class ExecutionGraphResponseOperationFilter : IOperationFilter
{
    private const string GraphDescription =
        "実行グラフ JSON（engine 契約）。詳細は docs/core-engine-execution-graph-spec.md を参照。";

    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var relativePath = context.ApiDescription.RelativePath ?? string.Empty;
        if (!relativePath.Contains("/graph", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(context.ApiDescription.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!operation.Responses.TryGetValue("200", out var success))
            return;

        success.Content = new Dictionary<string, OpenApiMediaType>
        {
            ["application/json"] = new()
            {
                Schema = new OpenApiSchema
                {
                    Type = "object",
                    AdditionalPropertiesAllowed = true,
                    Description = GraphDescription
                }
            }
        };
    }
}
