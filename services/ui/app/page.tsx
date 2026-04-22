import { redirect } from "next/navigation";

/**
 * ルート `/` は TOP ダッシュボードへ集約する。
 * 実行 UI 本体は `/dashboard` 以降のルートで提供する。
 */
export default function RootPage() {
  redirect("/dashboard");
}
