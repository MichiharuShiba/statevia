import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { LoginPageClient } from "../../app/login/LoginPageClient";
import { renderWithUiText } from "../testUtils";

const replace = vi.fn();
const refresh = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace, refresh, push: vi.fn() }),
  useSearchParams: () => new URLSearchParams("from=%2Fdefinitions")
}));

describe("LoginPageClient", () => {
  beforeEach(() => {
    replace.mockClear();
    refresh.mockClear();
    vi.stubGlobal(
      "fetch",
      vi.fn(() =>
        Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve({ ok: true })
        } as Response)
      )
    );
  });

  it("ログインフォームを表示する", () => {
    renderWithUiText(<LoginPageClient />);
    expect(screen.getByRole("heading", { name: "ログイン" })).toBeInTheDocument();
    expect(screen.getByLabelText("テナントキー")).toBeInTheDocument();
    expect(screen.getByLabelText("メールアドレス")).toBeInTheDocument();
    expect(screen.getByLabelText("パスワード")).toBeInTheDocument();
  });

  it("パスワード表示切替で input type が text と password を切り替える", () => {
    renderWithUiText(<LoginPageClient />);
    const passwordInput = screen.getByLabelText("パスワード");
    expect(passwordInput).toHaveAttribute("type", "password");

    fireEvent.click(screen.getByRole("button", { name: "パスワードを表示" }));
    expect(passwordInput).toHaveAttribute("type", "text");

    fireEvent.click(screen.getByRole("button", { name: "パスワードを非表示" }));
    expect(passwordInput).toHaveAttribute("type", "password");
  });

  it("成功時に from クエリ先へ遷移する", async () => {
    renderWithUiText(<LoginPageClient />);
    fireEvent.change(screen.getByLabelText("メールアドレス"), { target: { value: "user@example.com" } });
    fireEvent.change(screen.getByLabelText("パスワード"), { target: { value: "secret" } });
    fireEvent.click(screen.getByRole("button", { name: "ログイン" }));

    await waitFor(() => {
      expect(replace).toHaveBeenCalledWith("/definitions");
      expect(refresh).toHaveBeenCalled();
    });
  });
});
