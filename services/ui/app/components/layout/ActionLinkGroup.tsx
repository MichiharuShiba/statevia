"use client";

import Link from "next/link";
import { useUiText } from "../../lib/uiTextContext";

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
    "rounded-md border-2 border-[var(--brand-cta-border)] bg-[var(--brand-cta-bg)] px-3 py-1.5 text-sm font-medium text-[var(--brand-cta-fg)] hover:bg-[var(--brand-cta-bg-hover)]",
  secondary:
    "text-sm text-[var(--md-sys-color-primary)] underline underline-offset-2 hover:text-[var(--md-sys-color-primary)]"
};

function getLinkClass(priority: ActionLinkPriority): string {
  return ACTION_LINK_CLASS_MAP[priority];
}

/**
 * 一覧遷移・戻り・関連導線をまとめて描画するリンクグループ。
 */
export function ActionLinkGroup({ links, className }: Readonly<ActionLinkGroupProps>) {
  const uiText = useUiText();
  if (links.length === 0) return null;

  return (
    <nav
      aria-label={uiText.actionLinks.aria.navigation}
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
