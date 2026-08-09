import type { ExecutionsFeatureUiText } from "./types";
import { executionsUiTextJa } from "./ja";

/**
 * executions feature の英語辞書切片（未翻訳は ja を継承）。
 */
export const executionsUiTextEn: ExecutionsFeatureUiText = {
  ...executionsUiTextJa,
  executionDashboard: {
    ...executionsUiTextJa.executionDashboard,
    header: {
      titleDefault: "Execution detail",
    },
    actions: {
      sectionTitle: "Execution actions",
      eventNameLabel: "Event name",
      eventNamePlaceholder: "event-name",
    },
    validation: {
      eventNameTooLong: "Event name must be within 64 characters.",
      eventNameInvalidFormat: "Event name must start with an ASCII letter and use only ASCII alphanumerics plus . - _.",
    },
    graph: {
      fullscreenEnter: "Fullscreen",
      fullscreenExit: "Exit fullscreen (Esc)",
      definitionMissingFallback: (graphId: string) =>
        `No registered graph definition for graph ID ${graphId}. Showing temporary edges.`,
    },
    errors: {
      executionNotFound: "The specified execution was not found. Please check the ID.",
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
    ...executionsUiTextJa.executionTimeline,
    title: "Event timeline",
    backToCurrent: "Back to current",
    replayingPastStateMessage: 'Showing a past state. Click "Back to current" to return to the latest state.',
    empty: "No events",
    loadMore: "Load more",
    errorUnknown: "An unknown error occurred.",

  },
  executionComparison: {
    ...executionsUiTextJa.executionComparison,
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
    ...executionsUiTextJa.nodeDetail,
    prompts: {
      loadExecution: (executionLabel: string) => `Please load ${executionLabel}.`,
      selectNode: (nodeLabel: string) => `Please select ${nodeLabel}.`,
    },
    title: (nodeLabel: string) => `${nodeLabel} detail`,
    meta: {
      type: (nodeType: string) => `Type: ${nodeType}`,
      nodeName: (nodeName: string) => `Node name: ${nodeName}`,
      nodeId: (id: string) => `Node ID: ${id}`,
      workerId: (workerId: string) => `Worker ID: ${workerId}`,
      attempt: (attempt: number) => `Attempts: ${attempt}`,
      waitKey: (waitKey: string) => `Wait key: ${waitKey}`,
      allowedEvents: (events: string) => `Allowed events: ${events}`,
      canceledByExecution: (canceledByExecution: boolean) => `Canceled: ${String(canceledByExecution)}`,
    },
    waiting: {
      title: "Waiting",
      reasonWaitByWaitKeyAndResumeWait: "Reason: waiting for resume event by wait key",
      resumeEventName: (eventName: string) => `Resume event name: ${eventName}`,
      selectResumeEvent: "Event to resume with",
    },
    cancel: {
      detailTitle: (cancelLabel: string) => `${cancelLabel} detail`,
      convergedByExecutionCancel: "Converged by execution cancellation",
    },
    failure: {
      title: "Failure",
      noMessage: "(No message)",
    },
    trace: {
      startedAt: (formattedInstant: string) => `Started: ${formattedInstant}`,
      completedAt: (formattedInstant: string) => `Completed: ${formattedInstant}`,
      duration: (durationText: string) => `Duration: ${durationText}`,
      durationUnavailable: "Duration: —",
      outputHeading: "Output",
      outputEmpty: "(None)",
      inputHeading: "Input",
      inputEmpty: "(None)",
      conditionRoutingHeading: "Condition routing",
      conditionRoutingEmpty: "(None)",
    },

  },
  graphLegend: {
    ...executionsUiTextJa.graphLegend,
    heading: {
      nodeStatus: "Node status",
      edgeType: "Edge type",
    },
    aria: {
      root: "Graph legend",
      nodeStatus: "Node status legend",
      edgeType: "Edge type legend",
    },
    edgeKind: {
      nextTraversed: "Next (executed path)",
      nextNotTraversed: "Next (not traversed)",
    },

  },
  executionHeader: {
    ...executionsUiTextJa.executionHeader,
    placeholderExecutionId: "ex-1",
    executionIdLabel: (executionLabel: string) => `${executionLabel} ID`,
    compareLabel: "Compare",
    realtimeSseLabel: "Realtime updates",
    cancelRequestedLabel: "Cancel requested",
    graphIdLine: (graphIdLabel: string, graphId: string) => `${graphIdLabel}: ${graphId}`,
    cancelRequestedLine: (cancelRequestedLabel: string, cancelRequested: boolean) =>
      `${cancelRequestedLabel}: ${cancelRequested ? "Yes" : "No"}`,

  },
  executionsPage: {
    ...executionsUiTextJa.executionsPage,
    loading: "Loading executions.",
    listSummary: (totalCount: number, page: number) => `Total ${totalCount} (page ${page})`,
    updatedAt: (formattedDateTime: string) => `Updated: ${formattedDateTime}`,
    empty: "No executions found.",
    error: "Failed to load executions. Please try again later.",
    pagination: {
      ...executionsUiTextJa.executionsPage.pagination,
      ariaLabel: "Executions pagination",
      currentPage: (page: number) => `Page ${page}`,
      prev: "Prev",
      next: "Next",
    },
    filter: {
      ...executionsUiTextJa.executionsPage.filter,
      contextActivePrefix: "Definition filter:",
      clearDefinition: "Clear definition filter",
      title: "Filters",
      all: "(All)",
      statusRunning: "Running",
      statusCompleted: "Completed",
      statusCancelled: "Cancelled",
      statusFailed: "Failed",
      definitionInputHint: "Definition display ID / UUID",
      definitionLabelWithHint: (definitionLabel: string) => `${definitionLabel} (display ID / UUID)`,
      definitionPlaceholder: "e.g. def-...",
      nameInputHint: "name (execution display ID partial match, or execution UUID exact match)",
      search: "Search",
      clear: "Clear",
      sortByLabel: "Sort by",
      sortOrderLabel: "Order",
      sortByUpdatedAt: "Updated at",
      sortByDisplayId: "Display ID",
      sortOrderDesc: "Descending",
      sortOrderAsc: "Ascending",
      invalidName: "Name allows only ASCII alphanumerics plus . - _ within 100 characters.",
      invalidDefinitionId: "Definition ID allows only ASCII alphanumerics plus - _ within 80 characters.",
      pageInfo: (limit: number, offset: number, page: number) =>
        `Items per page: ${limit}. Offset: ${offset} (approx. page ${page})`,
    },
    actions: {
      openDetail: "Details",
    },

  },
  executionDetailPage: {
    ...executionsUiTextJa.executionDetailPage,
    title: "Execution detail",
    missingExecutionId: "Execution ID is missing.",
    navRun: "Run",
    navGraph: "Graph",

  },
  executionGraphPage: {
    ...executionsUiTextJa.executionGraphPage,
    title: "Execution graph",
    missingExecutionId: "Execution ID is missing.",
    navDetail: "Detail",
    navRun: "Run",

  },
  executionRunPage: {
    ...executionsUiTextJa.executionRunPage,
    title: "Execution run",
    missingExecutionId: "Execution ID is missing.",
    navDetail: "Detail",
    navGraph: "Graph",

  },
  executionStatusBanner: {
    cancelRequestedNotice: (cancelLabel: string, resumeLabel: string) =>
      `${cancelLabel} already requested, so progress actions like ${resumeLabel} are disabled.`,
    terminalNotice: (executionLabel: string) => `${executionLabel} is already finished.`,

  },
  nodeList: {
    ...executionsUiTextJa.nodeList,
    title: "Node list",
    nodeCount: (count: number) => `${count} items`,
    columns: {
      status: "Status",
      type: "Type",
      nodeName: "Node name",
      nodeId: "Node ID",
      duration: "Duration",
    },

  },
  nodeGraph: {
    meta: {
      type: (nodeType: string) => `Type: ${nodeType}`,
      attempt: (attempt: number) => `Attempts: ${attempt}`,
      waitKey: (waitKey: string) => `Wait key: ${waitKey}`,
    },
    aria: {
      selectNode: (displayLabel: string) => `Select node: ${displayLabel}`,
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
      noResumeEvent: "Resume is unavailable because there is no allowed event.",
    },

  },
};
