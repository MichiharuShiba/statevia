import { Suspense } from "react";
import { LoginPageClient } from "@/features/auth/ui/LoginPageClient";

/**
 * ログイン画面（薄い route wrapper）。
 */
export default function LoginPage() {
  return (
    <Suspense fallback={null}>
      <LoginPageClient />
    </Suspense>
  );
}
