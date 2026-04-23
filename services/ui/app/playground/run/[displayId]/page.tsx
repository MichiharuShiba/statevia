"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useMemo } from "react";
import { ExecutionDashboard } from "../../../components/execution/ExecutionDashboard";

export default function PlaygroundRunPage() {
  const params = useParams();
  const displayId = useMemo(() => {
    const raw = params.displayId;
    const segment = Array.isArray(raw) ? raw[0] : raw;
    return segment ? decodeURIComponent(segment) : "";
  }, [params.displayId]);

  if (!displayId.trim()) {
    return (
      <div className="rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">
        実行 ID が指定されていません。
        <Link href="/playground" className="ml-2 text-blue-700 underline">
          Playground に戻る
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <section className="rounded-lg border border-sky-200 bg-sky-50 px-4 py-3 text-sm text-sky-900">
        <p className="font-medium">このページは旧導線（互換）です</p>
        <p className="mt-1">
          新しい導線では、同じ実行を <code>/workflows/{displayId}</code> / <code>/workflows/{displayId}/run</code> /
          {" "}
          <code>/workflows/{displayId}/graph</code> で参照できます。
        </p>
        <p className="mt-2 flex flex-wrap items-center gap-3">
          <Link className="text-blue-800 underline hover:text-blue-950" href={`/workflows/${encodeURIComponent(displayId)}`}>
            詳細（新導線）
          </Link>
          <Link className="text-blue-800 underline hover:text-blue-950" href={`/workflows/${encodeURIComponent(displayId)}/run`}>
            Run（新導線）
          </Link>
          <Link className="text-blue-800 underline hover:text-blue-950" href={`/workflows/${encodeURIComponent(displayId)}/graph`}>
            Graph（新導線）
          </Link>
        </p>
      </section>

      <ExecutionDashboard
        key={displayId}
        initialExecutionId={displayId}
        autoLoadOnMount
        headerTitle="実行の詳細（旧導線）"
        headerNav={
          <div className="flex flex-wrap items-center gap-3 text-xs">
            <Link className="text-zinc-600 hover:underline" href="/playground">
              ← Playground
            </Link>
            <Link className="text-zinc-600 hover:underline" href={`/workflows/${encodeURIComponent(displayId)}`}>
              新: 詳細
            </Link>
            <Link className="text-zinc-600 hover:underline" href={`/workflows/${encodeURIComponent(displayId)}/run`}>
              新: Run
            </Link>
            <Link className="text-zinc-600 hover:underline" href="/dashboard">
              ダッシュボード
            </Link>
            <a className="text-zinc-600 hover:underline" href="/health">
              health
            </a>
          </div>
        }
      />
    </div>
  );
}
