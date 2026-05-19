import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { DefinitionEditorPageClient } from "../../app/definitions/DefinitionEditorPageClient";
import { defaultDefinitionYaml } from "../../app/lib/defaultDefinitionYaml";
import { renderWithUiText } from "../testUtils";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() })
}));

vi.mock("../../app/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../app/lib/api")>();
  return { ...actual, apiGet: vi.fn(), apiPost: vi.fn(), apiPut: vi.fn() };
});

import { apiGet, apiPost, apiPut } from "../../app/lib/api";

describe("DefinitionEditorPageClient", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockImplementation(async (path: string) => {
      if (path === "/definitions/schema/nodes") {
        return {
          schemaVersion: "1",
          nodesVersion: 1,
          schema: { properties: { version: {}, workflow: { properties: { name: {} } }, nodes: { items: { properties: { id: {} } } } } }
        };
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

  it("新規作成モードで YAML エディタを表示する", async () => {
    renderWithUiText(<DefinitionEditorPageClient />);

    await waitFor(() => {
      expect(apiGet).toHaveBeenCalledWith("/definitions/schema/nodes");
    });
    expect(screen.getByRole("button", { name: "保存" })).toBeInTheDocument();
  });

  it("編集モードで既存定義を読み込む", async () => {
    renderWithUiText(<DefinitionEditorPageClient definitionId="def-1" />);

    await waitFor(() => {
      expect(screen.getByDisplayValue("Edited")).toBeInTheDocument();
    });
    expect(apiGet).toHaveBeenCalledWith("/definitions/def-1");
  });

  it("グラフモードに切り替えられる", async () => {
    renderWithUiText(<DefinitionEditorPageClient />);
    await waitFor(() => expect(apiGet).toHaveBeenCalled());

    fireEvent.click(screen.getByRole("button", { name: "Graph" }));

    await waitFor(() => {
      expect(screen.getByText("グラフ編集")).toBeInTheDocument();
    });
  });

  it("編集保存で PUT を呼ぶ", async () => {
    vi.mocked(apiPut).mockResolvedValue({
      displayId: "def-1",
      resourceId: "res-1",
      name: "Edited2",
      createdAt: "2026-01-01T00:00:00Z",
      updatedAt: "2026-01-02T00:00:00Z"
    });

    renderWithUiText(<DefinitionEditorPageClient definitionId="def-1" />);
    await waitFor(() => expect(screen.getByDisplayValue("Edited")).toBeInTheDocument());

    fireEvent.change(screen.getByLabelText(/定義名/i), { target: { value: "Edited2" } });
    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    await waitFor(() => {
      expect(apiPut).toHaveBeenCalledWith("/definitions/def-1", expect.objectContaining({ name: "Edited2" }));
    });
  });

  it("定義名が空のとき保存しない", async () => {
    renderWithUiText(<DefinitionEditorPageClient />);
    await waitFor(() => expect(apiGet).toHaveBeenCalled());

    fireEvent.change(screen.getByLabelText(/定義名/i), { target: { value: "" } });
    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    expect(apiPost).not.toHaveBeenCalled();
    expect(screen.getByText(/定義名を入力/)).toBeInTheDocument();
  });

  it("新規保存で POST を呼ぶ", async () => {
    vi.mocked(apiPost).mockResolvedValue({
      displayId: "def-new",
      resourceId: "res-new",
      name: "DemoFlow",
      createdAt: "2026-01-01T00:00:00Z",
      updatedAt: "2026-01-01T00:00:00Z"
    });

    renderWithUiText(<DefinitionEditorPageClient />);
    await waitFor(() => expect(apiGet).toHaveBeenCalled());

    fireEvent.change(screen.getByLabelText(/定義名/i), { target: { value: "DemoFlow" } });
    fireEvent.click(screen.getByRole("button", { name: "保存" }));

    await waitFor(() => {
      expect(apiPost).toHaveBeenCalledWith("/definitions", expect.objectContaining({ name: "DemoFlow" }));
    });
  });
});
