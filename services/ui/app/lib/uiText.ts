export type UiText = {
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
  labels: {
    status: string;
    nodeId: string;
    definitionId: string;
    displayId: string;
    graphId: string;
    workflowInput: string;
    definitionEditor: string;
  };
  status: {
    cancelledDisplay: string;
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
};

export const uiText: UiText = {
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
  labels: {
    status: "ステータス",
    nodeId: "ノードID",
    definitionId: "定義ID",
    displayId: "表示ID",
    graphId: "グラフID",
    workflowInput: "入力データ",
    definitionEditor: "定義エディタ",
  },
  status: {
    // 表示上は Cancelled に統一。内部状態値は既存仕様のまま扱う。
    cancelledDisplay: "Cancelled",
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
};

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
