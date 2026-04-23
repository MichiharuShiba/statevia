"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useMemo } from "react";
import { ExecutionDashboard } from "../../../components/execution/ExecutionDashboard";

/**
 * Run 専用ページ（T8 で実行操作の集約を拡張予定）。
 */
export default function WorkflowRunPage() {
  const params = useParams();
  const workflowId = useMemo(() => {
    const raw = params.workflowId;
    const segment = Array.isArray(raw) ? raw[0] : raw;
    return segment ? decodeURIComponent(String(segment)) : "";
  }, [params.workflowId]);

  if (!workflowId.trim()) {
    return (
      <main className="mx-auto max-w-2xl p-6">
        <p className="rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">
          ワークフロー ID が指定されていません。
        </p>
        <Link className="mt-3 inline-block text-sm text-blue-700 underline" href="/workflows">
          一覧に戻る
        </Link>
      </main>
    );
  }

  return (
    <ExecutionDashboard
      key={workflowId}
      initialExecutionId={workflowId}
      autoLoadOnMount
      headerTitle="ワークフロー実行"
      executionIdEditable={false}
      comparisonEnabled={false}
      headerNav={
        <div className="flex flex-wrap items-center gap-3 text-xs">
          <Link className="text-zinc-600 hover:underline" href={`/workflows/${encodeURIComponent(workflowId)}`}>
            詳細
          </Link>
          <Link className="text-zinc-600 hover:underline" href="/workflows">
            一覧
          </Link>
          <Link className="text-zinc-600 hover:underline" href="/dashboard">
            ダッシュボード
          </Link>
        </div>
      }
    />
  );
}
