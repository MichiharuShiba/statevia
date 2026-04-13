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
    <ExecutionDashboard
      key={displayId}
      initialExecutionId={displayId}
      autoLoadOnMount
      headerTitle="実行の詳細"
      headerNav={
        <div className="flex flex-wrap items-center gap-3 text-xs">
          <Link className="text-zinc-600 hover:underline" href="/playground">
            ← Playground
          </Link>
          <Link className="text-zinc-600 hover:underline" href="/">
            Execution UI
          </Link>
          <a className="text-zinc-600 hover:underline" href="/health">
            health
          </a>
        </div>
      }
    />
  );
}
