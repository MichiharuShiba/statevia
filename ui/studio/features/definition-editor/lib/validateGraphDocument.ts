import type {
  DefinitionGraphDocument,
  DefinitionGraphEdge,
  DefinitionGraphNode,
  ValidateGraphDocumentResult
} from "./types";
import { validateForkRegionLight } from "./validateForkRegionLight";

/** グラフ検証メッセージのオプション。 */
export type ValidateGraphDocumentMessageOptions = {
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
};

function collectConfiguredEdgeTargets(node: DefinitionGraphNode): string[] {
  return (node.edges ?? [])
    .map((edge) => edge.to?.trim())
    .filter((to): to is string => Boolean(to));
}

function collectForkBranchTargets(node: DefinitionGraphNode): string[] {
  if (node.type !== "fork" || !Array.isArray(node.branches)) {
    return [];
  }
  return node.branches
    .map((branchName) => branchName?.trim())
    .filter((branchName): branchName is string => Boolean(branchName));
}

function collectWaitEventTargets(node: DefinitionGraphNode): string[] {
  if (node.type !== "wait" || !node.events) {
    return [];
  }
  return Object.values(node.events)
    .map((targetName) => targetName?.trim())
    .filter((targetName): targetName is string => Boolean(targetName));
}

function collectEdgeTargets(node: DefinitionGraphNode): string[] {
  const nextTarget = node.next?.trim();
  const errorTarget = node.type === "action" ? node.error?.trim() : undefined;
  return [
    ...(nextTarget ? [nextTarget] : []),
    ...collectConfiguredEdgeTargets(node),
    ...(errorTarget ? [errorTarget] : []),
    ...collectForkBranchTargets(node),
    ...collectWaitEventTargets(node)
  ];
}

type NodeValidationContext = {
  node: DefinitionGraphNode;
  nodeName: string;
  hasNext: boolean;
  hasEdges: boolean;
  hasBranches: boolean;
  messages: string[];
  options: ValidateGraphDocumentMessageOptions;
};

/** wait.events がオブジェクトとして存在する（空マップ含む）か。 */
function hasEventsProperty(node: DefinitionGraphNode): boolean {
  return node.events !== undefined;
}

/** wait.events に非空キーが 1 件以上あるか。 */
function hasConfiguredWaitEvents(node: DefinitionGraphNode): boolean {
  if (!node.events) {
    return false;
  }
  return Object.keys(node.events).some((eventName) => eventName.trim().length > 0);
}

/**
 * 新形式 wait.events の併用禁止と遷移先必須を検証する。
 */
function validateWaitEventsMapNode({
  node,
  nodeName,
  hasEdges,
  messages,
  options
}: NodeValidationContext): void {
  if (node.event?.trim()) {
    messages.push(options.waitEventsAndEventTogether(nodeName));
  }
  if (hasEdges) {
    messages.push(options.waitEventsCannotHaveEdges(nodeName));
  }
  if (!hasConfiguredWaitEvents(node)) {
    messages.push(options.waitEventRequired(nodeName));
    return;
  }
  validateWaitEventTargets(nodeName, node.events ?? {}, messages, options);
}

/**
 * events マップ各エントリの遷移先が非空か検証する。
 */
function validateWaitEventTargets(
  nodeName: string,
  events: Record<string, string>,
  messages: string[],
  options: ValidateGraphDocumentMessageOptions
): void {
  for (const [eventName, rawTarget] of Object.entries(events)) {
    const trimmedName = eventName.trim();
    if (!trimmedName || rawTarget?.trim()) {
      continue;
    }
    messages.push(options.waitEventTargetRequired(nodeName, trimmedName));
  }
}

/**
 * 旧形式 wait（event + next/edges）を検証する。
 */
function validateLegacyWaitNode({
  node,
  nodeName,
  hasNext,
  hasEdges,
  messages,
  options
}: NodeValidationContext): void {
  if (!node.event?.trim()) {
    messages.push(options.waitEventRequired(nodeName));
  }
  if (!hasNext && !hasEdges) {
    messages.push(options.waitRequiresTransition(nodeName));
  }
}

