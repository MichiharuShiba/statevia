## Playground MVP（A）を固定メモ

### 扱うもの

- **Execution Stateのみ**（例：`INIT / RUNNING / WAITING / CANCEL_REQUESTED / CANCELED / FAILED / COMPLETED`）
- ノード状態・Fork/Joinは **次フェーズ**

### 入力（イベント）

最小セット（まずこれだけで十分）

- `EXECUTION_STARTED`
- `EXECUTION_WAITING`
- `EXECUTION_RESUMED`
- `EXECUTION_CANCEL_REQUESTED`
- `EXECUTION_CANCELED`
- `EXECUTION_FAILED`
- `EXECUTION_COMPLETED`

### ルール（理由の核）

- **Cancel wins**（Cancel系が他より優先）
- terminal後のイベントは棄却（理由：`GUARD_TERMINAL`）
- 重複/無意味イベントは棄却（理由：`NO_OP` など）

### 出力（画面）

- **Current State**（大きく）
- **Why?（理由）**
  - Applied rules（例：`CANCEL_WINS`）
  - Rejected events（例：`RESUMED rejected because GUARD_TERMINAL`）
  - becauseチェーン（箇条書きでOK）

---

## 画面の最小レイアウト

- 左：イベントリスト（追加/削除/並べ替え）
- 右上：Current State
- 右下：Why?（理由）

---

## 次に作るべき“1枚”の文章（LP/README用）

**タイトル**：状態には、理由がある。
**説明**：イベントの順序と優先順位ルールが、状態をどう決めるかを可視化するPlayground。
**示唆**：This model can be embedded into orchestration, audit, and governance systems.

## 1) UIワイヤー

### 画面レイアウト（最小で刺さる）

```
┌─────────────────────────────────────────────────────────────┐
│ Reasoned State Playground            状態には、理由がある。  │
├──────────────────────────────┬──────────────────────────────┤
│ Events                        │ Current State                │
│ ┌──────────────────────────┐ │ ┌──────────────────────────┐ │
│ │ [↕] 1 EXECUTION_STARTED   │ │ │  STATUS: CANCELED         │ │
│ │ [↕] 2 EXECUTION_RESUMED   │ │ │  (terminal)               │ │
│ │ [↕] 3 EXECUTION_CANCELED  │ │ └──────────────────────────┘ │
│ │ [↕] 4 EXECUTION_COMPLETED │ │ Why?                         │
│ └──────────────────────────┘ │ ┌──────────────────────────┐ │
│ [+ Add Event] [Reset Sample]  │ │ Applied Rules              │ │
│                               │ │  - CANCEL_WINS             │ │
│                               │ │ Rejected Events            │ │
│                               │ │  - COMPLETED (GUARD_TERMINAL)
│                               │ │ Because Chain              │ │
│                               │ │  - CANCELED applied        │ │
│                               │ │  - COMPLETED rejected ...  │ │
│                               │ └──────────────────────────┘ │
└──────────────────────────────┴──────────────────────────────┘
```

### コンポーネント分割（React想定）

- `PlaygroundPage`
  - `EventListPanel`
    - `EventRow`（DnDで並び替え）
    - `AddEventMenu`（イベント種別ドロップダウン）

  - `StatePanel`
  - `WhyPanel`
    - `AppliedRulesList`
    - `RejectedEventsList`
    - `BecauseChain`

### データフロー（超シンプル）

- UIの `events[]` が唯一の入力
- `compute(events[])` が
  - `state`
  - `trace`（applied/rejected/rules/because）
    を返す

- UIはそれを表示するだけ

---

## 2) reducer最小実装（trace付き / TypeScript）

> Playground用なので「Execution全体のみ」「イベント7種」「Cancel wins」「terminal guard」を最小で入れています。

