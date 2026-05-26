import { describe, expect, it, vi } from "vitest";
import { render, waitFor } from "@testing-library/react";
import { ActionInputCodeEditor } from "../../../app/components/editor/ActionInputCodeEditor";

describe("ActionInputCodeEditor", () => {
  it("JSON モードで編集できる", async () => {
    const onChange = vi.fn();
    const { container } = render(
      <ActionInputCodeEditor
        value='{"x":1}'
        onChange={onChange}
        syntaxHighlight="jsonOnly"
        ariaLabel="Input"
      />
    );

    await waitFor(() => {
      expect(container.querySelector(".cm-editor")).toBeTruthy();
    });
  });
});
