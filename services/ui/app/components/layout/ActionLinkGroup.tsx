import Link from "next/link";

type ActionLinkPriority = "primary" | "secondary";

export type ActionLinkItem = {
  label: string;
  href: string;
  priority?: ActionLinkPriority;
};

type ActionLinkGroupProps = {
  links: ActionLinkItem[];
  className?: string;
};

const ACTION_LINK_CLASS_MAP: Record<ActionLinkPriority, string> = {
  primary:
    "rounded-md border border-[var(--tone-accent)] bg-[var(--tone-accent-soft)] px-3 py-1.5 text-sm font-medium text-[var(--tone-accent-strong)] hover:bg-[var(--tone-accent-soft-hover)]",
  secondary:
    "text-sm text-[var(--tone-accent-strong)] underline underline-offset-2 hover:text-[var(--tone-accent-strong-hover)]"
};

function getLinkClass(priority: ActionLinkPriority): string {
  return ACTION_LINK_CLASS_MAP[priority];
}

/**
 * 一覧遷移・戻り・関連導線をまとめて描画するリンクグループ。
 */
export function ActionLinkGroup({ links, className }: Readonly<ActionLinkGroupProps>) {
  if (links.length === 0) return null;

  return (
    <nav
      aria-label="画面導線"
      className={className ? `flex flex-wrap items-center gap-3 ${className}` : "flex flex-wrap items-center gap-3"}
    >
      {links.map((link) => {
        const priority = link.priority ?? "secondary";
        return (
          <Link key={`${link.href}:${link.label}`} href={link.href} className={getLinkClass(priority)}>
            {link.label}
          </Link>
        );
      })}
    </nav>
  );
}
