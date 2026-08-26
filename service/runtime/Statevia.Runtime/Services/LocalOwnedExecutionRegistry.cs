using System.Collections.Concurrent;

namespace Statevia.Runtime.Services;

/// <summary>
/// このワーカープロセスが Engine に載せている execution と process CTS の対応。
/// </summary>
/// <remarks>
/// <para>Cancel 制御ループが同一プロセス所有へ IRQ するために使う。DB の <c>owner_worker_id</c> の代替ではない。</para>
/// <para>登録と CTS キャンセルはプロセス内メモリのみ。fencing 失敗時は現行どおりローカル停止する。</para>
/// </remarks>
internal sealed class LocalOwnedExecutionRegistry
{
    private readonly ConcurrentDictionary<Guid, Slot> _slots = new();

    /// <summary>所有開始後に process CTS を登録する。</summary>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="processCts">ライフサイクルスロットの処理 CTS。</param>
    /// <returns>新規登録できたとき <see langword="true"/>。</returns>
    public bool TryRegister(Guid executionId, CancellationTokenSource processCts) =>
        _slots.TryAdd(executionId, new Slot(processCts));

    /// <summary>スロット終了時に登録を外す（冪等）。</summary>
    /// <param name="executionId">実行 ID。</param>
    public void Unregister(Guid executionId) => _slots.TryRemove(executionId, out _);

    /// <summary>
    /// 登録済みならローカル Cancel を記録して process CTS をキャンセルする。
    /// </summary>
    /// <param name="executionId">実行 ID。</param>
    /// <returns>このプロセスが所有中で IRQ したとき <see langword="true"/>。</returns>
    public bool TryCancelLocal(Guid executionId)
    {
        if (!_slots.TryGetValue(executionId, out var slot))
            return false;

        Interlocked.Exchange(ref slot.LocalCancelRequested, 1);
        slot.ProcessCts.Cancel();
        return true;
    }

    /// <summary>IRQ フラグを消費する。heartbeat 喪失と区別するため。</summary>
    /// <param name="executionId">実行 ID。</param>
    /// <returns>ローカル Cancel が要求されていたとき <see langword="true"/>。</returns>
    public bool TryConsumeLocalCancel(Guid executionId) =>
        _slots.TryGetValue(executionId, out var slot)
        && Interlocked.CompareExchange(ref slot.LocalCancelRequested, 0, 1) == 1;

    private sealed class Slot(CancellationTokenSource processCts)
    {
        public CancellationTokenSource ProcessCts { get; } = processCts;

        public int LocalCancelRequested;
    }
}
