import { describe, expect, it, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import DefinitionEditPage from "../../app/definitions/[definitionId]/edit/page";
import { defaultDefinitionYaml } from "../../app/lib/defaultDefinitionYaml";
import { renderWithUiText } from "../testUtils";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() })
}));

vi.mock("../../app/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../app/lib/api")>();
  return { ...actual, apiGet: vi.fn() };
});

import { apiGet } from "../../app/lib/api";

describe("DefinitionEditPage", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockImplementation(async (path: string) => {
      if (path === "/definitions/schema/nodes") {
        return { schemaVersion: "1", nodesVersion: 1, schema: { properties: {} } };
      }
      if (path === "/definitions/def-1") {
        return {
          displayId: "def-1",
          resourceId: "res-1",
          name: "Edited",
          createdAt: "2026-01-01T00:00:00Z",
          updatedAt: "2026-01-01T00:00:00Z",
          yaml: defaultDefinitionYaml
        };
      }
      throw new Error(`unexpected path: ${path}`);
    });
  });

  it("編集ページで定義名を表示する", async () => {
    const page = await DefinitionEditPage({ params: Promise.resolve({ definitionId: "def-1" }) });
    renderWithUiText(page);

    await waitFor(() => {
      expect(screen.getByDisplayValue("Edited")).toBeInTheDocument();
    });
  });
});
