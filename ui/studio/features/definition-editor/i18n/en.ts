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
      nodeNameRequired: () => "node.name is required.",
      duplicateNodeName: (nodeName: string) => `Duplicate node.name: '${nodeName}'`,
      startCountInvalid: (count: number) => `Exactly one start node is required (current: ${count}).`,
      endCountInvalid: (count: number) => `Exactly one end node is required (current: ${count}).`,
      startRequiresTransition: (nodeName: string) => `Node '${nodeName}': start requires next or edges.`,
      actionRequired: (nodeName: string) => `Node '${nodeName}': action node requires action.`,
      actionRequiresTransition: (nodeName: string) => `Node '${nodeName}': action requires next or edges.`,
      waitEventRequired: (nodeName: string) =>
        `Node '${nodeName}': wait node requires events or event.`,
      waitRequiresTransition: (nodeName: string) =>
        `Node '${nodeName}': legacy wait (event) requires next or edges.`,
      waitEventsAndEventTogether: (nodeName: string) =>
        `Node '${nodeName}': wait cannot use both events and event.`,
      waitEventsCannotHaveEdges: (nodeName: string) =>
        `Node '${nodeName}': wait cannot use edges with events; put targets in events.`,
      waitEventTargetRequired: (nodeName: string, eventName: string) =>
        `Node '${nodeName}': events['${eventName}'] requires a next node name.`,
      forkBranchesRequired: (nodeName: string) => `Node '${nodeName}': fork requires at least two branches.`,
      joinRequiresTransition: (nodeName: string) => `Node '${nodeName}': join requires next or edges.`,
      joinModeInvalid: (nodeName: string) => `Node '${nodeName}': join.mode must be 'all'.`,
      endCannotHaveTransition: (nodeName: string) => `Node '${nodeName}': end cannot have next/edges.`,
      edgeToRequired: (nodeName: string) => `Node '${nodeName}': edge.to is required.`,
      edgeWhenPathRequired: (nodeName: string) => `Node '${nodeName}': edge.when.path is required.`,
      edgeWhenOpRequired: (nodeName: string) => `Node '${nodeName}': edge.when.op is required.`,
      edgeWhenValueRequired: (nodeName: string) =>
        `Node '${nodeName}': edge.when.value is required for this operator (not EXISTS).`,
      edgeWhenValueInInvalid: (nodeName: string) =>
        `Node '${nodeName}': IN requires a non-empty array (or JSON array string) for edge.when.value.`,
      edgeWhenValueBetweenInvalid: (nodeName: string) =>
        `Node '${nodeName}': BETWEEN requires an array of at least two values (or JSON array string) for edge.when.value.`,
      edgeDefaultMultiple: (nodeName: string) =>
        `Node '${nodeName}': only one edge can have default=true.`,
      selfReferenceEdge: (nodeName: string) => `Node '${nodeName}': self-referencing edge is not allowed.`,
      missingTargetNode: (nodeName: string, targetName: string) => `Node '${nodeName}': target '${targetName}' does not exist.`,
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
      waitEventsSectionTitle: "events (event → target)",
      waitEventNameLabel: "Event name",
      waitEventTargetLabel: "Target node name",
      waitEventsAdd: "Add event row",
      waitEventsRemove: "Remove row",
      waitLegacyEventLabel: "event (legacy)",
      waitConvertToEvents: "Convert to events",
      waitEventsConflictHint:
        "Cannot edit while both events and event are set. Fix YAML or convert to events.",
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
