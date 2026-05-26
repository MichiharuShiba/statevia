using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Statevia.Core.Api.Hosting.OpenApi;

/// <summary>
/// 一覧 API の <c>limit</c> / <c>offset</c> を OpenAPI 上で必須相当にする。
/// </summary>
internal sealed class ListPagingQueryOperationFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context) =>
        ApplyPagingQueryRequirements(operation, context);

    private static void ApplyPagingQueryRequirements(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var relativePath = context.ApiDescription.RelativePath ?? string.Empty;
        if (!IsPagedListPath(relativePath))
            return;

        MarkQueryParameterRequired(operation, "limit");
        MarkQueryParameterRequired(operation, "offset");
    }

    private static bool IsPagedListPath(string relativePath) =>
        string.Equals(relativePath, "v1/definitions", StringComparison.OrdinalIgnoreCase)
        || string.Equals(relativePath, "v1/executions", StringComparison.OrdinalIgnoreCase);

    private static void MarkQueryParameterRequired(OpenApiOperation operation, string name)
    {
        var parameter = operation.Parameters?.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
            && p.In == ParameterLocation.Query);

        if (parameter is null)
            return;

        parameter.Required = true;
        parameter.Description = name switch
        {
            "limit" => "ページサイズ（必須。1〜500）。",
            "offset" => "先頭からのオフセット（0 以上）。",
            _ => parameter.Description
        };
    }
}
