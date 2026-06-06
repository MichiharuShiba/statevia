import { redirect } from "next/navigation";

/**
 * 管理者ルートの既定リダイレクト。
 */
export default function AdminIndexPage() {
  redirect("/admin/users");
}
