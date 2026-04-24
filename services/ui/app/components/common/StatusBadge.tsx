import { getStatusStyle, type StatusLike } from "../../lib/statusStyle";

type StatusBadgeProps = {
  status: StatusLike;
  className?: string;
};

/**
 * Workflow / Node の状態ラベルを共通表示するバッジ。
 */
export function StatusBadge({ status, className }: Readonly<StatusBadgeProps>) {
  const style = getStatusStyle(status);
  const badgeClassName = [
    "inline-flex items-center gap-1 rounded px-2 py-0.5 text-xs font-medium",
    style.badgeClass,
    className
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <span className={badgeClassName}>
      <span aria-hidden>{style.icon}</span>
      <span>{status}</span>
    </span>
  );
}
