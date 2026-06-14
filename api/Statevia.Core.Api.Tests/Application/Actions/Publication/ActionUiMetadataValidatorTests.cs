using System.Text.Json;
using Statevia.Actions.Abstractions.Publication;
using Statevia.Core.Api.Application.Actions.Publication;

namespace Statevia.Core.Api.Tests.Application.Actions.Publication;

/// <summary><see cref="ActionUiMetadataValidator"/> の labelKey プレフィックス検証。</summary>
public sealed class ActionUiMetadataValidatorTests
{
    private const string ActionId = "statevia.action.builtin.rest";

    /// <summary>canonical actionId 根の labelKey は検証を通過する。</summary>
    [Fact]
    public void Validate_ValidLabelKeyPrefix_Passes()
    {
        // Arrange
        var publication = CreatePublication(
            new ActionUiMetadata(
                Fields: new Dictionary<string, ActionFieldUiHints>
                {
                    ["url"] = new ActionFieldUiHints(LabelKey: $"{ActionId}.ui.fields.url.label"),
                    ["method"] = new ActionFieldUiHints(
                        LabelKey: $"{ActionId}.ui.fields.method.label",
                        DescriptionKey: $"{ActionId}.ui.fields.method.description"),
                }));

        // Act / Assert
        ActionUiMetadataValidator.Validate(ActionId, publication);
    }

    /// <summary>labelKey が actionId 根で始まらない場合は ArgumentException。</summary>
    [Fact]
    public void Validate_InvalidLabelKeyPrefix_Throws()
    {
        // Arrange
        var publication = CreatePublication(
            new ActionUiMetadata(
                Fields: new Dictionary<string, ActionFieldUiHints>
                {
                    ["url"] = new ActionFieldUiHints(LabelKey: "wrong.prefix.ui.fields.url.label"),
                }));

        // Act / Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            ActionUiMetadataValidator.Validate(ActionId, publication));
        Assert.Contains("must start with", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>actionId 根と fieldName の間に余分なセグメントがある labelKey は ArgumentException。</summary>
    [Fact]
    public void Validate_ExtraSegmentBetweenActionIdAndUiFields_Throws()
    {
        // Arrange
        var publication = CreatePublication(
            new ActionUiMetadata(
                Fields: new Dictionary<string, ActionFieldUiHints>
                {
                    ["url"] = new ActionFieldUiHints(
                        LabelKey: $"{ActionId}.foo.ui.fields.url.label"),
                }));

        // Act / Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            ActionUiMetadataValidator.Validate(ActionId, publication));
        Assert.Contains("must exactly match", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>UiMetadata が null の Publication は検証を通過する。</summary>
    [Fact]
    public void Validate_NullUiMetadata_Passes()
    {
        // Arrange
        var publication = CreatePublication(uiMetadata: null);

        // Act / Assert
        ActionUiMetadataValidator.Validate(ActionId, publication);
    }

    private static ActionPublication CreatePublication(ActionUiMetadata? uiMetadata)
    {
        using var inputSchema = JsonDocument.Parse("""{"type":"object"}""");
        using var outputSchema = JsonDocument.Parse("""{"type":"object"}""");
        return new ActionPublication(
            new ActionDescriptor(ActionId, "1.0.0", "REST"),
            new ActionSchemaBundle(
                JsonDocument.Parse(inputSchema.RootElement.GetRawText()),
                JsonDocument.Parse(outputSchema.RootElement.GetRawText())),
            uiMetadata);
    }
}
