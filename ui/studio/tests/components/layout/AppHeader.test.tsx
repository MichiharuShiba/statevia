import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { AppHeader } from "@/shared/ui/AppHeader";
import { uiText } from "@/shared/i18n/uiText";
import { UiTextProvider } from "@/shared/i18n/uiTextContext";
import { clearSessionAndRedirectToLogin } from "@/shared/auth/authRedirect";

const usePathname = vi.fn();

vi.mock("next/navigation", () => ({
  usePathname: () => usePathname()
}));

vi.mock("next/image", () => ({
  default: ({ alt }: { alt: string }) => <img alt={alt} />
}));

vi.mock("@/shared/ui/AdminNavLinks", () => ({
  AdminNavLinks: () => <span data-testid="admin-nav">admin</span>
}));

vi.mock("@/shared/ui/ThemeToggle", () => ({
  ThemeToggle: () => <span data-testid="theme-toggle">theme</span>
}));

vi.mock("@/shared/ui/LanguageToggle", () => ({
  LanguageToggle: () => <span data-testid="language-toggle">lang</span>
}));

vi.mock("@/shared/auth/authRedirect", () => ({
  clearSessionAndRedirectToLogin: vi.fn()
}));

function renderHeader(pathname: string) {
  usePathname.mockReturnValue(pathname);
  return render(
    <UiTextProvider locale="ja">
      <AppHeader theme="dark" locale="ja" />
    </UiTextProvider>
  );
}

describe("AppHeader", () => {
  beforeEach(() => {
    usePathname.mockReset();
    vi.mocked(clearSessionAndRedirectToLogin).mockClear();
  });

  it("通常画面ではアプリ内ナビを表示する", () => {
    renderHeader("/dashboard");

    expect(screen.getByRole("link", { name: uiText.navigation.dashboard })).toHaveAttribute(
      "href",
      "/dashboard"
    );
    expect(screen.getByRole("link", { name: uiText.navigation.definitions })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: uiText.navigation.account })).toHaveAttribute(
      "href",
      "/account"
    );
    expect(screen.getByRole("button", { name: uiText.navigation.logout })).toBeInTheDocument();
    expect(screen.getByTestId("admin-nav")).toBeInTheDocument();
  });

  it("ログイン画面ではアプリ内ナビを表示しない", () => {
    renderHeader("/login");

    expect(screen.queryByRole("link", { name: uiText.navigation.dashboard })).toBeNull();
    expect(screen.queryByRole("link", { name: uiText.navigation.definitions })).toBeNull();
    expect(screen.queryByRole("link", { name: uiText.navigation.executions })).toBeNull();
    expect(screen.queryByRole("link", { name: uiText.navigation.account })).toBeNull();
    expect(screen.queryByRole("button", { name: uiText.navigation.logout })).toBeNull();
    expect(screen.queryByTestId("admin-nav")).toBeNull();
    expect(document.querySelector('header a[href="/login"]')).not.toBeNull();
  });

  it("ログアウト押下でセッション破棄を from なしで呼ぶ", () => {
    renderHeader("/dashboard");

    fireEvent.click(screen.getByRole("button", { name: uiText.navigation.logout }));

    expect(clearSessionAndRedirectToLogin).toHaveBeenCalledWith({ includeFrom: false });
  });
});
