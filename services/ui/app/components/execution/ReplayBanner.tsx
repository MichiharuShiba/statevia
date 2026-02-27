"use client";

type ReplayBannerProps = {
  onBackToCurrent: () => void;
};

export function ReplayBanner({ onBackToCurrent }: Readonly<ReplayBannerProps>) {
  return (
    <div className="rounded-xl border border-sky-200 bg-sky-50 px-3 py-2 text-xs text-sky-900 flex items-center justify-between gap-2 flex-wrap">
      <span>過去の時点を表示中です。「現在に戻る」で最新の状態に戻せます。</span>
      <button
        type="button"
        onClick={onBackToCurrent}
        className="shrink-0 rounded-lg border border-sky-300 bg-white px-2 py-1 font-medium text-sky-800 hover:bg-sky-100"
      >
        現在に戻る
      </button>
    </div>
  );
}
