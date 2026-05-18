import type { ComponentProps } from "react";
import type { DefinitionGraphEditor } from "../../../app/components/editor/DefinitionGraphEditor";

/** DefinitionGraphEditor テスト用の最小ラベルセット。 */
export const definitionGraphEditorTestLabels: ComponentProps<typeof DefinitionGraphEditor>["labels"] = {
  title: "Graph",
  empty: "Empty",
  addNode: "Add",
  addNodeDialogTitle: "Add node",
  addNodeDisabledReasonStart: "Start exists",
  addNodeDisabledReasonEnd: "End exists",
  nodeInspectorTitle: "Node",
  edgeInspectorTitle: "Edge",
  deleteNode: "Delete node",
  deleteEdge: "Delete edge",
  apply: "Apply",
  closeDialog: "Close",
  selfReferenceRejected: "Self ref",
  whenOpPlaceholder: "op",
  whenPathPlaceholder: "path",
  whenValuePlaceholder: "value",
  whenValueDisabledForExists: "disabled",
  whenValueHintIn: "in hint",
  whenValueHintBetween: "between hint",
  fullscreenEnter: "Fullscreen",
  fullscreenExit: "Exit fullscreen",
  actionInputLabel: "Input",
  actionErrorLabel: "Error",
  actionInputPlaceholder: "JSON",
  actionInputHint: "hint",
  actionInputInvalidJson: "invalid json"
};
