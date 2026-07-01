using System.Text.Json;
using Statevia.Core.Actions.Abstractions.Publication;
using Statevia.Service.Api.Application.Actions.Publication;

namespace Statevia.Service.Api.Tests.Application.Actions.Publication;

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

    /// <summary>Publication Descriptor.ActionId が Catalog と不一致のとき ArgumentException。</summary>
    [Fact]
    public void Validate_DescriptorActionIdMismatch_Throws()
    {
        // Arrange
        var publication = CreatePublication(
            new ActionUiMetadata(
                Fields: new Dictionary<string, ActionFieldUiHints>
                {
                    ["url"] = new ActionFieldUiHints(LabelKey: $"{ActionId}.ui.fields.url.label"),
                }));

        // Act / Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            ActionUiMetadataValidator.Validate("statevia.action.builtin.other", publication));
        Assert.Contains("does not match", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>enum labelKey は canonical actionId 根の .ui. 直後パスを要求する。</summary>
    [Fact]
    public void Validate_ValidEnumLabelKey_Passes()
    {
        // Arrange
        var publication = CreatePublication(
            new ActionUiMetadata(
                EnumLabelKeys: new Dictionary<string, string>
                {
                    ["GET"] = $"{ActionId}.ui.enums.method.GET.label",
                }));

        // Act / Assert
        ActionUiMetadataValidator.Validate(ActionId, publication);
    }

    /// <summary>enum labelKey が actionId 直後 .ui. でない場合は ArgumentException。</summary>
    [Fact]
    public void Validate_InvalidEnumLabelKeyUiRoot_Throws()
    {
        // Arrange
        var publication = CreatePublication(
            new ActionUiMetadata(
                EnumLabelKeys: new Dictionary<string, string>
                {
                    ["GET"] = $"{ActionId}.foo.ui.enums.method.GET.label",
                }));

        // Act / Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            ActionUiMetadataValidator.Validate(ActionId, publication));
        Assert.Contains("must start with", ex.Message, StringComparison.Ordinal);
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
