import { redirect } from "next/navigation";
import { fetchAuthMeServer } from "../lib/serverAuthMe";

/**
 * 管理者画面レイアウト。テナント管理者以外はダッシュボードへ戻す。
 */
export default async function AdminLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  const me = await fetchAuthMeServer();
  if (!me) redirect("/login?from=/admin/users");
  if (!me.isTenantAdmin) redirect("/dashboard");
  return children;
}
