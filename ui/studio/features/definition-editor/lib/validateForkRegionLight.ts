import type { DefinitionGraphNode } from "./types";

/** Studio の安い Fork 領域指摘メッセージ。 */
export type ForkRegionLightMessageOptions = {
  forkRegionIngressFromOutside: (fromName: string, toName: string) => string;
  forkRegionEgressWithoutJoin: (fromName: string, toName: string, joinName: string) => string;
  forkRegionWaitTargetOutside: (nodeName: string, toName: string) => string;
};

type NameMap = Map<string, DefinitionGraphNode>;

type ForkJoinLightPair = {
  forkName: string;
  joinName: string;
  body: Set<string>;
};

/**
 * Studio 向けの安い Fork 領域チェック（A1 / A2 / 禁止パターン D2）。
 *
 * @param byName - 正規化名 → ノード。
 * @param messages - 指摘の蓄積先。
 * @param options - i18n メッセージ。
 * @remarks Engine のスナップショット正本は共有しない。対応 Join が一意に決まる Fork だけ見る。
 */
export function validateForkRegionLight(
  byName: NameMap,
  messages: string[],
  options: ForkRegionLightMessageOptions
): void {
  const pairs = collectLightPairs(byName);
  pairs.forEach((pair) => {
    addIngressIssues(pair, byName, messages, options);
    addEgressAndWaitIssues(pair, byName, messages, options);
  });
}

function collectLightPairs(byName: NameMap): ForkJoinLightPair[] {
  return [...byName.values()]
    .filter((node) => node.type === "fork")
    .map((fork) => tryPairFork(fork, byName))
    .filter((pair): pair is ForkJoinLightPair => pair !== null);
}

function tryPairFork(fork: DefinitionGraphNode, byName: NameMap): ForkJoinLightPair | null {
  const heads = (fork.branches ?? [])
    .map((branch) => normalizeName(branch))
    .filter((head) => head.length > 0 && byName.has(head));
  if (heads.length < 2) {
    return null;
  }

  const joinSets = heads.map((head) => collectReachableJoins(head, byName));
  const joinName = pickPairedJoin(joinSets);
  if (!joinName) {
    return null;
  }
  const forkName = normalizeName(fork.name);
  return {
    forkName,
    joinName,
    body: collectRegionBody(heads, joinName, byName)
  };
}

function collectReachableJoins(start: string, byName: NameMap): Set<string> {
  const joins = new Set<string>();
  const visited = new Set<string>([start]);
  const queue = [start];
  const stepLimit = byName.size + 8;
  let steps = 0;
  while (queue.length > 0 && steps < stepLimit) {
    steps += 1;
    const current = queue.shift();
    if (current === undefined) {
      break;
    }
    const node = byName.get(current);
    if (!node) {
      continue;
    }
    if (node.type === "join") {
      joins.add(current);
    }
    enqueueNew(queue, visited, outgoingTargets(node, { includeError: false }));
  }
  return joins;
}

function collectRegionBody(heads: string[], joinName: string, byName: NameMap): Set<string> {
  const body = new Set<string>();
  const visited = new Set<string>(heads);
  const queue = [...heads];
  const stepLimit = byName.size + 8;
  let steps = 0;
  while (queue.length > 0 && steps < stepLimit) {
    steps += 1;
    const current = queue.shift();
    if (current === undefined || current === joinName) {
      continue;
    }
    const node = byName.get(current);
    if (!node || node.type === "end") {
      continue;
    }
    body.add(current);
    enqueueNew(queue, visited, outgoingTargets(node, { includeError: true }));
  }
  return body;
}

function addIngressIssues(
  pair: ForkJoinLightPair,
  byName: NameMap,
  messages: string[],
  options: ForkRegionLightMessageOptions
): void {
  [...byName.entries()]
    .filter(([source]) => source !== pair.forkName && !pair.body.has(source))
    .forEach(([source, node]) => {
      outgoingTargets(node, { includeError: true })
        .filter((target) => pair.body.has(target))
        .forEach((target) => {
          messages.push(options.forkRegionIngressFromOutside(displayName(node, source), displayTarget(byName, target)));
        });
    });
}

