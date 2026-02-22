"use client";

export type ViewMode = "list" | "graph";

type ViewToggleProps = {
  value: ViewMode;
  onChange: (mode: ViewMode) => void;
};

export function ViewToggle({ value, onChange }: ViewToggleProps) {
  return (
    <div className="inline-flex rounded-xl border border-zinc-200 p-1">
      <button
        className={`rounded-lg px-3 py-1.5 text-sm ${value === "list" ? "bg-zinc-900 text-white" : "text-zinc-700 hover:bg-zinc-100"}`}
        onClick={() => onChange("list")}
      >
        List
      </button>
      <button
        className={`rounded-lg px-3 py-1.5 text-sm ${value === "graph" ? "bg-zinc-900 text-white" : "text-zinc-700 hover:bg-zinc-100"}`}
        onClick={() => onChange("graph")}
      >
        Graph
      </button>
    </div>
  );
}

