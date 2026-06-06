"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { fetchAuthMe } from "../../lib/fetchAuthMe";
import { useUiText } from "../../lib/uiTextContext";

/**
 * テナント管理者向けナビリンク（非管理者には何も表示しない）。
 */
export function AdminNavLinks() {
  const uiText = useUiText();
  const [isTenantAdmin, setIsTenantAdmin] = useState(false);

  useEffect(() => {
    let cancelled = false;
    void fetchAuthMe().then((me) => {
      if (!cancelled) setIsTenantAdmin(me?.isTenantAdmin === true);
    });
    return () => {
      cancelled = true;
    };
  }, []);

  if (!isTenantAdmin) return null;

  return (
    <>
      <Link href="/admin/users" className="hover:text-[var(--brand-header-fg)] hover:underline">
        {uiText.navigation.adminUsers}
      </Link>
      <Link href="/admin/groups" className="hover:text-[var(--brand-header-fg)] hover:underline">
        {uiText.navigation.adminGroups}
      </Link>
    </>
  );
}
