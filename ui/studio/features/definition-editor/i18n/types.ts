/**
 * definition-editor feature の i18n 切片型。
 * shared の UiText が合成時に取り込む。
 */
export type DefinitionEditorFeatureUiText = {
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
      waitEventsAndEventTogether: (nodeId: string) => string;
      waitEventsCannotHaveEdges: (nodeId: string) => string;
      waitEventTargetRequired: (nodeId: string, eventName: string) => string;
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
      whenPathHint: string;
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
      actionIdCandidatesLoading: string;
      actionIdNoResults: string;
      schemaPathPlaceholder: string;
      schemaLiteralOrPathPlaceholder: string;
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
};
