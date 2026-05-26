import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { ExecutionsPageClient } from "../../app/executions/ExecutionsPageClient";
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
    buildExecutionsListPath: vi.fn(() => "/executions?limit=20&offset=0")
  };
});

import { apiGet } from "../../app/lib/api";

describe("ExecutionsPageClient", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockResolvedValue({ items: [], totalCount: 0 });
  });

  it("ワークフロー一覧を読み込む", async () => {
    renderWithUiText(<ExecutionsPageClient />);

    await waitFor(() => {
      expect(apiGet).toHaveBeenCalled();
    });
    expect(apiGet).toHaveBeenCalledWith("/executions?limit=20&offset=0");
  });

  it("フィルタ送信で URL を更新する", async () => {
    renderWithUiText(<ExecutionsPageClient />);
    await waitFor(() => expect(apiGet).toHaveBeenCalled());

    const nameInput = screen.getByLabelText(/name（execution/);
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
