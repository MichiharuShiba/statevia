import type { DefinitionEditorFeatureUiText } from "./types";

/**
 * definition-editor feature の日本語辞書切片。
 */
export const definitionEditorUiTextJa: DefinitionEditorFeatureUiText = {
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
      nodeNameRequired: () => "node.name は必須です。",
      duplicateNodeName: (nodeName: string) => `重複 node.name: '${nodeName}'`,
      startCountInvalid: (count: number) => `start ノードは1件のみ許可されます（現在 ${count} 件）。`,
      endCountInvalid: (count: number) => `end ノードは1件のみ許可されます（現在 ${count} 件）。`,
      startRequiresTransition: (nodeName: string) => `Node '${nodeName}': start は next または edges が必要です。`,
      actionRequired: (nodeName: string) => `Node '${nodeName}': action ノードは action が必須です。`,
      actionRequiresTransition: (nodeName: string) => `Node '${nodeName}': action は next または edges が必要です。`,
      waitEventRequired: (nodeName: string) =>
        `Node '${nodeName}': wait ノードは events または event が必須です。`,
      waitRequiresTransition: (nodeName: string) =>
        `Node '${nodeName}': wait の旧形式（event）は next または edges が必要です。`,
      waitEventsAndEventTogether: (nodeName: string) =>
        `Node '${nodeName}': wait の events と event は併用できません。`,
      waitEventsCannotHaveEdges: (nodeName: string) =>
        `Node '${nodeName}': wait の events と edges は併用できません。遷移先は events に書いてください。`,
      waitEventTargetRequired: (nodeName: string, eventName: string) =>
        `Node '${nodeName}': events['${eventName}'] の遷移先が必須です。`,
      forkBranchesRequired: (nodeName: string) => `Node '${nodeName}': fork は branches を2件以上指定してください。`,
      joinRequiresTransition: (nodeName: string) => `Node '${nodeName}': join は next または edges が必要です。`,
      joinModeInvalid: (nodeName: string) => `Node '${nodeName}': join.mode は 'all' のみ許可されます。`,
      endCannotHaveTransition: (nodeName: string) => `Node '${nodeName}': end は next/edges を持てません。`,
      edgeToRequired: (nodeName: string) => `Node '${nodeName}': edge.to は必須です。`,
      edgeWhenPathRequired: (nodeName: string) => `Node '${nodeName}': edge.when.path は必須です。`,
      edgeWhenOpRequired: (nodeName: string) => `Node '${nodeName}': edge.when.op は必須です。`,
      edgeWhenValueRequired: (nodeName: string) =>
        `Node '${nodeName}': この演算子では edge.when.value が必須です（EXISTS 以外）。`,
      edgeWhenValueInInvalid: (nodeName: string) =>
        `Node '${nodeName}': IN では edge.when.value は空でない配列（または JSON 配列文字列）が必要です。`,
      edgeWhenValueBetweenInvalid: (nodeName: string) =>
        `Node '${nodeName}': BETWEEN では edge.when.value は要素2件以上の配列（または JSON 配列文字列）が必要です。`,
      edgeDefaultMultiple: (nodeName: string) =>
        `Node '${nodeName}': default=true の edge は 1 つまでです。`,
      selfReferenceEdge: (nodeName: string) => `Node '${nodeName}': 自己参照エッジは許可されません。`,
      missingTargetNode: (nodeName: string, targetName: string) => `Node '${nodeName}': 参照先 '${targetName}' が存在しません。`,
      selfReferenceRejected: "自己参照エッジは作成できません。",
      whenOpPlaceholder: "演算子を選択",
      whenPathPlaceholder: "$.states.Fetch.output.amount",
      whenPathHint:
        "評価根は Execution Context（input と同じ）。例: $.states.A.output.y / $.states['a.b'].output.z / $.vars.flag / $.sys.today",
      whenValuePlaceholder: "例: 100 / true / \"100\"",
      whenValueDisabledForExists: "EXISTS では value は不要です。",
      whenValueHintIn: "IN は JSON配列で入力します。例: [\"A\", \"B\"]",
      whenValueHintBetween: "BETWEEN は [最小, 最大] の JSON配列で入力します。例: [1, 10]",
      fullscreenEnter: "全画面表示",
      fullscreenExit: "全画面終了",
      parseFailed: "YAML のパースに失敗したため、直前の有効グラフを表示しています。",
      actionErrorLabel: "error 遷移先（任意）",
      actionInputLabel: "input（任意）",
      actionInputPlaceholder:
        '例: $.input.orderId / $.states.A.output.x / {"id":"$.states[\'a.b\'].output.id"}',
      actionInputHint:
        "評価根は Execution Context（$.input / $.states / $.vars / $.sys）。ドット付き Node ID は $.states['id'].output。オブジェクトは JSON。",
      actionInputInvalidJson: "JSON の形式が正しくありません。",
      actionIdCandidatesLoading: "Action 一覧を読み込み中…",
      actionIdNoResults: "一致する Action がありません",
      schemaPathPlaceholder: "$.input.x または $.states.A.output.y",
      schemaLiteralOrPathPlaceholder: "リテラルまたは $.input.x",
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
};
