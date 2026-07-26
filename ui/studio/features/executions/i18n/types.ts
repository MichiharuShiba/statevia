/**
 * executions feature の i18n 切片型。
 * shared の UiText が合成時に取り込む。
 */
export type ExecutionsFeatureUiText = {
  executionDashboard: {
    header: {
      titleDefault: string;
    };
    actions: {
      sectionTitle: string;
      eventNameLabel: string;
      eventNamePlaceholder: string;
    };
    validation: {
      eventNameTooLong: string;
      eventNameInvalidFormat: string;
    };
    graph: {
      fullscreenEnter: string;
      fullscreenExit: string;
      definitionMissingFallback: (graphId: string) => string;
    };
    errors: {
      executionNotFound: string;
    };
    toasts: {
      cancelAccepted: string;
      publishAccepted: string;
      resumeAccepted: string;
    };
    replayDisabledReason: string;
    operationsAggregatedInRun: (cancelLabel: string, resumeLabel: string, sendEventLabel: string) => string;

  },
  executionTimeline: {
    title: string;
    backToCurrent: string;
    replayingPastStateMessage: string;
    empty: string;
    loadMore: string;
    errorUnknown: string;

  },
  executionComparison: {
    title: string;
    executionIdPlaceholder: string;
    executionABaselineLabel: (executionLabel: string) => string;
    executionBLabel: (executionLabel: string) => string;
    kind: {
      onlyLeft: string;
      onlyRight: string;
      diff: string;
    };
    state: {
      notLoaded: string;
    };
    summary: {
      title: string;
      failedOrCancelled: string;
      others: string;
      noDiff: string;
      loadBothToShow: string;
    };

  },
  nodeDetail: {
    prompts: {
      loadExecution: (executionLabel: string) => string;
      selectNode: (nodeLabel: string) => string;
    };
    title: (nodeLabel: string) => string;
    meta: {
      type: (nodeType: string) => string;
      stateName: (stateName: string) => string;
      executionNodeId: (id: string) => string;
      workerId: (workerId: string) => string;
      attempt: (attempt: number) => string;
      waitKey: (waitKey: string) => string;
      canceledByExecution: (canceledByExecution: boolean) => string;
    };
    waiting: {
      title: string;
      reasonWaitByWaitKeyAndResumeWait: string;
      resumeEventName: (eventName: string) => string;
    };
    cancel: {
      detailTitle: (cancelLabel: string) => string;
      convergedByExecutionCancel: string;
    };
    failure: {
      title: string;
      noMessage: string;
    };
    trace: {
      startedAt: (formattedInstant: string) => string;
      completedAt: (formattedInstant: string) => string;
      duration: (durationText: string) => string;
      durationUnavailable: string;
      outputHeading: string;
      outputEmpty: string;
      inputHeading: string;
      inputEmpty: string;
      conditionRoutingHeading: string;
      conditionRoutingEmpty: string;
    };

  },
  graphLegend: {
    heading: {
      nodeStatus: string;
      edgeType: string;
    };
    aria: {
      root: string;
      nodeStatus: string;
      edgeType: string;
    };
    edgeKind: {
      nextTraversed: string;
      nextNotTraversed: string;
    };

  },
  executionHeader: {
    placeholderExecutionId: string;
    executionIdLabel: (executionLabel: string) => string;
    compareLabel: string;
    realtimeSseLabel: string;
    cancelRequestedLabel: string;
    graphIdLine: (graphIdLabel: string, graphId: string) => string;
    cancelRequestedLine: (cancelRequestedLabel: string, cancelRequested: boolean) => string;

  },
  executionsPage: {
    pagination: {
      ariaLabel: string;
      currentPage: (page: number) => string;
      prev: string;
      next: string;
    };
    filter: {
      contextActivePrefix: string;
      clearDefinition: string;
      title: string;
      all: string;
      statusRunning: string;
      statusCompleted: string;
      statusCancelled: string;
      statusFailed: string;
      definitionInputHint: string;
      definitionLabelWithHint: (definitionLabel: string) => string;
      definitionPlaceholder: string;
      nameInputHint: string;
      search: string;
      clear: string;
      sortByLabel: string;
      sortOrderLabel: string;
      sortByUpdatedAt: string;
      sortByDisplayId: string;
      sortOrderDesc: string;
      sortOrderAsc: string;
      invalidName: string;
      invalidDefinitionId: string;
      pageInfo: (limit: number, offset: number, page: number) => string;
    };
    loading: string;
    listSummary: (totalCount: number, page: number) => string;
    updatedAt: (formattedDateTime: string) => string;
    actions: {
      openDetail: string;
    };
    empty: string;
    error: string;

  },
  executionDetailPage: {
    title: string;
    missingExecutionId: string;
    navRun: string;
    navGraph: string;

  },
  executionGraphPage: {
    title: string;
    missingExecutionId: string;
    navDetail: string;
    navRun: string;

  },
  executionRunPage: {
    title: string;
    missingExecutionId: string;
    navDetail: string;
    navGraph: string;

  },
  executionStatusBanner: {
    cancelRequestedNotice: (cancelLabel: string, resumeLabel: string) => string;
    terminalNotice: (executionLabel: string) => string;

  },
  nodeList: {
    title: string;
    nodeCount: (count: number) => string;
    columns: {
      status: string;
      type: string;
      nodeName: string;
      executionNodeId: string;
      duration: string;
    };

  },
  nodeGraph: {
    meta: {
      type: (nodeType: string) => string;
      attempt: (attempt: number) => string;
      waitKey: (waitKey: string) => string;
    };
    aria: {
      /** グラフノードを選択（ドラッグ可能なブロック用。内側に実 button があるため外側は div） */
      selectNode: (displayLabel: string) => string;
    };

  },
  nodeCommands: {
    resumeDisabledReason: {
      runOnly: string;
      executionNotLoaded: string;
      nodeNotSelected: string;
      executionTerminal: string;
      cancelRequested: string;
      waitingOnly: string;
    };

  };
};
