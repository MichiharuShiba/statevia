import { describe, expect, it, vi, beforeEach } from "vitest";
import { act, fireEvent, screen, waitFor } from "@testing-library/react";
import { useState } from "react";
import { DefinitionGraphEditor } from "@/features/definition-editor/ui/DefinitionGraphEditor";
import { resetActionSchemaIndexSessionCacheForTests } from "@/features/definition-editor/actionSchema/actionSchemaIndexSessionCache";
import { defaultDefinitionYaml } from "@/features/definition-editor/lib/defaultDefinitionYaml";
import { parseDefinitionYaml } from "@/features/definition-editor/lib/parseDefinitionYaml";
import type { DefinitionGraphDocument } from "@/features/definition-editor/lib/types";
import { renderWithUiText } from "../../testUtils";
import { definitionGraphEditorTestLabels } from "./definitionGraphEditorLabels";

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return { ...actual, apiGet: vi.fn() };
});

import { apiGet } from "@/shared/api";

const parseOpts = {
  rootObjectRequired: () => "root",
  nodesArrayRequired: () => "nodes"
};

/**
 * GraphInspector の mount 時 schema index 取得と setState を act 内で完了させる。
 * 未待ちだと "An update to GraphInspector was not wrapped in act(...)" が出る。
 */
async function settleGraphInspectorSchemaIndex(): Promise<void> {
  await waitFor(() => {
    expect(vi.mocked(apiGet)).toHaveBeenCalledWith("/actions/schema/index");
  });
  await act(async () => {
    await new Promise<void>((resolve) => {
      setTimeout(resolve, 0);
    });
  });
}

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
    resetActionSchemaIndexSessionCacheForTests();
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

  it("ドキュメントをグラフとして描画する", async () => {
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
    await settleGraphInspectorSchemaIndex();

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

  it("バリデーションメッセージとフルスクリーンを操作できる", async () => {
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
    await settleGraphInspectorSchemaIndex();

    expect(screen.getByText("node id required")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: definitionGraphEditorTestLabels.fullscreenEnter }));
    expect(screen.getByRole("button", { name: definitionGraphEditorTestLabels.fullscreenExit })).toBeInTheDocument();
  });

  it("wait ノード追加で onDocumentChange を呼ぶ", async () => {
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
    await settleGraphInspectorSchemaIndex();

    fireEvent.click(screen.getByRole("button", { name: "wait" }));
    expect(onDocumentChange).toHaveBeenCalled();
    const nextDocument = onDocumentChange.mock.calls.at(-1)?.[0] as DefinitionGraphDocument;
    const waitNode = nextDocument.nodes.find((node) => node.type === "wait");
    expect(waitNode).toMatchObject({
      type: "wait",
      events: { resume: "" }
    });
    expect(waitNode?.event).toBeUndefined();
  });

  it("wait.events をインスペクタで追加・編集できる", async () => {
    const document: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "w" },
      nodes: [
        { name: "s", type: "start", next: "w1" },
        { name: "w1", type: "wait", events: { approve: "ok" } },
        { name: "ok", type: "end" }
      ]
    };
    const onDocumentChange = vi.fn();

    renderWithUiText(
      <DefinitionGraphEditor
        document={document}
        onDocumentChange={onDocumentChange}
        validationMessages={[]}
        labels={definitionGraphEditorTestLabels}
      />
    );
    await settleGraphInspectorSchemaIndex();

    fireEvent.click(screen.getByText("w1"));
    expect(screen.getByText(definitionGraphEditorTestLabels.waitEventsSectionTitle)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: definitionGraphEditorTestLabels.waitEventsAdd }));

    expect(onDocumentChange).toHaveBeenCalled();
    const afterAdd = onDocumentChange.mock.calls.at(-1)?.[0] as DefinitionGraphDocument;
    expect(afterAdd.nodes.find((node) => node.name === "w1")?.events).toEqual({
      approve: "ok",
      event1: ""
    });

    const eventNameInput = screen.getByDisplayValue("event1");
    const row = eventNameInput.closest("div");
    const targetInput = row?.querySelectorAll("input")[1];
    expect(targetInput).toBeTruthy();
    fireEvent.change(targetInput!, { target: { value: "ok" } });
    fireEvent.blur(targetInput!);

    const afterEdit = onDocumentChange.mock.calls.at(-1)?.[0] as DefinitionGraphDocument;
    expect(afterEdit.nodes.find((node) => node.name === "w1")?.events).toEqual({
      approve: "ok",
      event1: "ok"
    });
  });

  it("旧形式 wait を開き events へ変換できる", async () => {
    const document: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "w" },
      nodes: [
        { name: "s", type: "start", next: "w1" },
        { name: "w1", type: "wait", event: "resume", next: "ok" },
        { name: "ok", type: "end" }
      ]
    };
    const onDocumentChange = vi.fn();

    renderWithUiText(
      <DefinitionGraphEditor
        document={document}
        onDocumentChange={onDocumentChange}
        validationMessages={[]}
        labels={definitionGraphEditorTestLabels}
      />
    );
    await settleGraphInspectorSchemaIndex();

    fireEvent.click(screen.getByText("w1"));
    expect(screen.getByText(definitionGraphEditorTestLabels.waitLegacyEventLabel)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: definitionGraphEditorTestLabels.waitConvertToEvents }));

    const nextDocument = onDocumentChange.mock.calls.at(-1)?.[0] as DefinitionGraphDocument;
    expect(nextDocument.nodes.find((node) => node.name === "w1")).toEqual({
      name: "w1",
      type: "wait",
      events: { resume: "ok" }
    });
  });

  it("wait.events 2 件の YAML 往復後もインスペクタに表示される", async () => {
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - name: s
    type: start
    next: w1
  - name: w1
    type: wait
    events:
      approve: ok
      reject: ng
  - name: ok
    type: end
  - name: ng
    type: end
`;
    const parsed = parseDefinitionYaml(yaml, parseOpts);
    expect(parsed.document).not.toBeNull();

    renderWithUiText(
      <DefinitionGraphEditor
        document={parsed.document}
        onDocumentChange={vi.fn()}
        validationMessages={[]}
        labels={definitionGraphEditorTestLabels}
      />
    );
    await settleGraphInspectorSchemaIndex();

    fireEvent.click(screen.getByText("w1"));
    expect(screen.getByDisplayValue("approve")).toBeInTheDocument();
    expect(screen.getByDisplayValue("reject")).toBeInTheDocument();
    expect(screen.getByDisplayValue("ok")).toBeInTheDocument();
    expect(screen.getByDisplayValue("ng")).toBeInTheDocument();
  });

  it("action 変更時に input をクリアする", async () => {
    const parsed = parseDefinitionYaml(defaultDefinitionYaml, parseOpts);
    expect(parsed.document).not.toBeNull();
    const onDocumentChange = vi.fn();
    const documentWithInput: DefinitionGraphDocument = {
      ...parsed.document!,
      nodes: parsed.document!.nodes.map((entry) =>
        entry.name === "slowStep" && entry.type === "action"
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
    await settleGraphInspectorSchemaIndex();

    fireEvent.click(screen.getByText("slowStep"));
    await waitFor(() => {
      expect(screen.getByDisplayValue("sleep")).toBeInTheDocument();
    });
    const actionInput = screen.getByDisplayValue("sleep");
    await act(async () => {
      fireEvent.change(actionInput, { target: { value: "statevia.action.builtin.noop" } });
      fireEvent.blur(actionInput);
      await new Promise<void>((resolve) => {
        setTimeout(resolve, 0);
      });
    });

    const nextDocument = onDocumentChange.mock.calls.at(-1)?.[0] as DefinitionGraphDocument;
    const updatedNode = nextDocument.nodes.find((entry) => entry.name === "slowStep");
    expect(updatedNode?.type).toBe("action");
    if (updatedNode?.type === "action") {
      expect(updatedNode.action).toBe("statevia.action.builtin.noop");
      expect(updatedNode.input).toBeUndefined();
    }
  });

  it("ノード選択後に action を更新する", async () => {
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
    await settleGraphInspectorSchemaIndex();

    fireEvent.click(screen.getByText("slowStep"));
    await act(async () => {
      await new Promise<void>((resolve) => {
        setTimeout(resolve, 0);
      });
    });
    const actionInput = screen.getByDisplayValue("sleep");
    fireEvent.change(actionInput, { target: { value: "noop" } });
    fireEvent.blur(actionInput);
    expect(onDocumentChange).toHaveBeenCalled();
  });

  it("一覧にない actionId では詳細 API を呼ばない", async () => {
    const parsed = parseDefinitionYaml(defaultDefinitionYaml, parseOpts);
    expect(parsed.document).not.toBeNull();

    renderWithUiText(<StatefulGraphEditorHarness initialDocument={parsed.document!} />);
    await settleGraphInspectorSchemaIndex();

    fireEvent.click(screen.getByText("slowStep"));
    await waitFor(() => {
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
    await settleGraphInspectorSchemaIndex();

    fireEvent.click(screen.getByText("slowStep"));
    await waitFor(() => {
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
    await waitFor(() => {
      expect(
        vi.mocked(apiGet).mock.calls.filter((call) => call[0] === "/actions/schema/statevia.action.builtin.rest")
      ).toHaveLength(1);
    });
    await act(async () => {
      await new Promise<void>((resolve) => {
        setTimeout(resolve, 0);
      });
    });
    expect(detailPathCalls(vi.mocked(apiGet).mock.calls)).toHaveLength(1);
  });
});