function addEgressAndWaitIssues(
  pair: ForkJoinLightPair,
  byName: NameMap,
  messages: string[],
  options: ForkRegionLightMessageOptions
): void {
  [...pair.body]
    .map((name) => byName.get(name))
    .filter((node): node is DefinitionGraphNode => node !== undefined)
    .forEach((node) => {
      const sourceName = displayName(node, normalizeName(node.name));
      outgoingTargets(node, { includeError: true })
        .filter((target) => target !== pair.joinName && !pair.body.has(target))
        .forEach((target) => {
          messages.push(
            options.forkRegionEgressWithoutJoin(sourceName, displayTarget(byName, target), displayTarget(byName, pair.joinName))
          );
        });
      if (node.type !== "wait") {
        return;
      }
      waitTargets(node)
        .filter((target) => target !== pair.joinName && !pair.body.has(target))
        .forEach((target) => {
          messages.push(options.forkRegionWaitTargetOutside(sourceName, displayTarget(byName, target)));
        });
    });
}

function outgoingTargets(node: DefinitionGraphNode, flags: { includeError: boolean }): string[] {
  const next = optionalName(node.next);
  const error = flags.includeError && node.type === "action" ? optionalName(node.error) : undefined;
  const edgeTargets = (node.edges ?? []).map((edge) => optionalName(edge.to)).filter((to): to is string => Boolean(to));
  const branchTargets =
    node.type === "fork"
      ? (node.branches ?? []).map((branch) => optionalName(branch)).filter((to): to is string => Boolean(to))
      : [];
  const wait = waitTargets(node);
  return uniqueNames([next, error, ...edgeTargets, ...branchTargets, ...wait].filter((to): to is string => Boolean(to)));
}

function waitTargets(node: DefinitionGraphNode): string[] {
  if (node.type !== "wait") {
    return [];
  }
  if (node.events) {
    return uniqueNames(
      Object.values(node.events)
        .map((target) => optionalName(target))
        .filter((to): to is string => Boolean(to))
    );
  }
  const legacyNext = optionalName(node.next);
  return legacyNext ? [legacyNext] : [];
}

function pickPairedJoin(joinSets: Set<string>[]): string | undefined {
  const intersection = intersectAll(joinSets);
  if (intersection.size === 1) {
    return [...intersection][0];
  }
  const union = unionAll(joinSets);
  if (union.size === 1) {
    return [...union][0];
  }
  return undefined;
}

function enqueueNew(queue: string[], visited: Set<string>, targets: string[]): void {
  targets
    .filter((target) => visited.add(target))
    .forEach((target) => {
      queue.push(target);
    });
}

function intersectAll(sets: Set<string>[]): Set<string> {
  if (sets.length === 0) {
    return new Set();
  }
  return sets.reduce(
    (acc, current) => new Set([...acc].filter((name) => current.has(name))),
    new Set(sets[0])
  );
}

function unionAll(sets: Set<string>[]): Set<string> {
  return new Set(sets.flatMap((current) => [...current]));
}

function uniqueNames(names: string[]): string[] {
  return [...new Set(names)];
}

function optionalName(value: string | undefined): string | undefined {
  const normalized = normalizeName(value ?? "");
  return normalized.length > 0 ? normalized : undefined;
}

function normalizeName(value: string): string {
  return value.trim().toLowerCase();
}

function displayName(node: DefinitionGraphNode, fallback: string): string {
  const trimmed = node.name?.trim();
  return trimmed && trimmed.length > 0 ? trimmed : fallback;
}

function displayTarget(byName: NameMap, normalized: string): string {
  const node = byName.get(normalized);
  return node ? displayName(node, normalized) : normalized;
}
