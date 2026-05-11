export type UiText = {
  actionLinks: {
    aria: {
      navigation: string;
    };
  };
  navigation: {
    dashboard: string;
    definitions: string;
    workflows: string;
    health: string;
  };
  entities: {
    workflow: string;
    definition: string;
    execution: string;
    node: string;
  };
  lists: {
    workflows: string;
    definitions: string;
    nodeCount: (count: number) => string;
  };
  actions: {
    load: string;
    loading: string;
    reload: string;
    cancel: string;
    resume: string;
    retry: string;
    save: string;
    sendEvent: string;
    openDetail: string;
    closeToast: string;
    viewList: string;
    viewGraph: string;
  };
  pagination: {
    prev: string;
    next: string;
  };
  labels: {
    status: string;
    nodeId: string;
    definitionId: string;
    displayId: string;
    resourceId: string;
    graphId: string;
    workflowInput: string;
    definitionEditor: string;
  };
  status: {
    cancelledDisplay: string;
    edgeTypeNext: string;
    edgeTypeResume: string;
    edgeTypeCancel: string;
  };
  pageState: {
    loading: string;
    empty: string;
    error: string;
  };
  errorPrefixes: {
    unauthorized401: string;
    forbidden403: string;
    conflict409: string;
    unprocessable422: string;
    server500: string;
  };
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
      workflowNotFound: string;
    };
    toasts: {
      cancelAccepted: string;
      publishAccepted: string;
      resumeAccepted: string;
    };
    replayDisabledReason: string;
    operationsAggregatedInRun: (cancelLabel: string, resumeLabel: string, sendEventLabel: string) => string;
  };
  executionTimeline: {
    title: string;
    backToCurrent: string;
    replayingPastStateMessage: string;
    empty: string;
    loadMore: string;
    errorUnknown: string;
  };
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
  };
  nodeDetail: {
    prompts: {
      loadExecution: (executionLabel: string) => string;
      selectNode: (nodeLabel: string) => string;
    };
    title: (nodeLabel: string) => string;
    meta: {
      type: (nodeType: string) => string;
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
      conditionRoutingHeading: string;
      conditionRoutingEmpty: string;
    };
  };
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
  };
  dashboard: {
    title: string;
    descriptionRecent: string;
    loadingRecent: string;
    emptyStartFromDefinitionsOrWorkflows: string;
    totalCount: (count: number | null) => string;
    updatedAt: (formattedDateTime: string) => string;
    aria: {
      recentWorkflowsList: string;
    };
    actions: {
      openDetail: string;
    };
    error: {
      fetchFailed: string;
    };
  };
  definitionRunPage: {
    title: string;
    unspecifiedDefinitionId: string;
    definitionIdLine: (definitionIdLabel: string, definitionId: string) => string;
    workflowInputLabelWithHint: (workflowInputLabel: string) => string;
    inputJsonPlaceholder: string;
    toasts: {
      definitionIdRequired: (definitionIdLabel: string) => string;
      invalidWorkflowInputJson: (workflowInputLabel: string) => string;
      workflowInputTooLarge: string;
      workflowStarted: (workflowDisplayId: string) => string;
    };
    nav: {
      backToDefinitionDetail: string;
    };
    actions: {
      starting: string;
      startWorkflow: string;
    };
    help: {
      redirectAfterStart: (runPath: string) => string;
    };
  };
  executionHeader: {
    placeholderExecutionId: string;
    executionIdLabel: (executionLabel: string) => string;
    compareLabel: string;
    realtimeSseLabel: string;
    cancelRequestedLabel: string;
    graphIdLine: (graphIdLabel: string, graphId: string) => string;
    cancelRequestedLine: (cancelRequestedLabel: string, cancelRequested: boolean) => string;
  };
  workflowsPage: {
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
  };
  definitionsPage: {
    description: string;
    pagination: {
      ariaLabel: string;
      currentPage: (page: number) => string;
    };
    search: {
      label: string;
      placeholder: string;
      submit: string;
      clear: string;
      invalidName: string;
    };
    sortByLabel: string;
    sortOrderLabel: string;
    sortByCreatedAt: string;
    sortByName: string;
    sortOrderDesc: string;
    sortOrderAsc: string;
    loading: string;
    emptyNoMatch: string;
    searchSummaryPrefix: (keyword: string) => string;
    listSummary: (totalCount: number, page: number) => string;
    createdAt: (formattedDateTime: string) => string;
    displayIdAndCreatedAt: (displayIdLabel: string, displayId: string, createdAtLabel: string) => string;
    actions: {
      openDetail: string;
      createNew: string;
    };
    error: string;
  };
  workflowDetailPage: {
    title: string;
    missingWorkflowId: string;
    navRun: string;
    navGraph: string;
  };
  workflowGraphPage: {
    title: string;
    missingWorkflowId: string;
    navDetail: string;
    navRun: string;
  };
  workflowRunPage: {
    title: string;
    missingWorkflowId: string;
    navDetail: string;
    navGraph: string;
  };
  definitionDetail: {
    title: string;
    urlPrefix: string;
    errorFetchFailed: string;
    ariaMeta: string;
    meta: {
      name: string;
      createdAt: string;
    };
    relatedWorkflows: {
      title: string;
      description: string;
      openList: string;
    };
    actions: {
      title: string;
      edit: string;
      run: string;
    };
    nav: {
      backToDefinitions: string;
    };
  };
  definitionEditor: {
    backToDetail: string;
    descriptionCreating: string;
    descriptionEditingTarget: (definitionId: string) => string;
    loadingMeta: string;
    validation: {
      nameRequired: string;
      yamlRequired: string;
      yamlLintInvalid: string;
      nameInvalidFormat: string;
      yamlTooLarge: string;
    };
    labels: {
      name: string;
      yaml: string;
    };
    actions: {
      saving: string;
      saveWithApiHint: string;
      resetTemplate: string;
      switchToYaml: string;
      switchToGraph: string;
    };
    graph: {
      title: string;
      empty: string;
      addNode: string;
      addNodeDialogTitle: string;
      addNodeDisabledReasonStart: string;
      addNodeDisabledReasonEnd: string;
      nodeInspectorTitle: string;
      edgeInspectorTitle: string;
      deleteNode: string;
      deleteEdge: string;
      apply: string;
      closeDialog: string;
      rootObjectRequired: () => string;
      nodesArrayRequired: () => string;
      nodesRequired: () => string;
      nodeIdRequired: () => string;
      duplicateNodeId: (nodeId: string) => string;
      startCountInvalid: (count: number) => string;
      endCountInvalid: (count: number) => string;
      startRequiresTransition: (nodeId: string) => string;
      actionRequired: (nodeId: string) => string;
      actionRequiresTransition: (nodeId: string) => string;
      waitEventRequired: (nodeId: string) => string;
      waitRequiresTransition: (nodeId: string) => string;
      forkBranchesRequired: (nodeId: string) => string;
      joinRequiresTransition: (nodeId: string) => string;
      joinModeInvalid: (nodeId: string) => string;
      endCannotHaveTransition: (nodeId: string) => string;
      edgeToRequired: (nodeId: string) => string;
      edgeWhenPathRequired: (nodeId: string) => string;
      edgeWhenOpRequired: (nodeId: string) => string;
      edgeWhenValueRequired: (nodeId: string) => string;
      edgeWhenValueInInvalid: (nodeId: string) => string;
      edgeWhenValueBetweenInvalid: (nodeId: string) => string;
      edgeDefaultMultiple: (nodeId: string) => string;
      selfReferenceEdge: (nodeId: string) => string;
      missingTargetNode: (nodeId: string, targetId: string) => string;
      selfReferenceRejected: string;
      whenOpPlaceholder: string;
      whenPathPlaceholder: string;
      whenValuePlaceholder: string;
      whenValueDisabledForExists: string;
      whenValueHintIn: string;
      whenValueHintBetween: string;
      fullscreenEnter: string;
      fullscreenExit: string;
      parseFailed: string;
      actionErrorLabel: string;
      actionInputLabel: string;
      actionInputPlaceholder: string;
      actionInputHint: string;
      actionInputInvalidJson: string;
    };
    saved: {
      completePrefix: string;
      complete: (displayId: string) => string;
      openNewDetail: string;
      runWithThisDefinition: string;
    };
    toasts: {
      savedWithDisplayId: (displayIdLabel: string, displayId: string) => string;
    };
    hints: {
      title: string;
    };
  };
  tenantMissingBanner: {
    noticeParts: (
      loadLabel: string,
      cancelLabel: string,
      resumeLabel: string
    ) => {
      beforePrimaryEnv: string;
      betweenEnvs: string;
      afterSecondaryEnv: string;
    };
  };
  executionStatusBanner: {
    cancelRequestedNotice: (cancelLabel: string, resumeLabel: string) => string;
    terminalNotice: (executionLabel: string) => string;
  };
  nodeList: {
    title: string;
    nodeCount: (count: number) => string;
    columns: {
      nodeId: string;
      type: string;
      status: string;
      waitKey: string;
    };
  };
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
  };
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

