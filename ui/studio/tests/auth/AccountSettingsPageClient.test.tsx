import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { AccountSettingsPageClient } from "@/features/auth/ui/AccountSettingsPageClient";
import { renderWithUiText } from "../testUtils";
import { uiText } from "@/shared/i18n/uiText";

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return {
    ...actual,
    apiPut: vi.fn()
  };
});

import { apiPut } from "@/shared/api";

describe("AccountSettingsPageClient", () => {
  beforeEach(() => {
    vi.mocked(apiPut).mockClear();
    vi.mocked(apiPut).mockResolvedValue(null);
  });

  it("アカウント設定フォームを表示する", () => {
    renderWithUiText(<AccountSettingsPageClient />);

    expect(screen.getByRole("heading", { name: uiText.auth.account.title })).toBeInTheDocument();
    expect(screen.getByLabelText(uiText.auth.account.currentPasswordLabel)).toHaveAttribute(
      "type",
      "password"
    );
    expect(screen.getByLabelText(uiText.auth.account.newPasswordLabel)).toHaveAttribute("minLength", "8");
    expect(screen.getByLabelText(uiText.auth.account.newPasswordLabel)).toHaveAttribute("type", "password");
    expect(screen.getByLabelText(uiText.auth.account.confirmPasswordLabel)).toHaveAttribute(
      "type",
      "password"
    );
  });

  it("一致時に現行・新しいパスワードだけを PUT する", async () => {
    renderWithUiText(<AccountSettingsPageClient />);

    fireEvent.change(screen.getByLabelText(uiText.auth.account.currentPasswordLabel), {
      target: { value: "old-secret" }
    });
    fireEvent.change(screen.getByLabelText(uiText.auth.account.newPasswordLabel), {
      target: { value: "newsecret1" }
    });
    fireEvent.change(screen.getByLabelText(uiText.auth.account.confirmPasswordLabel), {
      target: { value: "newsecret1" }
    });
    fireEvent.click(screen.getByRole("button", { name: uiText.auth.account.submit }));

    await waitFor(() => {
      expect(apiPut).toHaveBeenCalledWith("/auth/me/password", {
        currentPassword: "old-secret",
        newPassword: "newsecret1"
      });
    });
    expect(await screen.findByText(uiText.auth.account.success)).toBeInTheDocument();
  });

  it("確認パスワード不一致では PUT しない", async () => {
    renderWithUiText(<AccountSettingsPageClient />);

    fireEvent.change(screen.getByLabelText(uiText.auth.account.currentPasswordLabel), {
      target: { value: "old-secret" }
    });
    fireEvent.change(screen.getByLabelText(uiText.auth.account.newPasswordLabel), {
      target: { value: "newsecret1" }
    });
    fireEvent.change(screen.getByLabelText(uiText.auth.account.confirmPasswordLabel), {
      target: { value: "othersecret1" }
    });
    fireEvent.click(screen.getByRole("button", { name: uiText.auth.account.submit }));

    expect(await screen.findByRole("alert")).toHaveTextContent(uiText.auth.account.passwordMismatch);
    expect(apiPut).not.toHaveBeenCalled();
  });
});
