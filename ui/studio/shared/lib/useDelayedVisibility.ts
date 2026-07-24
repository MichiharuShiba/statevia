import { useEffect, useState } from "react";

/** 読み込み表示を出すまでの既定遅延（ミリ秒）。 */
export const DEFAULT_LOADING_INDICATOR_DELAY_MS = 300;

/**
 * `active` が true になってから {@link delayMs} 経過後にだけ true を返す。
 * 短い読み込みでは表示せず、即 `false` に戻したときは遅延なく非表示にする。
 * @param active 読み込み中などの活性フラグ
 * @param delayMs 表示までの待ち時間（ミリ秒）
 */
export function useDelayedVisibility(active: boolean, delayMs = DEFAULT_LOADING_INDICATOR_DELAY_MS): boolean {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    if (!active) {
      setVisible(false);
      return;
    }

    if (delayMs <= 0) {
      setVisible(true);
      return;
    }

    const timerId = globalThis.setTimeout(() => {
      setVisible(true);
    }, delayMs);

    return () => {
      globalThis.clearTimeout(timerId);
    };
  }, [active, delayMs]);

  return visible;
}
