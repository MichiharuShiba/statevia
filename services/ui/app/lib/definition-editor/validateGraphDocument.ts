import type {
  DefinitionGraphDocument,
  DefinitionGraphEdge,
  DefinitionGraphNode,
  ValidateGraphDocumentResult
} from "./types";

export type ValidateGraphDocumentMessageOptions = {
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
};

function collectEdgeTargets(node: DefinitionGraphNode): string[] {
  const targets: string[] = [];
  if (node.next?.trim()) {
    targets.push(node.next.trim());
  }
  if (Array.isArray(node.edges)) {
    for (const edge of node.edges) {
      if (edge.to?.trim()) {
        targets.push(edge.to.trim());
      }
    }
  }
  if (node.type === "action" && node.error?.trim()) {
    targets.push(node.error.trim());
  }
  if (node.type === "fork" && Array.isArray(node.branches)) {
    for (const branchId of node.branches) {
      if (branchId?.trim()) {
        targets.push(branchId.trim());
      }
    }
  }
  return targets;
}

/** IN / BETWEEN 用: YAML 配列または JSON 配列文字列を配列として解釈する */
function asWhenConditionArray(value: unknown): unknown[] | null {
  if (Array.isArray(value)) {
    return value;
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
  nodeId: string,
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
      messages.push(options.edgeWhenValueInInvalid(nodeId));
    }
    return;
  }
  if (opUpper === "BETWEEN") {
    const arr = asWhenConditionArray(value);
    if (arr == null || arr.length < 2) {
      messages.push(options.edgeWhenValueBetweenInvalid(nodeId));
    }
    return;
  }
  if (whenScalarValueIsAbsent(value)) {
    messages.push(options.edgeWhenValueRequired(nodeId));
  }
}

function validateEdgeCondition(
  nodeId: string,
  edge: DefinitionGraphEdge,
  messages: string[],
  options: ValidateGraphDocumentMessageOptions
): void {
  if (!edge.to?.trim()) {
    messages.push(options.edgeToRequired(nodeId));
  }
  if (edge.when) {
    if (!edge.when.path?.trim()) {
      messages.push(options.edgeWhenPathRequired(nodeId));
    }
    const opRaw = edge.when.op?.trim() ?? "";
    if (opRaw) {
      validateWhenValueForOp(nodeId, opRaw.toUpperCase(), edge.when.value, messages, options);
    } else {
      messages.push(options.edgeWhenOpRequired(nodeId));
    }
  }
}

function collectNodeMap(
  nodes: DefinitionGraphNode[],
  messages: string[],
  options: ValidateGraphDocumentMessageOptions
): Map<string, DefinitionGraphNode> {
  const byId = new Map<string, DefinitionGraphNode>();
  for (const node of nodes) {
    const nodeId = node.id?.trim();
    if (!nodeId) {
      messages.push(options.nodeIdRequired());
      continue;
    }
    const normalized = nodeId.toLowerCase();
    if (byId.has(normalized)) {
      messages.push(options.duplicateNodeId(nodeId));
      continue;
    }
    byId.set(normalized, { ...node, id: nodeId });
  }
  return byId;
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

type NodeValidationContext = {
  node: DefinitionGraphNode;
  nodeId: string;
  hasNext: boolean;
  hasEdges: boolean;
  hasBranches: boolean;
  messages: string[];
  options: ValidateGraphDocumentMessageOptions;
};

type NodeTypeValidator = (context: NodeValidationContext) => void;

const nodeTypeValidators: Record<DefinitionGraphNode["type"], NodeTypeValidator> = {
  start: ({ nodeId, hasNext, hasEdges, messages, options }) => {
    if (!hasNext && !hasEdges) {
      messages.push(options.startRequiresTransition(nodeId));
    }
  },
  action: ({ node, nodeId, hasNext, hasEdges, messages, options }) => {
    if (!node.action?.trim()) {
      messages.push(options.actionRequired(nodeId));
    }
    if (!hasNext && !hasEdges) {
      messages.push(options.actionRequiresTransition(nodeId));
    }
  },
  wait: ({ node, nodeId, hasNext, hasEdges, messages, options }) => {
    if (!node.event?.trim()) {
      messages.push(options.waitEventRequired(nodeId));
    }
    if (!hasNext && !hasEdges) {
      messages.push(options.waitRequiresTransition(nodeId));
    }
  },
  fork: ({ node, nodeId, hasBranches, messages, options }) => {
    if (!hasBranches || (node.branches?.length ?? 0) < 2) {
      messages.push(options.forkBranchesRequired(nodeId));
    }
  },
  join: ({ node, nodeId, hasNext, hasEdges, messages, options }) => {
    if (!hasNext && !hasEdges) {
      messages.push(options.joinRequiresTransition(nodeId));
    }
    if (node.mode && node.mode !== "all") {
      messages.push(options.joinModeInvalid(nodeId));
    }
  },
  end: ({ nodeId, hasNext, hasEdges, messages, options }) => {
    if (hasNext || hasEdges) {
      messages.push(options.endCannotHaveTransition(nodeId));
    }
  }
};

function validateNodeByType(context: NodeValidationContext): void {
  nodeTypeValidators[context.node.type](context);
}

function validateNodeTargets(
  node: DefinitionGraphNode,
  nodeId: string,
  byId: Map<string, DefinitionGraphNode>,
  messages: string[],
  options: ValidateGraphDocumentMessageOptions
): void {
  const targets = collectEdgeTargets(node);
  for (const targetId of targets) {
    if (targetId.toLowerCase() === nodeId.toLowerCase()) {
      messages.push(options.selfReferenceEdge(nodeId));
    }
    if (!byId.has(targetId.toLowerCase())) {
      messages.push(options.missingTargetNode(nodeId, targetId));
    }
  }
}

function validateNode(
  node: DefinitionGraphNode,
  byId: Map<string, DefinitionGraphNode>,
  messages: string[],
  options: ValidateGraphDocumentMessageOptions
): void {
  const nodeId = node.id?.trim() ?? "";
  if (!nodeId) {
    return;
  }

  const hasNext = Boolean(node.next?.trim());
  const hasEdges = Array.isArray(node.edges) && node.edges.length > 0;
  const hasBranches = Array.isArray(node.branches) && node.branches.length > 0;
  const defaultEdgeCount = (node.edges ?? []).filter((edge) => edge.default === true).length;

  validateNodeByType({
    node,
    nodeId,
    hasNext,
    hasEdges,
    hasBranches,
    messages,
    options
  });

  if (hasEdges) {
    for (const edge of node.edges ?? []) {
      validateEdgeCondition(nodeId, edge, messages, options);
    }
    if (defaultEdgeCount > 1) {
      messages.push(options.edgeDefaultMultiple(nodeId));
    }
  }

  validateNodeTargets(node, nodeId, byId, messages, options);
}

/**
 * Graph編集用ドキュメントのクライアント側整合性を検証する。
 * 保存前に弾ける構造不整合（自己参照、重複ID、必須項目不足）を返す。
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

  const byId = collectNodeMap(document.nodes, messages, options);
  validateStartEndCounts(document.nodes, messages, options);
  for (const node of document.nodes) {
    validateNode(node, byId, messages, options);
  }

  return {
    isValid: messages.length === 0,
    messages
  };
}