export const uiText: UiText = {
  actionLinks: {
    aria: {
      navigation: "画面導線",
    },
  },
  navigation: {
    dashboard: "ダッシュボード",
    definitions: "定義",
    workflows: "ワークフロー",
    health: "ヘルスチェック",
  },
  entities: {
    workflow: "ワークフロー",
    definition: "定義",
    execution: "実行",
    node: "ノード",
  },
  lists: {
    workflows: "ワークフロー一覧",
    definitions: "定義一覧",
    nodeCount: (count: number) => `${count} 件`,
  },
  actions: {
    load: "ロード",
    loading: "ローディング...",
    reload: "リロード",
    cancel: "キャンセル",
    resume: "再開",
    retry: "再試行",
    save: "保存",
    sendEvent: "イベント送信",
    openDetail: "詳細",
    closeToast: "通知を閉じる",
    viewList: "リスト",
    viewGraph: "グラフ",
  },
  pagination: {
    prev: "前へ",
    next: "次へ",
  },
  labels: {
    status: "ステータス",
    nodeId: "ノードID",
    definitionId: "定義ID",
    displayId: "表示ID",
    resourceId: "リソースID",
    graphId: "グラフID",
    workflowInput: "入力データ",
    definitionEditor: "定義エディタ",
  },
  status: {
    // 表示上は Cancelled に統一。内部状態値は既存仕様のまま扱う。
    cancelledDisplay: "Cancelled",
    edgeTypeNext: "Next",
    edgeTypeResume: "Resume",
    edgeTypeCancel: "Cancel",
  },
  pageState: {
    loading: "ローディング...",
    empty: "表示できるデータがありません。",
    error: "データの取得に失敗しました。",
  },
  errorPrefixes: {
    unauthorized401: "401 認証が必要です",
    forbidden403: "403 権限不足またはテナント未指定",
    conflict409: "409 状態競合",
    unprocessable422: "422 入力不正",
    server500: "500 サーバーエラー",
  },
  executionDashboard: {
    header: {
      titleDefault: "実行の詳細",
    },
    actions: {
      sectionTitle: "実行操作",
      eventNameLabel: "イベント名",
      eventNamePlaceholder: "event-name",
    },
    validation: {
      eventNameTooLong: "イベント名は64文字以内で入力してください。",
      eventNameInvalidFormat: "イベント名は半角英字開始で、半角英数字と . - _ のみ利用できます。",
    },
    graph: {
      fullscreenEnter: "全画面表示",
      fullscreenExit: "全画面終了 (Esc)",
      definitionMissingFallback: (graphId: string) =>
        `グラフID: ${graphId} の定義が未登録のため、仮エッジ表示です。`,
    },
    errors: {
      workflowNotFound: "指定されたワークフローが見つかりませんでした。ID を確認してください。",
    },
    toasts: {
      cancelAccepted: "CancelExecution accepted",
      publishAccepted: "PublishEvent accepted",
      resumeAccepted: "ResumeNode accepted",
    },
    replayDisabledReason: "リプレイ表示中は実行できません",
    operationsAggregatedInRun: (cancelLabel: string, resumeLabel: string, sendEventLabel: string) =>
      `${cancelLabel} / ${resumeLabel} / ${sendEventLabel} は Run 画面に集約しています。`,
  },
  executionTimeline: {
    title: "イベントタイムライン",
    backToCurrent: "現在に戻る",
    replayingPastStateMessage: "過去の時点を表示中です。「現在に戻る」で最新の状態に戻せます。",
    empty: "イベントがありません",
    loadMore: "続きを読み込む",
    errorUnknown: "不明なエラーが発生しました。",
  },
  executionComparison: {
    title: "2実行の比較",
    executionIdPlaceholder: "ex-2",
    executionABaselineLabel: (executionLabel: string) => `${executionLabel} A（基準）`,
    executionBLabel: (executionLabel: string) => `${executionLabel} B`,
    kind: {
      onlyLeft: "A のみ",
      onlyRight: "B のみ",
      diff: "差分",
    },
    state: {
      notLoaded: "未読み込み",
    },
    summary: {
      title: "差分サマリ",
      failedOrCancelled: "失敗 / キャンセル",
      others: "その他",
      noDiff: "ノード差分なし",
      loadBothToShow: "A と B を読み込むと表示されます",
    },
  },
  nodeDetail: {
    prompts: {
      loadExecution: (executionLabel: string) => `${executionLabel} を読み込んでください。`,
      selectNode: (nodeLabel: string) => `${nodeLabel} を選択してください。`,
    },
    title: (_nodeLabel: string) => "ノード詳細",
    meta: {
      type: (nodeType: string) => `種別: ${nodeType}`,
      attempt: (attempt: number) => `試行回数: ${attempt}`,
      waitKey: (waitKey: string) => `Wait キー: ${waitKey}`,
      canceledByExecution: (canceledByExecution: boolean) => `キャンセル: ${String(canceledByExecution)}`,
    },
    waiting: {
      title: "待機中 (Wait)",
      reasonWaitByWaitKeyAndResumeWait: "理由: Wait キー により 再開 待ち",
      resumeEventName: (eventName: string) => `再開 イベント名: ${eventName}`,
    },
    cancel: {
      detailTitle: (cancelLabel: string) => `${cancelLabel} 詳細`,
      convergedByExecutionCancel: "実行 キャンセル により収束",
    },
    failure: {
      title: "失敗情報",
      noMessage: "（メッセージなし）",
    },
    trace: {
      startedAt: (formattedInstant: string) => `開始: ${formattedInstant}`,
      completedAt: (formattedInstant: string) => `終了: ${formattedInstant}`,
      duration: (durationText: string) => `実行時間: ${durationText}`,
      durationUnavailable: "実行時間: —",
      outputHeading: "出力",
      outputEmpty: "（なし）",
      conditionRoutingHeading: "条件ルーティング",
      conditionRoutingEmpty: "（なし）",
    },
  },
  graphLegend: {
    heading: {
      nodeStatus: "ノードステータス",
      edgeType: "エッジ種別",
    },
    aria: {
      root: "グラフ凡例",
      nodeStatus: "ノードステータス凡例",
      edgeType: "エッジ種別凡例",
    },
    edgeKind: {
      nextTraversed: "Next（実行経路）",
      nextNotTraversed: "Next（未通過）",
    },
  },
  dashboard: {
    title: "ダッシュボード",
    descriptionRecent: "直近のワークフロー（最大 10 件）です。",
    loadingRecent: "直近のワークフローを取得しています。",
    emptyStartFromDefinitionsOrWorkflows: "定義一覧またはワークフロー一覧から操作を開始できます。",
    totalCount: (count: number | null) => (count == null ? "合計件数: --" : `合計件数: ${count}`),
    updatedAt: (formattedDateTime: string) => `更新: ${formattedDateTime}`,
    aria: {
      recentWorkflowsList: "直近ワークフロー一覧",
    },
    actions: {
      openDetail: "詳細を開く",
    },
    error: {
      fetchFailed: "データを取得できませんでした。",
    },
  },
  definitionRunPage: {
    title: "定義を実行",
    unspecifiedDefinitionId: "（未指定）",
    definitionIdLine: (definitionIdLabel: string, definitionId: string) => `${definitionIdLabel}: ${definitionId}`,
    workflowInputLabelWithHint: (workflowInputLabel: string) => `${workflowInputLabel}（任意・JSON）`,
    inputJsonPlaceholder: '例: {"orderId":"123"}',
    toasts: {
      definitionIdRequired: (definitionIdLabel: string) => `${definitionIdLabel} が指定されていません。`,
      invalidWorkflowInputJson: (workflowInputLabel: string) => `${workflowInputLabel} の JSON が不正です。`,
      workflowInputTooLarge: "入力データは65536バイト（64KiB）以内で指定してください。",
      workflowStarted: (workflowDisplayId: string) => `ワークフローを開始しました: ${workflowDisplayId}`,
    },
    nav: {
      backToDefinitionDetail: "定義の詳細へ戻る",
    },
    actions: {
      starting: "開始中...",
      startWorkflow: "ワークフロー開始",
    },
    help: {
      redirectAfterStart: (runPath: string) => `開始後は実行画面（${runPath}）へ自動遷移します。`,
    },
  },
  executionHeader: {
    placeholderExecutionId: "ex-1",
    executionIdLabel: (executionLabel: string) => `${executionLabel} ID`,
    compareLabel: "比較",
    realtimeSseLabel: "リアルタイム更新",
    cancelRequestedLabel: "キャンセル要求",
    graphIdLine: (graphIdLabel: string, graphId: string) => `${graphIdLabel}: ${graphId}`,
    cancelRequestedLine: (cancelRequestedLabel: string, cancelRequested: boolean) =>
      `${cancelRequestedLabel}: ${cancelRequested ? "あり" : "なし"}`,
  },
  workflowsPage: {
    pagination: {
      ariaLabel: "ワークフロー一覧ページネーション",
      currentPage: (page: number) => `${page} ページ目`,
      prev: "前へ",
      next: "次へ",
    },
    filter: {
      contextActivePrefix: "定義文脈（フィルタ中）:",
      clearDefinition: "定義条件を外す",
      title: "フィルタ",
      all: "（すべて）",
      statusRunning: "Running",
      statusCompleted: "Completed",
      statusCancelled: "Cancelled",
      statusFailed: "Failed",
      definitionInputHint: "定義 表示ID / UUID",
      definitionLabelWithHint: (definitionLabel: string) => `${definitionLabel}（定義 表示ID / UUID）`,
      definitionPlaceholder: "例: def-…",
      nameInputHint: "name（workflow 表示ID 部分一致、または workflow UUID 完全一致）",
      search: "検索",
      clear: "クリア",
      sortByLabel: "ソート項目",
      sortOrderLabel: "順序",
      sortByUpdatedAt: "更新日時",
      sortByDisplayId: "表示ID",
      sortOrderDesc: "降順",
      sortOrderAsc: "昇順",
      invalidName: "name は半角英数字と . - _ のみ、100文字以内で入力してください。",
      invalidDefinitionId: "定義IDは半角英数字と - _ のみ、80文字以内で入力してください。",
      pageInfo: (limit: number, offset: number, page: number) =>
        `1 ページあたり: ${limit} 件。 offset: ${offset}（page ≈ ${page}）`,
    },
    loading: "ワークフロー一覧を読み込み中です。",
    listSummary: (totalCount: number, page: number) => `合計 ${totalCount} 件（${page} ページ目）`,
    updatedAt: (formattedDateTime: string) => `更新: ${formattedDateTime}`,
    actions: {
      openDetail: "詳細",
    },
    empty: "条件に合うワークフローはありません。",
    error: "取得に失敗しました。時間をおいて再試行してください。",
  },
  definitionsPage: {
    description: "定義の検索とページングを行い、詳細画面へ遷移します。",
    pagination: {
      ariaLabel: "定義一覧ページネーション",
      currentPage: (page: number) => `${page} ページ目`,
    },
    search: {
      label: "名前検索（部分一致）",
      placeholder: "例: order",
      submit: "検索",
      clear: "クリア",
      invalidName: "検索キーワードは半角英数字と . - _ のみ、100文字以内で入力してください。",
    },
    sortByLabel: "ソート項目",
    sortOrderLabel: "順序",
    sortByCreatedAt: "作成日時",
    sortByName: "名前",
    sortOrderDesc: "降順",
    sortOrderAsc: "昇順",
    loading: "定義一覧を読み込み中です。",
    emptyNoMatch: "該当する定義はありません。検索条件を変更するか、条件をクリアして再検索してください。",
    searchSummaryPrefix: (keyword: string) => `検索: "${keyword}" / `,
    listSummary: (totalCount: number, page: number) => `合計 ${totalCount} 件（${page} ページ目）`,
    createdAt: (formattedDateTime: string) => `作成: ${formattedDateTime}`,
    displayIdAndCreatedAt: (displayIdLabel: string, displayId: string, createdAtLabel: string) =>
      `${displayIdLabel}: ${displayId} / ${createdAtLabel}`,
    actions: {
      openDetail: "詳細を開く",
      createNew: "新しい定義を作成",
    },
    error: "定義一覧を取得できませんでした。",
  },
  workflowDetailPage: {
    title: "ワークフロー詳細",
    missingWorkflowId: "ワークフロー ID が指定されていません。",
    navRun: "実行",
    navGraph: "グラフ",
  },
  workflowGraphPage: {
    title: "ワークフローグラフ",
    missingWorkflowId: "ワークフロー ID が指定されていません。",
    navDetail: "詳細",
    navRun: "実行",
  },
  workflowRunPage: {
    title: "ワークフロー実行",
    missingWorkflowId: "ワークフロー ID が指定されていません。",
    navDetail: "詳細",
    navGraph: "グラフ",
  },
  definitionDetail: {
    title: "定義 詳細",
    urlPrefix: "URL:",
    errorFetchFailed: "定義を取得できませんでした。",
    ariaMeta: "定義メタ情報",
    meta: {
      name: "名前",
      createdAt: "登録日時",
    },
    relatedWorkflows: {
      title: "関連ワークフロー",
      description: "この定義に紐づく実行の一覧へ進みます。",
      openList: "ワークフロー一覧を開く",
    },
    actions: {
      title: "編集・実行",
      edit: "定義の編集",
      run: "新規実行を開始",
    },
    nav: {
      backToDefinitions: "定義一覧へ戻る",
    },
  },
  definitionEditor: {
    backToDetail: "定義の詳細へ戻る",
    descriptionCreating: "新しい定義を作成します。",
    descriptionEditingTarget: (definitionId: string) => `編集対象: ${definitionId}`,
    loadingMeta: "定義メタ情報を読み込み中...",
    validation: {
      nameRequired: "定義名を入力してください。",
      yamlRequired: "YAML を入力してください。",
      yamlLintInvalid: "YAML の構文エラーを修正してください。",
      nameInvalidFormat: "定義名は半角英字で開始し、半角英数字と . - _ のみ100文字以内で入力してください。",
      yamlTooLarge: "YAMLは256KB（262144バイト）以内で入力してください。",
    },
    labels: {
      name: "定義名（name）",
      yaml: "YAML",
    },
    actions: {
      saving: "保存中...",
      saveWithApiHint: "保存",
      resetTemplate: "編集前に戻す",
      switchToYaml: "YAML",
      switchToGraph: "Graph",
    },
    graph: {
      title: "グラフ編集",
      empty: "YAML のパースに失敗したためグラフを表示できません。YAML を修正してください。",
      addNode: "ノード追加",
      addNodeDialogTitle: "ノードを追加",
      addNodeDisabledReasonStart: "start は1つまで追加できます。",
      addNodeDisabledReasonEnd: "end は1つまで追加できます。",
      nodeInspectorTitle: "ノード編集",
      edgeInspectorTitle: "エッジ編集",
      deleteNode: "ノード削除",
      deleteEdge: "エッジ削除",
      apply: "反映",
      closeDialog: "閉じる",
      rootObjectRequired: () => "YAML ルートはオブジェクトである必要があります。",
      nodesArrayRequired: () => "nodes は配列で指定してください。",
      nodesRequired: () => "nodes は1件以上必要です。",
      nodeIdRequired: () => "node.id は必須です。",
      duplicateNodeId: (nodeId: string) => `重複 node.id: '${nodeId}'`,
      startCountInvalid: (count: number) => `start ノードは1件のみ許可されます（現在 ${count} 件）。`,
      endCountInvalid: (count: number) => `end ノードは1件のみ許可されます（現在 ${count} 件）。`,
      startRequiresTransition: (nodeId: string) => `Node '${nodeId}': start は next または edges が必要です。`,
      actionRequired: (nodeId: string) => `Node '${nodeId}': action ノードは action が必須です。`,
      actionRequiresTransition: (nodeId: string) => `Node '${nodeId}': action は next または edges が必要です。`,
      waitEventRequired: (nodeId: string) => `Node '${nodeId}': wait ノードは event が必須です。`,
      waitRequiresTransition: (nodeId: string) => `Node '${nodeId}': wait は next または edges が必要です。`,
      forkBranchesRequired: (nodeId: string) => `Node '${nodeId}': fork は branches を2件以上指定してください。`,
      joinRequiresTransition: (nodeId: string) => `Node '${nodeId}': join は next または edges が必要です。`,
      joinModeInvalid: (nodeId: string) => `Node '${nodeId}': join.mode は 'all' のみ許可されます。`,
      endCannotHaveTransition: (nodeId: string) => `Node '${nodeId}': end は next/edges を持てません。`,
      edgeToRequired: (nodeId: string) => `Node '${nodeId}': edge.to は必須です。`,
      edgeWhenPathRequired: (nodeId: string) => `Node '${nodeId}': edge.when.path は必須です。`,
      edgeWhenOpRequired: (nodeId: string) => `Node '${nodeId}': edge.when.op は必須です。`,
      edgeWhenValueRequired: (nodeId: string) =>
        `Node '${nodeId}': この演算子では edge.when.value が必須です（EXISTS 以外）。`,
      edgeWhenValueInInvalid: (nodeId: string) =>
        `Node '${nodeId}': IN では edge.when.value は空でない配列（または JSON 配列文字列）が必要です。`,
      edgeWhenValueBetweenInvalid: (nodeId: string) =>
        `Node '${nodeId}': BETWEEN では edge.when.value は要素2件以上の配列（または JSON 配列文字列）が必要です。`,
      edgeDefaultMultiple: (nodeId: string) =>
        `Node '${nodeId}': default=true の edge は 1 つまでです。`,
      selfReferenceEdge: (nodeId: string) => `Node '${nodeId}': 自己参照エッジは許可されません。`,
      missingTargetNode: (nodeId: string, targetId: string) => `Node '${nodeId}': 参照先 '${targetId}' が存在しません。`,
      selfReferenceRejected: "自己参照エッジは作成できません。",
      whenOpPlaceholder: "演算子を選択",
      whenPathPlaceholder: "$.payload.amount",
      whenValuePlaceholder: "例: 100 / true / \"100\"",
      whenValueDisabledForExists: "EXISTS では value は不要です。",
      whenValueHintIn: "IN は JSON配列で入力します。例: [\"A\", \"B\"]",
      whenValueHintBetween: "BETWEEN は [最小, 最大] の JSON配列で入力します。例: [1, 10]",
      fullscreenEnter: "全画面表示",
      fullscreenExit: "全画面終了",
      parseFailed: "YAML のパースに失敗したため、直前の有効グラフを表示しています。",
      actionErrorLabel: "error 遷移先（任意）",
      actionInputLabel: "input（任意）",
      actionInputPlaceholder: '例: $.input.orderId または {"orderId":"$.input.orderId"}',
      actionInputHint: "パスは $ / $. のみ。オブジェクトは JSON で入力。",
      actionInputInvalidJson: "JSON の形式が正しくありません。",
    },
    saved: {
      completePrefix: "保存完了:",
      complete: (displayId: string) => `保存完了: ${displayId}`,
      openNewDetail: "新しい定義の詳細へ",
      runWithThisDefinition: "この定義で実行開始",
    },
    toasts: {
      savedWithDisplayId: (displayIdLabel: string, displayId: string) =>
        `定義を保存しました（${displayIdLabel}: ${displayId}）`,
    },
    hints: {
      title: "修正ヒント",
    },
  },
  tenantMissingBanner: {
    noticeParts: (loadLabel: string, cancelLabel: string, resumeLabel: string) => ({
      beforePrimaryEnv: `テナントが未指定です。${loadLabel} / ${cancelLabel} / ${resumeLabel} が失敗する場合は `,
      betweenEnvs: " を設定するか、サーバーで ",
      afterSecondaryEnv: " を設定してください。",
    }),
  },
  executionStatusBanner: {
    cancelRequestedNotice: (cancelLabel: string, resumeLabel: string) =>
      `${cancelLabel}要求済みのため、${resumeLabel}など進行系操作はできません`,
    terminalNotice: (executionLabel: string) => `${executionLabel}は終了しています`,
  },
  nodeList: {
    title: "ノード一覧",
    nodeCount: (count: number) => `${count} 件`,
    columns: {
      nodeId: "ノードID",
      type: "種別",
      status: "ステータス",
      waitKey: "Wait キー",
    },
  },
  nodeGraph: {
    meta: {
      type: (nodeType: string) => `種別: ${nodeType}`,
      attempt: (attempt: number) => `試行回数: ${attempt}`,
      waitKey: (waitKey: string) => `Wait キー: ${waitKey}`,
    },
    aria: {
      selectNode: (displayLabel: string) => `ノードを選択: ${displayLabel}`,
    },
  },
  nodeCommands: {
    resumeDisabledReason: {
      runOnly: "Run 画面でのみ Resume できます",
      executionNotLoaded: "Execution が未読込です",
      nodeNotSelected: "Node を選択してください",
      executionTerminal: "Executionは終了しています",
      cancelRequested: "Cancel要求済みのため、Resumeなど進行系操作はできません",
      waitingOnly: "WAITING 状態のノードのみ Resume できます",
    },
  },
};

