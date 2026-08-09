import { getStatusStyle } from "@/shared/lib/statusStyle";

type StatusBadgeProps = {
  /**
   * 表示する状態文字列。
   * 既知の StatusLike に加え、API 由来の想定外値も受け付ける（getStatusStyle がフォールバックする）。
   */
  status: string;
  className?: string;
};

/**
 * Execution / Node の状態ラベルを共通表示するバッジ。
 */
export function StatusBadge({ status, className }: Readonly<StatusBadgeProps>) {
  const { badgeClass } = getStatusStyle(status);
  const badgeClassName = [
    "inline-flex w-28 items-center justify-center rounded px-2 py-0.5 text-xs font-medium",
    badgeClass,
    className
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <span className={badgeClassName}>{status}</span>
  );
}
