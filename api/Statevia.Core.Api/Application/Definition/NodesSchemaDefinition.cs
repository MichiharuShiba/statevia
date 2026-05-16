namespace Statevia.Core.Api.Application.Definition;

using static NodesSchemaLiteral;

/// <summary>
/// UI 補完/Lint 用の nodes 形式スキーマ定義を返す。
/// </summary>
internal static class NodesSchemaDefinition
{
    public const string SchemaVersion = "1.0.2";
    public const int NodesVersion = 1;

    public static object CreateSchemaDocument() => new
    {
        type = TypeObject,
        required = new[] { Version, Workflow, Nodes },
        properties = new
        {
            version = new
            {
                type = TypeInteger,
                @enum = new[] { NodesVersion }
            },
            workflow = new
            {
                type = TypeObject,
                properties = new
                {
                    id = new { type = TypeString },
                    name = new { type = TypeString },
                    description = new
                    {
                        type = TypeString,
                        description = "Authoring metadata (optional)."
                    }
                }
            },
            nodes = new
            {
                type = TypeArray,
                minItems = 1,
                items = new
                {
                    type = TypeObject,
                    required = new[] { Id, Type },
                    properties = new
                    {
                        id = new { type = TypeString },
                        type = new
                        {
                            type = TypeString,
                            @enum = new[]
                            {
                                NodeTypeStart,
                                NodeTypeEnd,
                                NodeTypeAction,
                                NodeTypeWait,
                                NodeTypeFork,
                                NodeTypeJoin,
                            }
                        },
                        next = new { type = TypeString },
                        error = new
                        {
                            oneOf = new object[]
                            {
                                new { type = TypeString },
                                new
                                {
                                    type = TypeObject,
                                    properties = new
                                    {
                                        id = new { type = TypeString }
                                    },
                                    required = new[] { Id }
                                }
                            }
                        },
                        action = new { type = TypeString },
                        @event = new { type = TypeString },
                        mode = new
                        {
                            type = TypeString,
                            @enum = new[] { JoinModeAll }
                        },
                        branches = new
                        {
                            type = TypeArray,
                            items = new { type = TypeString }
                        },
                        input = new
                        {
                            description =
                                "Action input mapping: path strings ($ / $.seg...) or literals; object map matches ParseStrictInputMapping.",
                            anyOf = new object[]
                            {
                                new { type = TypeString },
                                new
                                {
                                    type = TypeObject,
                                    additionalProperties = true
                                }
                            }
                        },
                        edges = new
                        {
                            type = TypeArray,
                            items = new
                            {
                                type = TypeObject,
                                required = new[] { To },
                                properties = new
                                {
                                    to = new
                                    {
                                        oneOf = new object[]
                                        {
                                            new { type = TypeString },
                                            new
                                            {
                                                type = TypeObject,
                                                properties = new
                                                {
                                                    id = new { type = TypeString }
                                                }
                                            }
                                        }
                                    },
                                    when = new
                                    {
                                        type = TypeObject,
                                        required = new[] { Path, Op },
                                        properties = new
                                        {
                                            path = new { type = TypeString },
                                            op = new { type = TypeString },
                                            value = new
                                            {
                                                type = new[]
                                                {
                                                    TypeString,
                                                    TypeNumber,
                                                    TypeBoolean,
                                                    TypeArray,
                                                    TypeObject,
                                                    TypeNull,
                                                }
                                            }
                                        }
                                    },
                                    order = new { type = TypeInteger },
                                    @default = new { type = TypeBoolean }
                                }
                            }
                        }
                    }
                }
            }
        }
    };
}
