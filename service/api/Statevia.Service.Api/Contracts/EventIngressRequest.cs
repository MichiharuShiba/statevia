using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Statevia.Service.Api.Contracts;

/// <summary>トピック／キーで Wait 購読へ配送する外部イベント入力。</summary>
/// <remarks>
/// <para><see cref="Topic"/> 必須。<see cref="Key"/> 省略時は空文字として照合する。</para>
/// <para>配信者は受信側の遷移名を指定しない（event フィールドなし）。</para>
/// </remarks>
public sealed class EventIngressRequest
{
    /// <summary>配送先トピック。</summary>
    [Required]
    [StringLength(256, MinimumLength = 1)]
    public required string Topic { get; init; }

    /// <summary>相関キー。省略・空白は空文字に正規化して照合する。</summary>
    [StringLength(256)]
    public string? Key { get; init; }

    /// <summary>呼び出し元ペイロード。現段階では予約フィールドとして受け付ける。</summary>
    public JsonElement? Payload { get; init; }
}
