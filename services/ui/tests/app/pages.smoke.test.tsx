import { describe, expect, it, vi } from "vitest";
import { waitFor } from "@testing-library/react";
import DashboardPage from "../../app/dashboard/page";
import DefinitionsPage from "../../app/definitions/page";
import WorkflowsPage from "../../app/workflows/page";
import { renderWithUiText } from "../testUtils";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn(), refresh: vi.fn() }),
  useSearchParams: () => new URLSearchParams("limit=20&offset=0"),
  redirect: vi.fn()
}));

vi.mock("../../app/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../app/lib/api")>();
  return {
    ...actual,
    apiGet: vi.fn(() => Promise.resolve({ items: [], totalCount: 0 })),
    buildDefinitionsListPath: vi.fn(() => "/definitions?limit=20&offset=0"),
    buildWorkflowsListPath: vi.fn(() => "/workflows?limit=20&offset=0")
  };
});

describe("page smoke", () => {
  it("dashboard page を描画する", async () => {
    const { container } = renderWithUiText(<DashboardPage />);
    await waitFor(() => expect(container.firstChild).toBeTruthy());
  });

  it("definitions page を描画する", async () => {
    const { container } = renderWithUiText(<DefinitionsPage />);
    await waitFor(() => expect(container.firstChild).toBeTruthy());
  });

  it("workflows page ラッパーを export する", () => {
    expect(WorkflowsPage).toBeTypeOf("function");
  });
});
