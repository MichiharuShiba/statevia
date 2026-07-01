using System.Globalization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Statevia.Service.Api.Contracts;

namespace Statevia.Service.Api.Hosting.OpenApi;

/// <summary>
/// <see cref="ApiExceptionFilter"/> の契約に沿い、主要操作へエラー応答を付与する。
/// </summary>
internal sealed class DefaultErrorResponsesOperationFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        if (IsHealthOperation(context))
            return;

        AddErrorResponse(operation, context, StatusCodes.Status404NotFound, "Not Found");
        AddErrorResponse(operation, context, StatusCodes.Status422UnprocessableEntity, "Validation Error");
        AddErrorResponse(operation, context, StatusCodes.Status500InternalServerError, "Internal Server Error");

        if (RequiresIdempotencyConflict(context))
            AddErrorResponse(operation, context, StatusCodes.Status409Conflict, "Idempotency Conflict");
    }

    private static bool IsHealthOperation(OperationFilterContext context) =>
        string.Equals(context.ApiDescription.RelativePath, "v1/health", StringComparison.OrdinalIgnoreCase);

    private static bool RequiresIdempotencyConflict(OperationFilterContext context)
    {
        var httpMethod = context.ApiDescription.HttpMethod;
        if (!string.Equals(httpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            return false;

        var relativePath = context.ApiDescription.RelativePath ?? string.Empty;
        return relativePath.StartsWith("v1/executions", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddErrorResponse(
        OpenApiOperation operation,
        OperationFilterContext context,
        int statusCode,
        string description)
    {
        operation.Responses.TryAdd(
            statusCode.ToString(CultureInfo.InvariantCulture),
            new OpenApiResponse
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new()
                    {
                        Schema = context.SchemaGenerator.GenerateSchema(typeof(ErrorResponse), context.SchemaRepository)
                    }
                }
            });
    }
}
