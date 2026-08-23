using System.Text.Json;
using Statevia.Core.Actions.Abstractions.Publication;

namespace Statevia.Reference.Http;

/// <summary>http.request の ActionPublication。</summary>
internal static class HttpRequestPublication
{
    private const string SchemaBaseUri = StateviaActionSchemaVocabulary.ActionSchemaIdBaseUri;
    private const string ValueKindKeyword = StateviaActionSchemaVocabulary.ValueKindKeyword;
    private const string ValueKindLiteralOrPath = StateviaActionSchemaVocabulary.ValueKindLiteralOrPath;

    /// <summary>canonical actionId に対応する Publication を返す。</summary>
    /// <param name="actionId">canonical actionId。</param>
    public static ActionPublication Create(string actionId)
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

        return new ActionPublication(
            new ActionDescriptor(
                actionId,
                "1.0.0",
                "HTTP Request",
                Category: "Http"),
            new ActionSchemaBundle(
                JsonDocument.Parse(
                    $$"""
                    {
                      "$schema": "https://json-schema.org/draft/2020-12/schema",
                      "$id": "{{SchemaBaseUri}}/{{actionId}}/input",
                      "type": "object",
                      "additionalProperties": false,
                      "required": ["url", "method"],
                      "properties": {
                        "url": {
                          "title": "URL",
                          "type": "string",
                          "format": "uri",
                          "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                        },
                        "method": {
                          "title": "HTTP method",
                          "type": "string",
                          "enum": ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"],
                          "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                        },
                        "headers": {
                          "title": "Headers",
                          "type": "object",
                          "additionalProperties": { "type": "string" },
                          "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                        },
                        "body": {
                          "title": "Body",
                          "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                        },
                        "timeout": {
                          "title": "Timeout (seconds)",
                          "type": "integer",
                          "minimum": 1,
                          "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                        },
                        "idempotencyKey": {
                          "title": "Idempotency-Key",
                          "type": "string",
                          "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                        }
                      }
                    }
                    """),
                JsonDocument.Parse(
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
                    """)),
            new ActionUiMetadata(
                FieldOrder: ["url", "method", "headers", "body", "timeout", "idempotencyKey"],
                Fields: fields));
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
