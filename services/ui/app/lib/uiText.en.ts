import type { UiText } from "./uiText";
import { uiTextJa } from "./uiText";

/**
 * 英語辞書。段階移行のため、未翻訳キーは日本語辞書を継承する。
 */
export const uiTextEn: UiText = {
  ...uiTextJa,
  actionLinks: {
    aria: {
      navigation: "Navigation links",
    },
  },
  navigation: {
    dashboard: "Dashboard",
    definitions: "Definitions",
    workflows: "Workflows",
    health: "Health",
  },
  entities: {
    workflow: "Workflow",
    definition: "Definition",
    execution: "Execution",
    node: "Node",
  },
  lists: {
    ...uiTextJa.lists,
    workflows: "Workflows",
    definitions: "Definitions",
    nodeCount: (count: number) => `${count} items`,
  },
  actions: {
    ...uiTextJa.actions,
    load: "Load",
    loading: "Loading...",
    reload: "Reload",
    cancel: "Cancel",
    resume: "Resume",
    retry: "Retry",
    save: "Save",
    sendEvent: "Send event",
    openDetail: "Details",
    closeToast: "Close notification",
    viewList: "List",
    viewGraph: "Graph",
  },
  pagination: {
    prev: "Prev",
    next: "Next",
  },
  labels: {
    ...uiTextJa.labels,
    status: "Status",
    nodeId: "Node ID",
    definitionId: "Definition ID",
    displayId: "Display ID",
    resourceId: "Resource ID",
    graphId: "Graph ID",
    workflowInput: "Workflow input",
    definitionEditor: "Definition Editor",
  },
  errorPrefixes: {
    unauthorized401: "401 Authentication required",
    forbidden403: "403 Insufficient permission or tenant is missing",
    conflict409: "409 State conflict",
    unprocessable422: "422 Invalid input",
    server500: "500 Server error",
  },
  executionDashboard: {
    ...uiTextJa.executionDashboard,
    header: {
      titleDefault: "Execution detail",
    },
    actions: {
      sectionTitle: "Execution actions",
      eventNameLabel: "Event name",
      eventNamePlaceholder: "event-name",
    },
    graph: {
      fullscreenEnter: "Fullscreen",
      fullscreenExit: "Exit fullscreen (Esc)",
      definitionMissingFallback: (graphId: string) =>
        `No registered graph definition for graph ID ${graphId}. Showing temporary edges.`,
    },
    errors: {
      workflowNotFound: "The specified workflow was not found. Please check the ID.",
    },
    toasts: {
      cancelAccepted: "CancelExecution accepted",
      publishAccepted: "PublishEvent accepted",
      resumeAccepted: "ResumeNode accepted",
    },
    replayDisabledReason: "Actions are disabled while replaying.",
    operationsAggregatedInRun: (cancelLabel: string, resumeLabel: string, sendEventLabel: string) =>
      `${cancelLabel} / ${resumeLabel} / ${sendEventLabel} are grouped in the Run screen.`,
  },
  executionTimeline: {
    ...uiTextJa.executionTimeline,
    title: "Execution timeline",
    backToCurrent: "Back to current",
    replayingPastStateMessage: 'Showing a past state. Click "Back to current" to return to the latest state.',
    empty: "No events",
    loadMore: "Load more",
    errorUnknown: "An unknown error occurred.",
  },
  executionComparison: {
    ...uiTextJa.executionComparison,
    title: "Compare two executions",
    executionIdPlaceholder: "ex-2",
    executionABaselineLabel: (executionLabel: string) => `${executionLabel} A (baseline)`,
    executionBLabel: (executionLabel: string) => `${executionLabel} B`,
    kind: {
      onlyLeft: "A only",
      onlyRight: "B only",
      diff: "Diff",
    },
    state: {
      notLoaded: "Not loaded",
    },
    summary: {
      title: "Diff summary",
      failedOrCancelled: "Failed / Cancelled",
      others: "Others",
      noDiff: "No node differences",
      loadBothToShow: "Load A and B to show differences.",
    },
  },
  nodeDetail: {
    ...uiTextJa.nodeDetail,
    prompts: {
      loadExecution: (executionLabel: string) => `Please load ${executionLabel}.`,
      selectNode: (nodeLabel: string) => `Please select ${nodeLabel}.`,
    },
    title: (nodeLabel: string) => `${nodeLabel} detail`,
    meta: {
      type: (nodeType: string) => `Type: ${nodeType}`,
      attempt: (attempt: number) => `Attempts: ${attempt}`,
      waitKey: (waitKey: string) => `Wait key: ${waitKey}`,
      canceledByExecution: (canceledByExecution: boolean) => `Canceled: ${String(canceledByExecution)}`,
    },
    waiting: {
      title: "Waiting",
      reasonWaitByWaitKeyAndResumeWait: "Reason: waiting for resume event by wait key",
      resumeEventName: (eventName: string) => `Resume event name: ${eventName}`,
    },
    cancel: {
      detailTitle: (cancelLabel: string) => `${cancelLabel} detail`,
      convergedByExecutionCancel: "Converged by execution cancellation",
    },
    failure: {
      title: "Failure",
      noMessage: "(No message)",
    },
  },
  graphLegend: {
    ...uiTextJa.graphLegend,
    heading: {
      nodeStatus: "Node status",
      edgeType: "Edge type",
    },
    aria: {
      root: "Graph legend",
      nodeStatus: "Node status legend",
      edgeType: "Edge type legend",
    },
  },
  executionHeader: {
    ...uiTextJa.executionHeader,
    placeholderExecutionId: "ex-1",
    executionIdLabel: (executionLabel: string) => `${executionLabel} ID`,
    compareLabel: "Compare",
    realtimeSseLabel: "Realtime updates (SSE)",
    cancelRequestedLabel: "cancelRequested",
    graphIdLine: (graphIdLabel: string, graphId: string) => `${graphIdLabel}: ${graphId}`,
    cancelRequestedLine: (cancelRequestedLabel: string, cancelRequested: boolean) =>
      `${cancelRequestedLabel}: ${cancelRequested ? "true" : "false"}`,
  },
  pageState: {
    loading: "Loading...",
    empty: "No data to display.",
    error: "Failed to load data.",
  },
  dashboard: {
    ...uiTextJa.dashboard,
    title: "Dashboard",
    descriptionRecent: "Recent workflows (up to 10).",
    loadingRecent: "Loading recent workflows.",
    emptyStartFromDefinitionsOrWorkflows: "Start from Definitions or Workflows.",
    totalCount: (count: number | null) => (count == null ? "Total: --" : `Total: ${count}`),
    updatedAt: (formattedDateTime: string) => `Updated: ${formattedDateTime}`,
    aria: {
      recentWorkflowsList: "Recent workflows",
    },
    actions: {
      openDetail: "Open details",
    },
    error: {
      fetchFailed: "Failed to fetch data.",
    },
  },
  workflowsPage: {
    ...uiTextJa.workflowsPage,
    loading: "Loading workflows.",
    listSummary: (totalCount: number, page: number) => `Total ${totalCount} (page ${page})`,
    updatedAt: (formattedDateTime: string) => `Updated: ${formattedDateTime}`,
    empty: "No workflows found.",
    error: "Failed to load workflows. Please try again later.",
    pagination: {
      ...uiTextJa.workflowsPage.pagination,
      ariaLabel: "Workflows pagination",
      currentPage: (page: number) => `Page ${page}`,
      prev: "Prev",
      next: "Next",
    },
    filter: {
      ...uiTextJa.workflowsPage.filter,
      contextActivePrefix: "Definition filter:",
      clearDefinition: "Clear definition filter",
      title: "Filters",
      all: "(All)",
      definitionInputHint: "Definition display ID / UUID",
      definitionLabelWithHint: (definitionLabel: string) => `${definitionLabel} (display ID / UUID)`,
      definitionPlaceholder: "e.g. def-...",
      nameInputHint: "name (workflow display ID partial match, or workflow UUID exact match)",
      search: "Search",
      clear: "Clear",
      pageInfo: (limit: number, offset: number, page: number) =>
        `Items per page: ${limit}. Offset: ${offset} (approx. page ${page})`,
    },
    actions: {
      openDetail: "Details",
    },
  },
  definitionsPage: {
    ...uiTextJa.definitionsPage,
    description: "Search and paginate definitions, then open the detail page.",
    loading: "Loading definitions.",
    emptyNoMatch: "No definitions matched. Change the query or clear filters.",
    searchSummaryPrefix: (keyword: string) => `Search: "${keyword}" / `,
    listSummary: (totalCount: number, page: number) => `Total ${totalCount} (page ${page})`,
    createdAt: (formattedDateTime: string) => `Created: ${formattedDateTime}`,
    error: "Failed to load definitions.",
    pagination: {
      ...uiTextJa.definitionsPage.pagination,
      ariaLabel: "Definitions pagination",
      currentPage: (page: number) => `Page ${page}`,
    },
    search: {
      label: "Name search (partial match)",
      placeholder: "e.g. order",
      submit: "Search",
      clear: "Clear",
    },
    actions: {
      openDetail: "Open details",
    },
  },
  workflowDetailPage: {
    ...uiTextJa.workflowDetailPage,
    title: "Workflow detail",
    missingWorkflowId: "Workflow ID is missing.",
    navRun: "Run",
    navGraph: "Graph",
  },
  workflowGraphPage: {
    ...uiTextJa.workflowGraphPage,
    title: "Workflow graph",
    missingWorkflowId: "Workflow ID is missing.",
    navDetail: "Detail",
    navRun: "Run",
  },
  workflowRunPage: {
    ...uiTextJa.workflowRunPage,
    title: "Workflow run",
    missingWorkflowId: "Workflow ID is missing.",
    navDetail: "Detail",
    navGraph: "Graph",
  },
  definitionRunPage: {
    ...uiTextJa.definitionRunPage,
    title: "Run definition",
    unspecifiedDefinitionId: "(not specified)",
    workflowInputLabelWithHint: (workflowInputLabel: string) => `${workflowInputLabel} (optional, JSON)`,
    inputJsonPlaceholder: 'Example: {"orderId":"123"}',
    nav: {
      backToDefinitionDetail: "Back to definition detail",
    },
    actions: {
      starting: "Starting...",
      startWorkflow: "Start workflow",
    },
    toasts: {
      definitionIdRequired: (definitionIdLabel: string) => `${definitionIdLabel} is required.`,
      invalidWorkflowInputJson: (workflowInputLabel: string) => `${workflowInputLabel} JSON is invalid.`,
      workflowStarted: (workflowDisplayId: string) => `Workflow started: ${workflowDisplayId}`,
    },
    help: {
      redirectAfterStart: (runPath: string) => `After starting, you will be redirected to ${runPath}.`,
    },
  },
  definitionDetail: {
    ...uiTextJa.definitionDetail,
    title: "Definition detail",
    urlPrefix: "URL:",
    errorFetchFailed: "Failed to fetch definition.",
    ariaMeta: "Definition metadata",
    meta: {
      name: "Name",
      createdAt: "Created at",
    },
    relatedWorkflows: {
      title: "Related workflows",
      description: "Go to the list of workflows related to this definition.",
      openList: "Open workflows list",
    },
    actions: {
      title: "Edit / Run",
      edit: "Edit definition",
      run: "Start new execution",
    },
    nav: {
      backToDefinitions: "Back to definitions",
    },
  },
  definitionEditor: {
    ...uiTextJa.definitionEditor,
    backToDetail: "Back to definition detail",
    descriptionEditingTarget: (definitionId: string) => `Editing target: ${definitionId}`,
    loadingMeta: "Loading definition metadata...",
    validation: {
      nameRequired: "Please enter a definition name.",
      yamlRequired: "Please enter YAML.",
    },
    labels: {
      name: "Definition name (name)",
      yaml: "YAML",
    },
    actions: {
      saving: "Saving...",
      saveWithApiHint: "Save (POST /definitions)",
      resetTemplate: "Reset to template",
    },
    noteMvp:
      "In MVP, the update API is unavailable, so Save creates a new definition. For invalid input, the raw 422 response is shown.",
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
  },
  tenantMissingBanner: {
    noticeParts: (loadLabel: string, cancelLabel: string, resumeLabel: string) => ({
      beforePrimaryEnv: `Tenant is not set. ${loadLabel} / ${cancelLabel} / ${resumeLabel} may fail. Set `,
      betweenEnvs: " or configure ",
      afterSecondaryEnv: " in the server environment.",
    }),
  },
  executionStatusBanner: {
    cancelRequestedNotice: (cancelLabel: string, resumeLabel: string) =>
      `${cancelLabel} already requested, so progress actions like ${resumeLabel} are disabled.`,
    terminalNotice: (executionLabel: string) => `${executionLabel} is already finished.`,
  },
  nodeList: {
    ...uiTextJa.nodeList,
    title: "Node list",
    nodeCount: (count: number) => `${count} items`,
    columns: {
      nodeId: "Node ID",
      type: "Type",
      status: "Status",
      waitKey: "Wait key",
    },
  },
  nodeGraph: {
    meta: {
      type: (nodeType: string) => `Type: ${nodeType}`,
      attempt: (attempt: number) => `Attempts: ${attempt}`,
      waitKey: (waitKey: string) => `Wait key: ${waitKey}`,
    },
  },
  nodeCommands: {
    resumeDisabledReason: {
      runOnly: "Resume is available only on the Run screen.",
      executionNotLoaded: "Execution is not loaded.",
      nodeNotSelected: "Please select a node.",
      executionTerminal: "Execution is already finished.",
      cancelRequested: "Cancel was requested, so progress actions like Resume are disabled.",
      waitingOnly: "Resume is available only for WAITING nodes.",
    },
  },
};

