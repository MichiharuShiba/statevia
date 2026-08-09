using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Application.Contracts.Services;

/// <summary>
/// Engine の <see cref="ForkExpansionEvent"/> を受け、物理子展開と親 Unload を行う Hosted 側ハンドラ。
/// </summary>
public interface IForkExpansionHostHandler
{
    /// <summary>
    /// Fork 展開イベントを処理する。呼び出し元はテナント文脈を設定済みであること。
    /// </summary>
    /// <param name="evt">Engine からの展開通知。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task HandleAsync(ForkExpansionEvent evt, CancellationToken ct);
}
