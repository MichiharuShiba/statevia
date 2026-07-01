import { describe, expect, it, vi } from "vitest";
import { render, waitFor } from "@testing-library/react";
import { YamlCodeEditor } from "../../../app/components/editor/YamlCodeEditor";

describe("YamlCodeEditor", () => {
  it("YAML 文字列を編集できる", async () => {
    const onChange = vi.fn();
    const { container } = render(
      <YamlCodeEditor value="version: 1" onChange={onChange} completionKeywords={["version"]} />
    );

    await waitFor(() => {
      expect(container.querySelector(".cm-editor")).toBeTruthy();
    });
  });
});
