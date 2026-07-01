import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { AppHeader } from "../../../app/components/layout/AppHeader";
import { uiText } from "../../../app/lib/uiText";
import { UiTextProvider } from "../../../app/lib/uiTextContext";

const usePathname = vi.fn();

vi.mock("next/navigation", () => ({
  usePathname: () => usePathname()
}));

vi.mock("next/image", () => ({
  default: ({ alt }: { alt: string }) => <img alt={alt} />
}));

vi.mock("../../../app/components/layout/AdminNavLinks", () => ({
  AdminNavLinks: () => <span data-testid="admin-nav">admin</span>
}));

vi.mock("../../../app/components/layout/ThemeToggle", () => ({
  ThemeToggle: () => <span data-testid="theme-toggle">theme</span>
}));

vi.mock("../../../app/components/layout/LanguageToggle", () => ({
  LanguageToggle: () => <span data-testid="language-toggle">lang</span>
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
  });

  it("通常画面ではアプリ内ナビを表示する", () => {
    renderHeader("/dashboard");

    expect(screen.getByRole("link", { name: uiText.navigation.dashboard })).toHaveAttribute(
      "href",
      "/dashboard"
    );
    expect(screen.getByRole("link", { name: uiText.navigation.definitions })).toBeInTheDocument();
    expect(screen.getByTestId("admin-nav")).toBeInTheDocument();
  });

  it("ログイン画面ではアプリ内ナビを表示しない", () => {
    renderHeader("/login");

    expect(screen.queryByRole("link", { name: uiText.navigation.dashboard })).toBeNull();
    expect(screen.queryByRole("link", { name: uiText.navigation.definitions })).toBeNull();
    expect(screen.queryByRole("link", { name: uiText.navigation.executions })).toBeNull();
    expect(screen.queryByTestId("admin-nav")).toBeNull();
    expect(document.querySelector('header a[href="/login"]')).not.toBeNull();
  });
});