export const uiTextJa: UiText = uiText;

export type MappingEntry = {
  source: string;
  target: string;
  note: string;
};

/**
 * design.md の Confirmed Mapping Table と1:1で対応するトレーサビリティ用一覧。
 * T2 実装時に、各 source がどのキーへマッピングされるかの確認に利用する。
 */
export const confirmedMappingTable: MappingEntry[] = [
  { source: "Workflow", target: uiText.entities.workflow, note: "画面表示は日本語へ統一（内部識別子は変更しない）" },
  { source: "Definition", target: uiText.entities.definition, note: "一覧/詳細/説明文で統一" },
  { source: "Execution", target: uiText.entities.execution, note: "見出し・ラベルで統一" },
  { source: "Workflow 一覧", target: uiText.lists.workflows, note: "一覧名の英日混在を解消" },
  { source: "Definition 一覧", target: uiText.lists.definitions, note: "一覧名の英日混在を解消" },
  { source: "Load", target: uiText.actions.load, note: "操作語をカタカナに統一" },
  { source: "Loading... / 読み込み中...", target: uiText.actions.loading, note: "読み込み状態文言を統一" },
  { source: "Cancel", target: uiText.actions.cancel, note: "操作語を統一" },
  { source: "Resume", target: uiText.actions.resume, note: "操作語を統一" },
  { source: "Event 送信", target: uiText.actions.sendEvent, note: "和英混在を解消" },
  { source: "Cancelled / Canceled / CANCELED", target: uiText.status.cancelledDisplay, note: "表示用語を1つに固定（値変換は別管理）" },
  { source: "Nodes / {n} nodes", target: "ノード / {n} 件", note: "一覧系ラベルを日本語化" },
  { source: "nodeId", target: uiText.labels.nodeId, note: "IDラベル統一" },
  { source: "status", target: uiText.labels.status, note: "項目ラベル統一" },
  { source: "definitionId", target: uiText.labels.definitionId, note: "ユーザー向けラベル統一" },
  { source: "displayId", target: uiText.labels.displayId, note: "ユーザー向けIDラベルを統一" },
  { source: "graphId", target: uiText.labels.graphId, note: "ユーザー向けラベル統一" },
  { source: "close toast", target: uiText.actions.closeToast, note: "アクセシビリティラベルを日本語化" },
  { source: "List / Graph", target: `${uiText.actions.viewList} / ${uiText.actions.viewGraph}`, note: "表示切替文言を日本語化" },
  { source: "workflow input", target: uiText.labels.workflowInput, note: "入力欄ラベルを日本語化" },
  { source: "Definition Editor", target: uiText.labels.definitionEditor, note: "画面タイトルを日本語化" },
  { source: "health", target: uiText.navigation.health, note: "ナビゲーション文言を日本語化" },
  { source: "再読み込み", target: uiText.actions.reload, note: "再取得操作の表記を統一" },
];
