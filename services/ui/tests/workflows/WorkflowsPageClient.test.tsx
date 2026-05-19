import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { WorkflowsPageClient } from "../../app/workflows/WorkflowsPageClient";
import { renderWithUiText } from "../testUtils";

const replace = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace }),
  useSearchParams: () => new URLSearchParams("limit=20&offset=0")
}));

vi.mock("../../app/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../app/lib/api")>();
  return {
    ...actual,
    apiGet: vi.fn(),
    buildWorkflowsListPath: vi.fn(() => "/workflows?limit=20&offset=0")
  };
});

import { apiGet } from "../../app/lib/api";

describe("WorkflowsPageClient", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockResolvedValue({ items: [], totalCount: 0 });
  });

  it("ワークフロー一覧を読み込む", async () => {
    renderWithUiText(<WorkflowsPageClient />);

    await waitFor(() => {
      expect(apiGet).toHaveBeenCalled();
    });
    expect(apiGet).toHaveBeenCalledWith("/workflows?limit=20&offset=0");
  });

  it("フィルタ送信で URL を更新する", async () => {
    renderWithUiText(<WorkflowsPageClient />);
    await waitFor(() => expect(apiGet).toHaveBeenCalled());

    const nameInput = screen.getByLabelText(/name（workflow/);
    fireEvent.change(nameInput, { target: { value: "demo" } });
    const form = screen.getByRole("button", { name: "検索" }).closest("form");
    if (!(form instanceof HTMLFormElement)) {
      throw new TypeError("検索フォームが見つかりません");
    }
    fireEvent.submit(form);

    await waitFor(() => {
      expect(replace).toHaveBeenCalled();
    });
  });
});
