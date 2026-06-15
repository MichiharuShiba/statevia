import { describe, expect, it, vi } from "vitest";
import { fireEvent, screen } from "@testing-library/react";
import { DefinitionGraphEditor } from "../../../app/components/editor/DefinitionGraphEditor";
import { defaultDefinitionYaml } from "../../../app/lib/defaultDefinitionYaml";
import { parseDefinitionYaml } from "../../../app/lib/definition-editor/parseDefinitionYaml";
import type { DefinitionGraphDocument } from "../../../app/lib/definition-editor/types";
import { renderWithUiText } from "../../testUtils";
import { definitionGraphEditorTestLabels } from "./definitionGraphEditorLabels";

const parseOpts = {
  rootObjectRequired: () => "root",
  nodesArrayRequired: () => "nodes"
};

describe("DefinitionGraphEditor", () => {
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
    fireEvent.change(screen.getByDisplayValue("sleep"), { target: { value: "noop" } });
    expect(onDocumentChange).toHaveBeenCalled();
  });
});
