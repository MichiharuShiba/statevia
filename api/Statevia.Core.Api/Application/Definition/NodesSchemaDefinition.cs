namespace Statevia.Core.Api.Application.Definition;

/// <summary>
/// UI 補完/Lint 用の nodes 形式スキーマ定義を返す。
/// </summary>
public static class NodesSchemaDefinition
{
    public const string SchemaVersion = "1.0.1";
    public const int NodesVersion = 1;

    public static object CreateSchemaDocument() => new
    {
        type = "object",
        required = new[] { "version", "workflow", "nodes" },
        properties = new
        {
            version = new
            {
                type = "integer",
                @enum = new[] { 1 }
            },
            workflow = new
            {
                type = "object",
                properties = new
                {
                    id = new { type = "string" },
                    name = new { type = "string" },
                    description = new
                    {
                        type = "string",
                        description = "Authoring metadata (optional)."
                    }
                }
            },
            nodes = new
            {
                type = "array",
                minItems = 1,
                items = new
                {
                    type = "object",
                    required = new[] { "id", "type" },
                    properties = new
                    {
                        id = new { type = "string" },
                        type = new
                        {
                            type = "string",
                            @enum = new[] { "start", "end", "action", "wait", "fork", "join" }
                        },
                        next = new { type = "string" },
                        action = new { type = "string" },
                        @event = new { type = "string" },
                        mode = new { type = "string", @enum = new[] { "all" } },
                        branches = new
                        {
                            type = "array",
                            items = new { type = "string" }
                        },
                        input = new
                        {
                            description =
                                "Action input mapping: path strings ($ / $.seg...) or literals; object map matches ParseStrictInputMapping.",
                            anyOf = new object[]
                            {
                                new { type = "string" },
                                new
                                {
                                    type = "object",
                                    additionalProperties = true
                                }
                            }
                        },
                        edges = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                required = new[] { "to" },
                                properties = new
                                {
                                    to = new
                                    {
                                        oneOf = new object[]
                                        {
                                            new { type = "string" },
                                            new
                                            {
                                                type = "object",
                                                properties = new
                                                {
                                                    id = new { type = "string" }
                                                }
                                            }
                                        }
                                    },
                                    when = new
                                    {
                                        type = "object",
                                        required = new[] { "path", "op" },
                                        properties = new
                                        {
                                            path = new { type = "string" },
                                            op = new { type = "string" },
                                            value = new { type = new[] { "string", "number", "boolean", "array", "object", "null" } }
                                        }
                                    },
                                    order = new { type = "integer" },
                                    @default = new { type = "boolean" }
                                }
                            }
                        }
                    }
                }
            }
        }
    };
}
