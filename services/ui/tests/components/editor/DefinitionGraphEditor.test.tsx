import { describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import { DefinitionGraphEditor } from "../../../app/components/editor/DefinitionGraphEditor";
import { defaultDefinitionYaml } from "../../../app/lib/defaultDefinitionYaml";
import { parseDefinitionYaml } from "../../../app/lib/definition-editor/parseDefinitionYaml";
import { renderWithUiText } from "../../testUtils";
import { definitionGraphEditorTestLabels } from "./definitionGraphEditorLabels";

class ResizeObserverMock {
  observe() {}
  unobserve() {}
  disconnect() {}
}

vi.stubGlobal("ResizeObserver", ResizeObserverMock);

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
});
