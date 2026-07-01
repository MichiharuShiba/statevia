using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Statevia.Core.Api.Hosting;

namespace Statevia.Core.Api.Hosting.OpenApi;

/// <summary>
/// 全操作に <c>X-Tenant-Id</c>、ワークフロー書き込みに <c>X-Idempotency-Key</c> を OpenAPI に追加する。
/// </summary>
internal sealed class TenantAndIdempotencyHeadersOperationFilter : IOperationFilter
{
    private const string IdempotencyKeyHeaderName = "X-Idempotency-Key";

    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        operation.Parameters ??= [];
        operation.Parameters.Add(CreateTenantHeaderParameter());

        if (RequiresIdempotencyKey(context))
            operation.Parameters.Add(CreateIdempotencyKeyParameter());
    }

    private static bool RequiresIdempotencyKey(OperationFilterContext context)
    {
        var httpMethod = context.ApiDescription.HttpMethod;
        if (!string.Equals(httpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            return false;

        var relativePath = context.ApiDescription.RelativePath ?? string.Empty;
        return relativePath.StartsWith("v1/executions", StringComparison.OrdinalIgnoreCase);
    }

    private static OpenApiParameter CreateTenantHeaderParameter() =>
        new()
        {
            Name = TenantHeader.HeaderName,
            In = ParameterLocation.Header,
            Required = false,
            Description = $"テナント ID。未指定時は \"{TenantHeader.DefaultTenantId}\"。",
            Schema = new OpenApiSchema { Type = "string", Default = new Microsoft.OpenApi.Any.OpenApiString(TenantHeader.DefaultTenantId) }
        };

    private static OpenApiParameter CreateIdempotencyKeyParameter() =>
        new()
        {
            Name = IdempotencyKeyHeaderName,
            In = ParameterLocation.Header,
            Required = false,
            Description = "冪等キー（同一キー・同一コマンドの重複は 409）。",
            Schema = new OpenApiSchema { Type = "string" }
        };
}
