import { describe, expect, it, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import { DefinitionEditorPageClient } from "../../app/definitions/DefinitionEditorPageClient";
import { renderWithUiText } from "../testUtils";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() })
}));

vi.mock("../../app/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../app/lib/api")>();
  return { ...actual, apiGet: vi.fn(), apiPost: vi.fn(), apiPut: vi.fn() };
});

import { apiGet } from "../../app/lib/api";

describe("DefinitionEditorPageClient", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockImplementation(async (path: string) => {
      if (path === "/definitions/schema/nodes") {
        return { properties: { version: {}, workflow: { properties: { name: {} } }, nodes: { items: { properties: { id: {} } } } } };
      }
      throw new Error(`unexpected path: ${path}`);
    });
  });

  it("新規作成モードで YAML エディタを表示する", async () => {
    renderWithUiText(<DefinitionEditorPageClient />);

    await waitFor(() => {
      expect(apiGet).toHaveBeenCalledWith("/definitions/schema/nodes");
    });
    expect(screen.getByRole("button", { name: "保存" })).toBeInTheDocument();
  });

});
