import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, screen } from "@testing-library/react";
import { useState } from "react";
import { DefinitionGraphEditor } from "../../../app/components/editor/DefinitionGraphEditor";
import { defaultDefinitionYaml } from "../../../app/lib/defaultDefinitionYaml";
import { parseDefinitionYaml } from "../../../app/lib/definition-editor/parseDefinitionYaml";
import type { DefinitionGraphDocument } from "../../../app/lib/definition-editor/types";
import { renderWithUiText } from "../../testUtils";
import { definitionGraphEditorTestLabels } from "./definitionGraphEditorLabels";

vi.mock("../../../app/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../app/lib/api")>();
  return { ...actual, apiGet: vi.fn() };
});

import { apiGet } from "../../../app/lib/api";

const parseOpts = {
  rootObjectRequired: () => "root",
  nodesArrayRequired: () => "nodes"
};

function StatefulGraphEditorHarness({
  initialDocument
}: Readonly<{ initialDocument: DefinitionGraphDocument }>) {
  const [document, setDocument] = useState(initialDocument);
  return (
    <DefinitionGraphEditor
      document={document}
      onDocumentChange={setDocument}
      validationMessages={[]}
      labels={definitionGraphEditorTestLabels}
    />
  );
}

describe("DefinitionGraphEditor", () => {
  beforeEach(() => {
    vi.mocked(apiGet).mockReset();
    vi.mocked(apiGet).mockImplementation(async (path: string) => {
      if (path === "/actions/schema/index") {
        return {
          items: [
            {
              actionId: "statevia.action.builtin.noop",
              displayName: "No-op",
              version: "1.0.0"
            },
            {
              actionId: "statevia.action.builtin.rest",
              displayName: "REST",
              version: "1.0.0"
            }
          ]
        };
      }
      return undefined;
    });
  });

  it("ドキュメントをグラフとして描画する", () => {
    const parsed = parseDefinitionYaml(defaultDefinitionYaml, parseOpts);
    expect(parsed.document).not.toBeNull();

    renderWithUiText(
      <DefinitionGraphEditor
        document={parsed.document}
        onDocumentChange={vi.fn()}
        validationMessages={[]}
        labels={definitionGraphEditorTestLabels}
      />
    );

    expect(screen.getByText(definitionGraphEditorTestLabels.title)).toBeInTheDocument();
  });

  it("document が null のとき空状態を表示する", () => {
    renderWithUiText(
      <DefinitionGraphEditor
        document={null}
        onDocumentChange={vi.fn()}
        validationMessages={[]}
        labels={definitionGraphEditorTestLabels}
      />
    );

    expect(screen.getByText(definitionGraphEditorTestLabels.empty)).toBeInTheDocument();
  });

  it("バリデーションメッセージとフルスクリーンを操作できる", () => {
    const parsed = parseDefinitionYaml(defaultDefinitionYaml, parseOpts);
    expect(parsed.document).not.toBeNull();

    renderWithUiText(
      <DefinitionGraphEditor
        document={parsed.document}
        onDocumentChange={vi.fn()}
        validationMessages={["node id required"]}
        labels={definitionGraphEditorTestLabels}
      />
    );

    expect(screen.getByText("node id required")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: definitionGraphEditorTestLabels.fullscreenEnter }));
    expect(screen.getByRole("button", { name: definitionGraphEditorTestLabels.fullscreenExit })).toBeInTheDocument();
  });

  it("wait ノード追加で onDocumentChange を呼ぶ", () => {
    const parsed = parseDefinitionYaml(defaultDefinitionYaml, parseOpts);
    expect(parsed.document).not.toBeNull();
    const onDocumentChange = vi.fn();

    renderWithUiText(
      <DefinitionGraphEditor
        document={parsed.document}
        onDocumentChange={onDocumentChange}
        validationMessages={[]}
        labels={definitionGraphEditorTestLabels}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "wait" }));
    expect(onDocumentChange).toHaveBeenCalled();
    const nextDocument = onDocumentChange.mock.calls.at(-1)?.[0] as DefinitionGraphDocument;
    expect(nextDocument.nodes.some((node) => node.type === "wait")).toBe(true);
  });

  it("action 変更時に input をクリアする", () => {
    const parsed = parseDefinitionYaml(defaultDefinitionYaml, parseOpts);
    expect(parsed.document).not.toBeNull();
    const onDocumentChange = vi.fn();
    const documentWithInput: DefinitionGraphDocument = {
      ...parsed.document!,
      nodes: parsed.document!.nodes.map((entry) =>
        entry.id === "slowStep" && entry.type === "action"
          ? {
              ...entry,
              input: {
                channel: "email",
                to: "user@example.com"
              }
            }
          : entry
      )
    };

    renderWithUiText(
      <DefinitionGraphEditor
        document={documentWithInput}
        onDocumentChange={onDocumentChange}
        validationMessages={[]}
        labels={definitionGraphEditorTestLabels}
      />
    );

    fireEvent.click(screen.getByText("slowStep"));
    const actionInput = screen.getByDisplayValue("sleep");
    fireEvent.change(actionInput, { target: { value: "statevia.action.builtin.noop" } });
    fireEvent.blur(actionInput);

    const nextDocument = onDocumentChange.mock.calls.at(-1)?.[0] as DefinitionGraphDocument;
    const updatedNode = nextDocument.nodes.find((entry) => entry.id === "slowStep");
    expect(updatedNode?.type).toBe("action");
    if (updatedNode?.type === "action") {
      expect(updatedNode.action).toBe("statevia.action.builtin.noop");
      expect(updatedNode.input).toBeUndefined();
    }
  });

  it("ノード選択後に action を更新する", () => {
    const parsed = parseDefinitionYaml(defaultDefinitionYaml, parseOpts);
    expect(parsed.document).not.toBeNull();
    const onDocumentChange = vi.fn();

    renderWithUiText(
      <DefinitionGraphEditor
        document={parsed.document}
        onDocumentChange={onDocumentChange}
        validationMessages={[]}
        labels={definitionGraphEditorTestLabels}
      />
    );

    fireEvent.click(screen.getByText("slowStep"));
    const actionInput = screen.getByDisplayValue("sleep");
    fireEvent.change(actionInput, { target: { value: "noop" } });
    fireEvent.blur(actionInput);
    expect(onDocumentChange).toHaveBeenCalled();
  });

  it("一覧にない actionId では詳細 API を呼ばない", async () => {
    const parsed = parseDefinitionYaml(defaultDefinitionYaml, parseOpts);
    expect(parsed.document).not.toBeNull();

    renderWithUiText(<StatefulGraphEditorHarness initialDocument={parsed.document!} />);

    fireEvent.click(screen.getByText("slowStep"));
    await vi.waitFor(() => {
      expect(screen.getByDisplayValue("sleep")).toBeInTheDocument();
    });

    const detailCalls = vi
      .mocked(apiGet)
      .mock.calls.filter((call) => String(call[0]).startsWith("/actions/schema/") && call[0] !== "/actions/schema/index");
    expect(detailCalls).toHaveLength(0);

    fireEvent.change(screen.getByDisplayValue("sleep"), { target: { value: "custom.action" } });
    fireEvent.blur(screen.getByDisplayValue("custom.action"));

    const afterBlurCalls = vi
      .mocked(apiGet)
      .mock.calls.filter((call) => String(call[0]).startsWith("/actions/schema/") && call[0] !== "/actions/schema/index");
    expect(afterBlurCalls).toHaveLength(0);
  });

  it("index に一致した actionId の入力時のみ Schema API を呼ぶ", async () => {
    const parsed = parseDefinitionYaml(defaultDefinitionYaml, parseOpts);
    expect(parsed.document).not.toBeNull();
    vi.mocked(apiGet).mockImplementation(async (path: string) => {
      if (path.startsWith("/actions/schema/")) {
        return {
          descriptor: { actionId: "statevia.action.builtin.noop", version: "1.0.0", displayName: "Noop" },
          schema: {
            schemaVersion: "2020-12",
            inputSchema: { type: "object", properties: {} },
            outputSchema: { type: "object" }
          }
        };
      }
      throw new Error(`unexpected path: ${path}`);
    });

    renderWithUiText(<StatefulGraphEditorHarness initialDocument={parsed.document!} />);

    fireEvent.click(screen.getByText("slowStep"));
    await vi.waitFor(() => {
      expect(screen.getByDisplayValue("sleep")).toBeInTheDocument();
    });
    vi.mocked(apiGet).mockClear();
    vi.mocked(apiGet).mockImplementation(async (path: string) => {
      if (path.startsWith("/actions/schema/")) {
        return {
          descriptor: { actionId: "statevia.action.builtin.rest", version: "1.0.0", displayName: "REST" },
          schema: {
            schemaVersion: "2020-12",
            inputSchema: { type: "object", properties: {} },
            outputSchema: { type: "object" }
          }
        };
      }
      throw new Error(`unexpected path: ${path}`);
    });

    const actionInput = screen.getByDisplayValue("sleep");
    const detailPathCalls = (calls: unknown[][]) =>
      calls.filter((call) => String(call[0]).startsWith("/actions/schema/") && call[0] !== "/actions/schema/index");

    fireEvent.change(actionInput, { target: { value: "statevia.action.builtin.r" } });
    expect(detailPathCalls(vi.mocked(apiGet).mock.calls)).toHaveLength(0);

    fireEvent.change(actionInput, { target: { value: "statevia.action.builtin.rest" } });
    await vi.waitFor(() => {
      expect(
        vi.mocked(apiGet).mock.calls.filter((call) => call[0] === "/actions/schema/statevia.action.builtin.rest")
      ).toHaveLength(1);
    });
    expect(detailPathCalls(vi.mocked(apiGet).mock.calls)).toHaveLength(1);
  });
});
