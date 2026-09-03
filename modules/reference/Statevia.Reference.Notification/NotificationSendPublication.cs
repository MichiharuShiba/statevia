using Statevia.Core.Actions.Abstractions.Publication;
using System.Text.Json;

namespace Statevia.Reference.Notification;

/// <summary>notification.send の ActionPublication。</summary>
internal static class NotificationSendPublication
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
            ["channel"] = FieldHints(actionId, "channel", widget: "select"),
            ["to"] = FieldHints(actionId, "to", widget: "text"),
            ["subject"] = FieldHints(actionId, "subject", widget: "text"),
            ["body"] = FieldHints(actionId, "body", widget: "text"),
            ["from"] = FieldHints(actionId, "from", widget: "text"),
        };

        return new ActionPublication(
            new ActionDescriptor(
                actionId,
                "1.0.0",
                "Notification",
                Category: "Notification"),
            new ActionSchemaBundle(
                JsonDocument.Parse(
                    $$"""
                    {
                      "$schema": "https://json-schema.org/draft/2020-12/schema",
                      "$id": "{{SchemaBaseUri}}/{{actionId}}/input",
                      "type": "object",
                      "additionalProperties": false,
                      "required": ["channel", "to", "subject", "body"],
                      "properties": {
                        "channel": {
                          "title": "Channel",
                          "type": "string",
                          "enum": ["email"],
                          "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                        },
                        "to": {
                          "title": "To",
                          "type": "string",
                          "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                        },
                        "subject": {
                          "title": "Subject",
                          "type": "string",
                          "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                        },
                        "body": {
                          "title": "Body",
                          "type": "string",
                          "{{ValueKindKeyword}}": "{{ValueKindLiteralOrPath}}"
                        },
                        "from": {
                          "title": "From",
                          "type": "string",
                          "format": "email",
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
                      "required": ["channel"],
                      "properties": {
                        "channel": { "type": "string" },
                        "messageId": { "type": ["string", "null"] }
                      }
                    }
                    """)),
            new ActionUiMetadata(
                FieldOrder: ["channel", "to", "subject", "body", "from"],
                Fields: fields));
    }

    private static ActionFieldUiHints FieldHints(
        string actionId,
        string fieldName,
        string? widget = null) =>
        new(
            Widget: widget,
            LabelKey: $"{actionId}.ui.fields.{fieldName}.label");
}
