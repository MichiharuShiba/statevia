import { describe, expect, it, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import { DashboardPageClient } from "@/features/dashboard/ui/DashboardPageClient";
import { renderWithUiText } from "../testUtils";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() })
}));

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return { ...actual, apiGet: vi.fn() };
});

import { apiGet } from "@/shared/api";

describe("DashboardPageClient", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockResolvedValue({ items: [], totalCount: 0 });
  });

  it("空一覧を表示する", async () => {
    renderWithUiText(<DashboardPageClient />);

    await waitFor(() => {
      expect(screen.getByText(/0/)).toBeInTheDocument();
    });
    expect(apiGet).toHaveBeenCalledWith("/executions?limit=10&offset=0");
  });
});
