import { describe, expect, it, vi, beforeEach } from "vitest";
import { waitFor } from "@testing-library/react";
import { DefinitionsPageClient } from "../../app/definitions/DefinitionsPageClient";
import { renderWithUiText } from "../testUtils";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  useSearchParams: () => new URLSearchParams("limit=20&offset=0")
}));

vi.mock("../../app/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../app/lib/api")>();
  return {
    ...actual,
    apiGet: vi.fn(),
    buildDefinitionsListPath: vi.fn(() => "/definitions?limit=20&offset=0")
  };
});

import { apiGet } from "../../app/lib/api";

describe("DefinitionsPageClient", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockResolvedValue({ items: [], totalCount: 0 });
  });

  it("定義一覧を読み込んで空状態を表示する", async () => {
    renderWithUiText(<DefinitionsPageClient />);

    await waitFor(() => {
      expect(apiGet).toHaveBeenCalled();
    });
    expect(apiGet).toHaveBeenCalledWith("/definitions?limit=20&offset=0");
  });
});
