namespace Statevia.Core.Api.Persistence;

/// <summary>表示用 ID とリソース UUID の対応（U3: display_ids テーブル）。</summary>
public class DisplayIdRow
{
    public required string Kind { get; set; }
    public required string DisplayId { get; set; }
    public required Guid ResourceId { get; set; }
    public DateTime CreatedAt { get; set; }
}
