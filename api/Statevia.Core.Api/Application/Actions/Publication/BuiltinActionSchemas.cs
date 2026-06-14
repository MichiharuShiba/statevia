using System.Text.Json;
using Statevia.Actions.Abstractions.Publication;
using ActionPublication = Statevia.Actions.Abstractions.Publication.ActionPublication;
using PublicationDescriptor = Statevia.Actions.Abstractions.Publication.ActionDescriptor;

namespace Statevia.Core.Api.Application.Actions.Publication;

/// <summary>Builtin action の input/output JSON Schema と ActionPublication を提供する。</summary>
internal static class BuiltinActionSchemas
{
    private const string SchemaBaseUri = "https://statevia.dev/schemas/actions";
    private const string ValueKindKeyword = StateviaActionSchemaVocabulary.ValueKindKeyword;
    private const string ValueKindLiteralOrPath = StateviaActionSchemaVocabulary.ValueKindLiteralOrPath;

    /// <summary>canonical actionId に対応する Builtin ActionPublication を返す。</summary>
    /// <param name="actionId">canonical actionId。</param>
    /// <param name="displayName">表示名。</param>
    /// <param name="category">Capability カテゴリ名。</param>
    /// <param name="inputSchemaJson">input JSON Schema 文字列。</param>
    /// <param name="outputSchemaJson">output JSON Schema 文字列。</param>
    /// <param name="fieldOrder">UI フィールド順。</param>
    /// <param name="fields">UI フィールドヒント。</param>
    public static ActionPublication Create(
        string actionId,
        string displayName,
        string category,
        string inputSchemaJson,
        string outputSchemaJson,
        IReadOnlyList<string>? fieldOrder = null,
        IReadOnlyDictionary<string, ActionFieldUiHints>? fields = null) =>
        new(
            new PublicationDescriptor(
                actionId,
                "1.0.0",
                displayName,
                Category: category),
            new ActionSchemaBundle(
                JsonDocument.Parse(inputSchemaJson),
                JsonDocument.Parse(outputSchemaJson)),
            fieldOrder is null && fields is null
                ? null
                : new ActionUiMetadata(fieldOrder, fields));

