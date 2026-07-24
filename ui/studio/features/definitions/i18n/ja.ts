import type { DefinitionsFeatureUiText } from "./types";

/**
 * definitions feature の日本語辞書切片。
 */
export const definitionsUiTextJa: DefinitionsFeatureUiText = {
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
      delete: "削除",
      restore: "復元",
      confirmDelete: "削除する",
      confirmRestore: "復元する",
      cancelConfirm: "やめる",
      deleting: "削除中...",
      restoring: "復元中...",
    },
    includeDeleted: {
      label: "削除済みを含む",
    },
    deletedBadge: "削除済み",
    deletedAt: (formattedDateTime: string) => `削除: ${formattedDateTime}`,
    toasts: {
      deleted: "定義を削除しました。",
      restored: "定義を復元しました。",
    },
    error: "定義一覧を取得できませんでした。",
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
    relatedExecutions: {
      title: "関連実行",
      description: "この定義に紐づく実行の一覧へ進みます。",
      openList: "実行一覧を開く",
    },
    actions: {
      title: "編集・実行",
      edit: "定義の編集",
      run: "新規実行を開始",
      delete: "定義を削除",
      confirmDelete: "削除する",
      cancelConfirm: "やめる",
      deleting: "削除中...",
    },
    toasts: {
      deleted: "定義を削除しました。",
    },
    nav: {
      backToDefinitions: "定義一覧へ戻る",
    },
  },
  definitionRunPage: {
    title: "定義を実行",
    unspecifiedDefinitionId: "（未指定）",
    definitionIdLine: (definitionIdLabel: string, definitionId: string) => `${definitionIdLabel}: ${definitionId}`,
    inputLabelWithHint: (inputLabel: string) => `${inputLabel}（任意・JSON）`,
    inputJsonPlaceholder: '例: {"orderId":"123"}',
    toasts: {
      definitionIdRequired: (definitionIdLabel: string) => `${definitionIdLabel} が指定されていません。`,
      invalidInputJson: (inputLabel: string) => `${inputLabel} の JSON が不正です。`,
      inputTooLarge: "入力データは65536バイト（64KiB）以内で指定してください。",
      executionStarted: (executionDisplayId: string) => `実行を開始しました: ${executionDisplayId}`,
    },
    nav: {
      backToDefinitionDetail: "定義の詳細へ戻る",
    },
    actions: {
      starting: "開始中...",
      startExecution: "実行開始",
    },
    help: {
      redirectAfterStart: (runPath: string) => `開始後は実行画面（${runPath}）へ自動遷移します。`,
    },
  },
};
