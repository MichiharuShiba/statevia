import type { DefinitionEditorFeatureUiText } from "./types";
import { definitionEditorUiTextJa } from "./ja";

/**
 * definition-editor feature の英語辞書切片（未翻訳は ja を継承）。
 */
export const definitionEditorUiTextEn: DefinitionEditorFeatureUiText = {
  ...definitionEditorUiTextJa,
  definitionEditor: {
    ...definitionEditorUiTextJa.definitionEditor,
    backToDetail: "Back to definition detail",
    descriptionCreating: "Create a new definition.",
    descriptionEditingTarget: (definitionId: string) => `Editing target: ${definitionId}`,
    loadingMeta: "Loading definition metadata...",
    validation: {
      nameRequired: "Please enter a definition name.",
      yamlRequired: "Please enter YAML.",
      yamlLintInvalid: "Please fix YAML syntax errors.",
      nameInvalidFormat: "Definition name must start with an ASCII letter and use only ASCII alphanumerics plus . - _ within 100 characters.",
      yamlTooLarge: "YAML must be within 256KB (262144 bytes).",
    },
    labels: {
      name: "Definition name (name)",
      yaml: "YAML",
    },
    actions: {
      saving: "Saving...",
      saveWithApiHint: "Save",
      resetTemplate: "Reset to initial",
      switchToYaml: "YAML",
      switchToGraph: "Graph",
    },
    graph: {
      title: "Graph editor",
      empty: "Graph cannot be shown because YAML parsing failed. Please fix YAML first.",
      addNode: "Add node",
      addNodeDialogTitle: "Add node",
      addNodeDisabledReasonStart: "Only one start node is allowed.",
      addNodeDisabledReasonEnd: "Only one end node is allowed.",
      nodeInspectorTitle: "Node inspector",
      edgeInspectorTitle: "Edge inspector",
      deleteNode: "Delete node",
      deleteEdge: "Delete edge",
      apply: "Apply",
      closeDialog: "Close",
      rootObjectRequired: () => "YAML root must be an object.",
      nodesArrayRequired: () => "nodes must be an array.",
      nodesRequired: () => "nodes requires at least one item.",
      nodeIdRequired: () => "node.id is required.",
      duplicateNodeId: (nodeId: string) => `Duplicate node.id: '${nodeId}'`,
      startCountInvalid: (count: number) => `Exactly one start node is required (current: ${count}).`,
      endCountInvalid: (count: number) => `Exactly one end node is required (current: ${count}).`,
      startRequiresTransition: (nodeId: string) => `Node '${nodeId}': start requires next or edges.`,
      actionRequired: (nodeId: string) => `Node '${nodeId}': action node requires action.`,
      actionRequiresTransition: (nodeId: string) => `Node '${nodeId}': action requires next or edges.`,
      waitEventRequired: (nodeId: string) => `Node '${nodeId}': wait node requires event.`,
      waitRequiresTransition: (nodeId: string) => `Node '${nodeId}': wait requires next or edges.`,
      forkBranchesRequired: (nodeId: string) => `Node '${nodeId}': fork requires at least two branches.`,
      joinRequiresTransition: (nodeId: string) => `Node '${nodeId}': join requires next or edges.`,
      joinModeInvalid: (nodeId: string) => `Node '${nodeId}': join.mode must be 'all'.`,
      endCannotHaveTransition: (nodeId: string) => `Node '${nodeId}': end cannot have next/edges.`,
      edgeToRequired: (nodeId: string) => `Node '${nodeId}': edge.to is required.`,
      edgeWhenPathRequired: (nodeId: string) => `Node '${nodeId}': edge.when.path is required.`,
      edgeWhenOpRequired: (nodeId: string) => `Node '${nodeId}': edge.when.op is required.`,
      edgeWhenValueRequired: (nodeId: string) =>
        `Node '${nodeId}': edge.when.value is required for this operator (not EXISTS).`,
      edgeWhenValueInInvalid: (nodeId: string) =>
        `Node '${nodeId}': IN requires a non-empty array (or JSON array string) for edge.when.value.`,
      edgeWhenValueBetweenInvalid: (nodeId: string) =>
        `Node '${nodeId}': BETWEEN requires an array of at least two values (or JSON array string) for edge.when.value.`,
      edgeDefaultMultiple: (nodeId: string) =>
        `Node '${nodeId}': only one edge can have default=true.`,
      selfReferenceEdge: (nodeId: string) => `Node '${nodeId}': self-referencing edge is not allowed.`,
      missingTargetNode: (nodeId: string, targetId: string) => `Node '${nodeId}': target '${targetId}' does not exist.`,
      selfReferenceRejected: "Self-referencing edges are not allowed.",
      whenOpPlaceholder: "Select operator",
      whenPathPlaceholder: "$.states.Fetch.output.amount",
      whenPathHint:
        "Root is Execution Context (same as input). e.g. $.states.A.output.y / $.states['a.b'].output.z / $.vars.flag / $.sys.today",
      whenValuePlaceholder: "e.g. 100 / true / \"100\"",
      whenValueDisabledForExists: "Value is not required for EXISTS.",
      whenValueHintIn: "For IN, enter a JSON array. Example: [\"A\", \"B\"]",
      whenValueHintBetween: "For BETWEEN, enter [min, max] as a JSON array. Example: [1, 10]",
      fullscreenEnter: "Fullscreen",
      fullscreenExit: "Exit fullscreen",
      parseFailed: "YAML parsing failed. Keeping the previous valid graph.",
      actionErrorLabel: "Error transition (optional)",
      actionInputLabel: "input (optional)",
      actionInputPlaceholder:
        'e.g. $.input.orderId / $.states.A.output.x / {"id":"$.states[\'a.b\'].output.id"}',
      actionInputHint:
        "Root is Execution Context ($.input / $.states / $.vars / $.sys). Dotted node IDs use $.states['id'].output. Objects as JSON.",
      actionInputInvalidJson: "Invalid JSON.",
      actionIdCandidatesLoading: "Loading actions…",
      actionIdNoResults: "No matching actions",
      schemaPathPlaceholder: "$.input.x or $.states.A.output.y",
      schemaLiteralOrPathPlaceholder: "literal or $.input.x",
    },
    saved: {
      completePrefix: "Saved:",
      complete: (displayId: string) => `Saved: ${displayId}`,
      openNewDetail: "Open new definition detail",
      runWithThisDefinition: "Run with this definition",
    },
    toasts: {
      savedWithDisplayId: (displayIdLabel: string, displayId: string) =>
        `Definition saved (${displayIdLabel}: ${displayId})`,
    },
    hints: {
      title: "Fix hints",
    },

  },
};
