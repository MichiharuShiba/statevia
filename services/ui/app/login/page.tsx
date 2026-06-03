import { Suspense } from "react";
import { LoginPageClient } from "./LoginPageClient";

/**
 * ログイン画面（E3 task 9 · 画面 1）。
 */
export default function LoginPage() {
  return (
    <Suspense fallback={null}>
      <LoginPageClient />
    </Suspense>
  );
}