    /// <summary>noop action の Publication。</summary>
    public static ActionPublication NoOp(string actionId) =>
        Create(
            actionId,
            "No-op",
            "Transform",
            $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{SchemaBaseUri}}/{{actionId}}/input",
              "type": "object",
              "additionalProperties": true
            }
            """,
            $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{SchemaBaseUri}}/{{actionId}}/output"
            }
            """);

    /// <summary>sleep action の Publication。</summary>
    public static ActionPublication Sleep(string actionId)
    {
        var fields = new Dictionary<string, ActionFieldUiHints>
        {
            ["duration"] = FieldHints(actionId, "duration", widget: "text"),
        };
        return Create(
            actionId,
            "Sleep",
            "Timing",
            $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{SchemaBaseUri}}/{{actionId}}/input",
              "type": "object",
              "additionalProperties": false,
              "required": ["duration"],
              "properties": {
                "duration": {
                  "title": "duration",
                  "oneOf": [
                    { "type": "string" },
                    { "type": "integer", "minimum": 0 }
                  ],
                  "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                }
              }
            }
            """,
            $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{SchemaBaseUri}}/{{actionId}}/output",
              "type": "object",
              "additionalProperties": false
            }
            """,
            fieldOrder: ["duration"],
            fields: fields);
    }

    /// <summary>rest action の Publication。</summary>
    public static ActionPublication Rest(string actionId)
    {
        var fields = new Dictionary<string, ActionFieldUiHints>
        {
            ["url"] = FieldHints(actionId, "url", widget: "url"),
            ["method"] = FieldHints(actionId, "method", widget: "select"),
            ["headers"] = FieldHints(actionId, "headers", widget: "text"),
            ["body"] = FieldHints(actionId, "body", widget: "text"),
            ["timeout"] = FieldHints(actionId, "timeout", widget: "text"),
            ["idempotencyKey"] = FieldHints(actionId, "idempotencyKey", widget: "text", sensitive: true),
        };
        return Create(
            actionId,
            "REST",
            "Http",
            $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{SchemaBaseUri}}/{{actionId}}/input",
              "type": "object",
              "additionalProperties": false,
              "required": ["url", "method"],
              "properties": {
                "url": {
                  "type": "string",
                  "format": "uri",
                  "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                },
                "method": {
                  "type": "string",
                  "enum": ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"],
                  "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                },
                "headers": {
                  "type": "object",
                  "additionalProperties": { "type": "string" },
                  "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                },
                "body": {
                  "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                },
                "timeout": {
                  "type": "integer",
                  "minimum": 1,
                  "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                },
                "idempotencyKey": {
                  "type": "string",
                  "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                }
              }
            }
            """,
            $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{SchemaBaseUri}}/{{actionId}}/output",
              "type": "object",
              "additionalProperties": false,
              "required": ["statusCode", "headers", "body"],
              "properties": {
                "statusCode": { "type": "integer" },
                "headers": {
                  "type": "object",
                  "additionalProperties": { "type": "string" }
                },
                "body": {}
              }
            }
            """,
            fieldOrder: ["url", "method", "headers", "body", "timeout", "idempotencyKey"],
            fields: fields);
    }

    /// <summary>notify action の Publication。</summary>
    public static ActionPublication Notify(string actionId)
    {
        var fields = new Dictionary<string, ActionFieldUiHints>
        {
            ["channel"] = FieldHints(actionId, "channel", widget: "select"),
            ["to"] = FieldHints(actionId, "to", widget: "text"),
            ["subject"] = FieldHints(actionId, "subject", widget: "text"),
            ["body"] = FieldHints(actionId, "body", widget: "text"),
            ["from"] = FieldHints(actionId, "from", widget: "text"),
        };
        return Create(
            actionId,
            "Notification",
            "Notification",
            $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{SchemaBaseUri}}/{{actionId}}/input",
              "type": "object",
              "additionalProperties": false,
              "required": ["channel", "to", "subject", "body"],
              "properties": {
                "channel": {
                  "type": "string",
                  "enum": ["email"],
                  "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                },
                "to": {
                  "type": "string",
                  "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                },
                "subject": {
                  "type": "string",
                  "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                },
                "body": {
                  "type": "string",
                  "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                },
                "from": {
                  "type": "string",
                  "format": "email",
                  "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                }
              }
            }
            """,
            $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{SchemaBaseUri}}/{{actionId}}/output",
              "type": "object",
              "additionalProperties": false,
              "required": ["channel"],
              "properties": {
                "channel": { "type": "string" },
                "messageId": { "type": ["string", "null"] }
              }
            }
            """,
            fieldOrder: ["channel", "to", "subject", "body", "from"],
            fields: fields);
    }

    /// <summary>signal action の Publication。</summary>
    public static ActionPublication Signal(string actionId)
    {
        var fields = new Dictionary<string, ActionFieldUiHints>
        {
            ["target"] = FieldHints(actionId, "target", widget: "select"),
            ["signal"] = FieldHints(actionId, "signal", widget: "text"),
        };
        return Create(
            actionId,
            "Signal",
            "Signal",
            $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{SchemaBaseUri}}/{{actionId}}/input",
              "type": "object",
              "additionalProperties": false,
              "required": ["signal"],
              "properties": {
                "target": {
                  "type": "string",
                  "enum": ["current"],
                  "default": "current",
                  "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                },
                "signal": {
                  "type": "string",
                  "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                }
              }
            }
            """,
            $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{SchemaBaseUri}}/{{actionId}}/output",
              "type": "object",
              "additionalProperties": false
            }
            """,
            fieldOrder: ["target", "signal"],
            fields: fields);
    }

    /// <summary>publish action の Publication。</summary>
    public static ActionPublication Publish(string actionId)
    {
        var fields = new Dictionary<string, ActionFieldUiHints>
        {
            ["topic"] = FieldHints(actionId, "topic", widget: "text"),
            ["payload"] = FieldHints(actionId, "payload", widget: "text"),
        };
        return Create(
            actionId,
            "Publish",
            "Event",
            $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{SchemaBaseUri}}/{{actionId}}/input",
              "type": "object",
              "additionalProperties": false,
              "required": ["topic"],
              "properties": {
                "topic": {
                  "type": "string",
                  "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                },
                "payload": {
                  "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                }
              }
            }
            """,
            $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{SchemaBaseUri}}/{{actionId}}/output",
              "type": "object",
              "additionalProperties": false,
              "required": ["topic", "dispatched"],
              "properties": {
                "topic": { "type": "string" },
                "dispatched": { "type": "boolean" }
              }
            }
            """,
            fieldOrder: ["topic", "payload"],
            fields: fields);
    }

    /// <summary>workflow action の Publication。</summary>
    public static ActionPublication Workflow(string actionId)
    {
        var fields = new Dictionary<string, ActionFieldUiHints>
        {
            ["definitionId"] = FieldHints(actionId, "definitionId", widget: "text"),
            ["mode"] = FieldHints(actionId, "mode", widget: "select"),
            ["input"] = FieldHints(actionId, "input", widget: "text"),
            ["timeout"] = FieldHints(actionId, "timeout", widget: "text"),
        };
        return Create(
            actionId,
            "Workflow",
            "Workflow",
            $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{SchemaBaseUri}}/{{actionId}}/input",
              "type": "object",
              "additionalProperties": false,
              "required": ["definitionId", "mode"],
              "properties": {
                "definitionId": {
                  "type": "string",
                  "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                },
                "mode": {
                  "type": "string",
                  "enum": ["async", "sync"],
                  "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                },
                "input": {
                  "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                },
                "timeout": {
                  "type": "integer",
                  "minimum": 1,
                  "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                }
              }
            }
            """,
            $$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "{{SchemaBaseUri}}/{{actionId}}/output",
              "type": "object",
              "additionalProperties": false,
              "required": ["workflowId", "displayId", "status"],
              "properties": {
                "workflowId": { "type": "string" },
                "displayId": { "type": "string" },
                "status": { "type": "string" }
              }
            }
            """,
            fieldOrder: ["definitionId", "mode", "input", "timeout"],
            fields: fields);
    }

    private static ActionFieldUiHints FieldHints(
        string actionId,
        string fieldName,
        string? widget = null,
        bool sensitive = false) =>
        new(
            Widget: widget,
            LabelKey: $"{actionId}.ui.fields.{fieldName}.label",
            Sensitive: sensitive);
}
