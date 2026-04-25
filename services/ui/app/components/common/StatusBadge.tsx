import { getStatusStyle, type StatusLike } from "../../lib/statusStyle";

type StatusBadgeProps = {
  status: StatusLike;
  className?: string;
};

/**
 * Workflow / Node の状態ラベルを共通表示するバッジ。
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