```ts
// playground/core.ts

export type ExecutionStatus =
  | "INIT"
  | "RUNNING"
  | "WAITING"
  | "CANCEL_REQUESTED"
  | "CANCELED"
  | "FAILED"
  | "COMPLETED";

export type EventType =
  | "EXECUTION_STARTED"
  | "EXECUTION_WAITING"
  | "EXECUTION_RESUMED"
  | "EXECUTION_CANCEL_REQUESTED"
  | "EXECUTION_CANCELED"
  | "EXECUTION_FAILED"
  | "EXECUTION_COMPLETED";

export type ExecutionEvent = {
  id: string; // UI側でuuidでも連番でもOK
  type: EventType;
};

export type ReasonCode =
  | "CANCEL_WINS"
  | "GUARD_TERMINAL"
  | "NO_OP"
  | "INVALID_TRANSITION";

export type Reason = {
  code: ReasonCode;
  params?: Record<string, unknown>;
};

export type TraceEntry = {
  eventId: string;
  eventType: EventType;
  applied: boolean;
  reasons: Reason[];
};

export type Trace = {
  appliedRules: Array<{ code: ReasonCode; params?: Record<string, unknown> }>;
  entries: TraceEntry[];
  becauseChain: string[]; // UI用に最小は文字列でOK（後で構造化できる）
};

export type ExecutionState = {
  status: ExecutionStatus;
  terminal: boolean;
};

export const initialState: ExecutionState = { status: "INIT", terminal: false };

function isTerminal(s: ExecutionStatus): boolean {
  return s === "CANCELED" || s === "FAILED" || s === "COMPLETED";
}

/**
 * Cancel wins 優先順位（簡易 rank）
 * 数字が小さいほど強い
 */
function rank(e: EventType): number {
  switch (e) {
    case "EXECUTION_CANCELED":
      return 0;
    case "EXECUTION_CANCEL_REQUESTED":
      return 1;
    case "EXECUTION_FAILED":
      return 2;
    case "EXECUTION_COMPLETED":
      return 3;
    case "EXECUTION_RESUMED":
      return 4;
    case "EXECUTION_WAITING":
      return 5;
    case "EXECUTION_STARTED":
      return 6;
    default:
      return 99;
  }
}

/**
 * 遷移（最小）
 * - terminal後は何も適用しない（GUARD_TERMINAL）
 * - Cancel wins: Cancel/Canceled が来たら終端へ寄せる
 */
export function applyEvent(
  state: ExecutionState,
  event: ExecutionEvent,
): {
  next: ExecutionState;
  trace: TraceEntry;
  appliedRules: Trace["appliedRules"];
  because: string[];
} {
  const reasons: Reason[] = [];
  const appliedRules: Trace["appliedRules"] = [];
  const because: string[] = [];

  // Guard: terminal
  if (state.terminal) {
    reasons.push({
      code: "GUARD_TERMINAL",
      params: { terminalStatus: state.status },
    });
    because.push(`${event.type} rejected because terminal(${state.status})`);
    return {
      next: state,
      trace: {
        eventId: event.id,
        eventType: event.type,
        applied: false,
        reasons,
      },
      appliedRules,
      because,
    };
  }

  // Cancel wins: if CancelRequested/Canceled occurs, we prefer cancel line strongly
  if (event.type === "EXECUTION_CANCELED") {
    appliedRules.push({ code: "CANCEL_WINS" });
    because.push("CANCEL_WINS applied: EXECUTION_CANCELED overrides others");
    const next: ExecutionState = { status: "CANCELED", terminal: true };
    return {
      next,
      trace: {
        eventId: event.id,
        eventType: event.type,
        applied: true,
        reasons: [{ code: "CANCEL_WINS" }],
      },
      appliedRules,
      because,
    };
  }

  if (event.type === "EXECUTION_CANCEL_REQUESTED") {
    appliedRules.push({ code: "CANCEL_WINS" });
    because.push(
      "CANCEL_WINS applied: EXECUTION_CANCEL_REQUESTED sets cancel-requested context",
    );
    const next: ExecutionState = {
      status: "CANCEL_REQUESTED",
      terminal: false,
    };
    return {
      next,
      trace: {
        eventId: event.id,
        eventType: event.type,
        applied: true,
        reasons: [{ code: "CANCEL_WINS" }],
      },
      appliedRules,
      because,
    };
  }

  // If we are already cancel-requested, block "forward progress" events (minimal policy)
  if (state.status === "CANCEL_REQUESTED") {
    // You can tune this list later
    if (
      event.type === "EXECUTION_RESUMED" ||
      event.type === "EXECUTION_COMPLETED"
    ) {
      reasons.push({ code: "CANCEL_WINS", params: { blocked: event.type } });
      because.push(
        `${event.type} rejected because CANCEL_REQUESTED (Cancel wins)`,
      );
      return {
        next: state,
        trace: {
          eventId: event.id,
          eventType: event.type,
          applied: false,
          reasons,
        },
        appliedRules: [{ code: "CANCEL_WINS" }],
        because,
      };
    }
  }

  // Normal transitions (minimal)
  switch (event.type) {
    case "EXECUTION_STARTED": {
      if (state.status !== "INIT") {
        reasons.push({ code: "NO_OP", params: { status: state.status } });
        because.push(`STARTED ignored because already ${state.status}`);
        return {
          next: state,
          trace: {
            eventId: event.id,
            eventType: event.type,
            applied: false,
            reasons,
          },
          appliedRules,
          because,
        };
      }
      const next = {
        status: "RUNNING",
        terminal: false,
      } satisfies ExecutionState;
      because.push("STARTED -> RUNNING");
      return {
        next,
        trace: {
          eventId: event.id,
          eventType: event.type,
          applied: true,
          reasons: [],
        },
        appliedRules,
        because,
      };
    }

    case "EXECUTION_WAITING": {
      if (state.status !== "RUNNING") {
        reasons.push({
          code: "INVALID_TRANSITION",
          params: { from: state.status, to: "WAITING" },
        });
        because.push(`WAITING rejected because invalid from ${state.status}`);
        return {
          next: state,
          trace: {
            eventId: event.id,
            eventType: event.type,
            applied: false,
            reasons,
          },
          appliedRules,
          because,
        };
      }
      const next = {
        status: "WAITING",
        terminal: false,
      } satisfies ExecutionState;
      because.push("WAITING applied: RUNNING -> WAITING");
      return {
        next,
        trace: {
          eventId: event.id,
          eventType: event.type,
          applied: true,
          reasons: [],
        },
        appliedRules,
        because,
      };
    }

    case "EXECUTION_RESUMED": {
      if (state.status !== "WAITING") {
        reasons.push({
          code: "INVALID_TRANSITION",
          params: { from: state.status, to: "RUNNING" },
        });
        because.push(`RESUMED rejected because invalid from ${state.status}`);
        return {
          next: state,
          trace: {
            eventId: event.id,
            eventType: event.type,
            applied: false,
            reasons,
          },
          appliedRules,
          because,
        };
      }
      const next = {
        status: "RUNNING",
        terminal: false,
      } satisfies ExecutionState;
      because.push("RESUMED applied: WAITING -> RUNNING");
      return {
        next,
        trace: {
          eventId: event.id,
          eventType: event.type,
          applied: true,
          reasons: [],
        },
        appliedRules,
        because,
      };
    }

    case "EXECUTION_FAILED": {
      const next = {
        status: "FAILED",
        terminal: true,
      } satisfies ExecutionState;
      because.push("FAILED applied -> terminal");
      return {
        next,
        trace: {
          eventId: event.id,
          eventType: event.type,
          applied: true,
          reasons: [],
        },
        appliedRules,
        because,
      };
    }

    case "EXECUTION_COMPLETED": {
      const next = {
        status: "COMPLETED",
        terminal: true,
      } satisfies ExecutionState;
      because.push("COMPLETED applied -> terminal");
      return {
        next,
        trace: {
          eventId: event.id,
          eventType: event.type,
          applied: true,
          reasons: [],
        },
        appliedRules,
        because,
      };
    }

    default: {
      reasons.push({ code: "NO_OP" });
      because.push(`${event.type} ignored`);
      return {
        next: state,
        trace: {
          eventId: event.id,
          eventType: event.type,
          applied: false,
          reasons,
        },
        appliedRules,
        because,
      };
    }
  }
}

/**
 * compute: eventsを順に適用し、stateとtraceを返す
 * 追加：rankで並べ替えるのはUI側（比較実験用）でやるのが楽。
 */
export function compute(events: ExecutionEvent[]): {
  state: ExecutionState;
  trace: Trace;
} {
  let s = initialState;

  const trace: Trace = {
    appliedRules: [],
    entries: [],
    becauseChain: [],
  };

  for (const e of events) {
    const out = applyEvent(s, e);
    s = out.next;

    trace.entries.push(out.trace);
    trace.appliedRules.push(...out.appliedRules);
    trace.becauseChain.push(...out.because);
  }

  // normalize: terminal判定を最終整合（保険）
  s = { ...s, terminal: isTerminal(s.status) };

  return { state: s, trace };
}

/**
 * optional: Playgroundで「順序差分」を作るためのヘルパ
 * - 同じeventsでもrank順でソートした結果を見られる
 */
export function sortByRank(events: ExecutionEvent[]): ExecutionEvent[] {
  return [...events].sort((a, b) => rank(a.type) - rank(b.type));
}
```

---

## 使い方（UI側の最小イメージ）

- `events` は画面左のリストそのもの
- 右側は毎回 `compute(events)` を呼んで表示

```ts
const { state, trace } = compute(events);
// state.status を StatePanelへ
// trace.appliedRules / trace.entries(棄却) / trace.becauseChain を WhyPanelへ
```
