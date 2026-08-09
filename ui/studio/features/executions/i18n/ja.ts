import type { ExecutionsFeatureUiText } from "./types";

/**
 * executions feature の日本語辞書切片。
 */
export const executionsUiTextJa: ExecutionsFeatureUiText = {
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
      executionNotFound: "指定された実行が見つかりませんでした。ID を確認してください。",
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
      type: (nodeType: string) => `タイプ: ${nodeType}`,
      nodeName: (nodeName: string) => `ノード名: ${nodeName}`,
      executionNodeId: (id: string) => `実行ノードID: ${id}`,
      workerId: (workerId: string) => `ワーカーID: ${workerId}`,
      attempt: (attempt: number) => `試行回数: ${attempt}`,
      waitKey: (waitKey: string) => `Wait キー: ${waitKey}`,
      allowedEvents: (events: string) => `受付イベント: ${events}`,
      canceledByExecution: (canceledByExecution: boolean) => `キャンセル: ${String(canceledByExecution)}`,
    },
    waiting: {
      title: "待機中 (Wait)",
      reasonWaitByWaitKeyAndResumeWait: "理由: Wait キー により 再開 待ち",
      resumeEventName: (eventName: string) => `再開 イベント名: ${eventName}`,
      selectResumeEvent: "再開するイベント",
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
      inputHeading: "入力",
      inputEmpty: "（なし）",
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
  executionsPage: {
    pagination: {
      ariaLabel: "実行一覧ページネーション",
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
      nameInputHint: "name（execution 表示ID 部分一致、または execution UUID 完全一致）",
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
    loading: "実行一覧を読み込み中です。",
    listSummary: (totalCount: number, page: number) => `合計 ${totalCount} 件（${page} ページ目）`,
    updatedAt: (formattedDateTime: string) => `更新: ${formattedDateTime}`,
    actions: {
      openDetail: "詳細",
    },
    empty: "条件に合う実行はありません。",
    error: "取得に失敗しました。時間をおいて再試行してください。",

  },
  executionDetailPage: {
    title: "実行詳細",
    missingExecutionId: "実行 ID が指定されていません。",
    navRun: "実行",
    navGraph: "グラフ",

  },
  executionGraphPage: {
    title: "実行グラフ",
    missingExecutionId: "実行 ID が指定されていません。",
    navDetail: "詳細",
    navRun: "実行",

  },
  executionRunPage: {
    title: "実行",
    missingExecutionId: "実行 ID が指定されていません。",
    navDetail: "詳細",
    navGraph: "グラフ",

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
      status: "ステータス",
      type: "タイプ",
      nodeName: "ノード名",
      executionNodeId: "実行ノードID",
      duration: "実行時間",
    },

  },
  nodeGraph: {
    meta: {
      type: (nodeType: string) => `タイプ: ${nodeType}`,
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
      noResumeEvent: "受付可能なイベントが無いため Resume できません",
    },

  },
};