/** IN / BETWEEN 用: YAML 配列または JSON 配列文字列を配列として解釈する */
function asWhenConditionArray(value: unknown): unknown[] | null {
  if (Array.isArray(value)) {
    return value as unknown[];
  }
  if (typeof value === "string") {
    const t = value.trim();
    if (t.startsWith("[") && t.endsWith("]")) {
      try {
        const parsed: unknown = JSON.parse(t);
        return Array.isArray(parsed) ? parsed : null;
      } catch {
        return null;
      }
    }
  }
  return null;
}

/** EXISTS 以外で比較値として「未指定」とみなすか（0 / false は有効） */
function whenScalarValueIsAbsent(value: unknown): boolean {
  if (value === undefined || value === null) {
    return true;
  }
  if (typeof value === "string" && value.trim() === "") {
    return true;
  }
  if (Array.isArray(value) && value.length === 0) {
    return true;
  }
  return false;
}

function validateWhenValueForOp(
  nodeName: string,
  opUpper: string,
  value: unknown,
  messages: string[],
  options: ValidateGraphDocumentMessageOptions
): void {
  if (opUpper === "EXISTS") {
    return;
  }
  if (opUpper === "IN") {
    const arr = asWhenConditionArray(value);
    if (arr == null || arr.length === 0) {
      messages.push(options.edgeWhenValueInInvalid(nodeName));
    }
    return;
  }
  if (opUpper === "BETWEEN") {
    const arr = asWhenConditionArray(value);
    if (arr == null || arr.length < 2) {
      messages.push(options.edgeWhenValueBetweenInvalid(nodeName));
    }
    return;
  }
  if (whenScalarValueIsAbsent(value)) {
    messages.push(options.edgeWhenValueRequired(nodeName));
  }
}

function validateEdgeCondition(
  nodeName: string,
  edge: DefinitionGraphEdge,
  messages: string[],
  options: ValidateGraphDocumentMessageOptions
): void {
  if (!edge.to?.trim()) {
    messages.push(options.edgeToRequired(nodeName));
  }
  if (edge.when) {
    if (!edge.when.path?.trim()) {
      messages.push(options.edgeWhenPathRequired(nodeName));
    }
    const opRaw = edge.when.op?.trim() ?? "";
    if (opRaw) {
      validateWhenValueForOp(nodeName, opRaw.toUpperCase(), edge.when.value, messages, options);
    } else {
      messages.push(options.edgeWhenOpRequired(nodeName));
    }
  }
}

function collectNodeMap(
  nodes: DefinitionGraphNode[],
  messages: string[],
  options: ValidateGraphDocumentMessageOptions
): Map<string, DefinitionGraphNode> {
  const byName = new Map<string, DefinitionGraphNode>();
  for (const node of nodes) {
    const nodeName = node.name?.trim();
    if (!nodeName) {
      messages.push(options.nodeNameRequired());
      continue;
    }
    const normalized = nodeName.toLowerCase();
    if (byName.has(normalized)) {
      messages.push(options.duplicateNodeName(nodeName));
      continue;
    }
    byName.set(normalized, { ...node, name: nodeName });
  }
  return byName;
}

function validateStartEndCounts(
  nodes: DefinitionGraphNode[],
  messages: string[],
  options: ValidateGraphDocumentMessageOptions
): void {
  const startCount = nodes.filter((node) => node.type === "start").length;
  const endCount = nodes.filter((node) => node.type === "end").length;
  if (startCount !== 1) {
    messages.push(options.startCountInvalid(startCount));
  }
  if (endCount !== 1) {
    messages.push(options.endCountInvalid(endCount));
  }
}

type NodeTypeValidator = (context: NodeValidationContext) => void;

