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
      nodeNameRequired: () => string;
      duplicateNodeName: (nodeName: string) => string;
      startCountInvalid: (count: number) => string;
      endCountInvalid: (count: number) => string;
      startRequiresTransition: (nodeName: string) => string;
      actionRequired: (nodeName: string) => string;
      actionRequiresTransition: (nodeName: string) => string;
      waitEventRequired: (nodeName: string) => string;
      waitRequiresTransition: (nodeName: string) => string;
      waitEventsAndEventTogether: (nodeName: string) => string;
      waitEventsCannotHaveEdges: (nodeName: string) => string;
      waitEventTargetRequired: (nodeName: string, eventName: string) => string;
      forkBranchesRequired: (nodeName: string) => string;
      joinRequiresTransition: (nodeName: string) => string;
      joinModeInvalid: (nodeName: string) => string;
      endCannotHaveTransition: (nodeName: string) => string;
      edgeToRequired: (nodeName: string) => string;
      edgeWhenPathRequired: (nodeName: string) => string;
      edgeWhenOpRequired: (nodeName: string) => string;
      edgeWhenValueRequired: (nodeName: string) => string;
      edgeWhenValueInInvalid: (nodeName: string) => string;
      edgeWhenValueBetweenInvalid: (nodeName: string) => string;
      edgeDefaultMultiple: (nodeName: string) => string;
      selfReferenceEdge: (nodeName: string) => string;
      missingTargetNode: (nodeName: string, targetName: string) => string;
      forkRegionIngressFromOutside: (fromName: string, toName: string) => string;
      forkRegionEgressWithoutJoin: (fromName: string, toName: string, joinName: string) => string;
      forkRegionWaitTargetOutside: (nodeName: string, toName: string) => string;
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
      waitEventsSectionTitle: string;
      waitEventNameLabel: string;
      waitEventTargetLabel: string;
      waitEventsAdd: string;
      waitEventsRemove: string;
      waitLegacyEventLabel: string;
      waitConvertToEvents: string;
      waitEventsConflictHint: string;
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
