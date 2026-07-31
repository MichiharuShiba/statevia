using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Statevia.Service.Api.Contracts;

/// <summary>トピック／相関キーで Wait へ配送する外部イベント入力。</summary>
/// <remarks><see cref="CorrelationKey"/> と <see cref="Topic"/> の少なくとも一方が必須（event のみは 422）。</remarks>
public sealed class EventIngressRequest : IValidatableObject
{
    /// <summary>配送するイベント名。</summary>
    [Required]
    [StringLength(256)]
    public required string Event { get; init; }

    /// <summary>相関キー。<see cref="Topic"/> と同時に未指定は不可。</summary>
    [StringLength(256)]
    public string? CorrelationKey { get; init; }

    /// <summary>トピック。<see cref="CorrelationKey"/> と同時に未指定は不可。</summary>
    [StringLength(256)]
    public string? Topic { get; init; }

    /// <summary>呼び出し元ペイロード。現段階では予約フィールドとして受け付ける。</summary>
    public JsonElement? Payload { get; init; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(CorrelationKey) && string.IsNullOrWhiteSpace(Topic))
        {
            yield return new ValidationResult(
                "At least one of correlationKey or topic is required for event ingress.",
                [nameof(CorrelationKey), nameof(Topic)]);
        }
    }
}