const nodeTypeValidators: Record<DefinitionGraphNode["type"], NodeTypeValidator> = {
  start: ({ nodeName, hasNext, hasEdges, messages, options }) => {
    if (!hasNext && !hasEdges) {
      messages.push(options.startRequiresTransition(nodeName));
    }
  },
  action: ({ node, nodeName, hasNext, hasEdges, messages, options }) => {
    if (!node.action?.trim()) {
      messages.push(options.actionRequired(nodeName));
    }
    if (!hasNext && !hasEdges) {
      messages.push(options.actionRequiresTransition(nodeName));
    }
  },
  wait: (context) => {
    if (hasEventsProperty(context.node)) {
      validateWaitEventsMapNode(context);
      return;
    }
    validateLegacyWaitNode(context);
  },
  fork: ({ node, nodeName, hasBranches, messages, options }) => {
    if (!hasBranches || (node.branches?.length ?? 0) < 2) {
      messages.push(options.forkBranchesRequired(nodeName));
    }
  },
  join: ({ node, nodeName, hasNext, hasEdges, messages, options }) => {
    if (!hasNext && !hasEdges) {
      messages.push(options.joinRequiresTransition(nodeName));
    }
    if (node.mode && node.mode !== "all") {
      messages.push(options.joinModeInvalid(nodeName));
    }
  },
  end: ({ nodeName, hasNext, hasEdges, messages, options }) => {
    if (hasNext || hasEdges) {
      messages.push(options.endCannotHaveTransition(nodeName));
    }
  }
};

function validateNodeByType(context: NodeValidationContext): void {
  nodeTypeValidators[context.node.type](context);
}

function validateNodeTargets(
  node: DefinitionGraphNode,
  nodeName: string,
  byName: Map<string, DefinitionGraphNode>,
  messages: string[],
  options: ValidateGraphDocumentMessageOptions
): void {
  const targets = collectEdgeTargets(node);
  for (const targetName of targets) {
    if (targetName.toLowerCase() === nodeName.toLowerCase()) {
      messages.push(options.selfReferenceEdge(nodeName));
    }
    if (!byName.has(targetName.toLowerCase())) {
      messages.push(options.missingTargetNode(nodeName, targetName));
    }
  }
}

function validateNode(
  node: DefinitionGraphNode,
  byName: Map<string, DefinitionGraphNode>,
  messages: string[],
  options: ValidateGraphDocumentMessageOptions
): void {
  const nodeName = node.name?.trim() ?? "";
  if (!nodeName) {
    return;
  }

  const hasNext = Boolean(node.next?.trim());
  const hasEdges = Array.isArray(node.edges) && node.edges.length > 0;
  const hasBranches = Array.isArray(node.branches) && node.branches.length > 0;
  const defaultEdgeCount = (node.edges ?? []).filter((edge) => edge.default === true).length;

  validateNodeByType({
    node,
    nodeName,
    hasNext,
    hasEdges,
    hasBranches,
    messages,
    options
  });

  if (hasEdges) {
    for (const edge of node.edges ?? []) {
      validateEdgeCondition(nodeName, edge, messages, options);
    }
    if (defaultEdgeCount > 1) {
      messages.push(options.edgeDefaultMultiple(nodeName));
    }
  }

  validateNodeTargets(node, nodeName, byName, messages, options);
}

/**
 * Graph編集用ドキュメントのクライアント側整合性を検証する。
 * 保存前に弾ける構造不整合（自己参照、重複名、必須項目不足）と、
 * Fork 領域の安いチェック（領域外侵入・Join なし脱出・Wait 領域外）を返す。
 * Engine 検証の正本は共有しない。
 */
export function validateGraphDocument(
  document: DefinitionGraphDocument,
  options: ValidateGraphDocumentMessageOptions
): ValidateGraphDocumentResult {
  const messages: string[] = [];
  if (!Array.isArray(document.nodes) || document.nodes.length === 0) {
    return {
      isValid: false,
      messages: [options.nodesRequired()]
    };
  }

  const byName = collectNodeMap(document.nodes, messages, options);
  validateStartEndCounts(document.nodes, messages, options);
  for (const node of document.nodes) {
    validateNode(node, byName, messages, options);
  }
  validateForkRegionLight(byName, messages, options);

  return {
    isValid: messages.length === 0,
    messages
  };
}
