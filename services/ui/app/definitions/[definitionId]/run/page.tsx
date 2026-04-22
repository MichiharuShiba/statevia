import Link from "next/link";

type DefinitionRunPlaceholderPageProps = {
  params: Promise<{
    definitionId: string;
  }>;
};

const playgroundHref = (definitionId: string) => {
  const sp = new URLSearchParams();
  sp.set("definitionId", definitionId);
  return `/playground?${sp.toString()}`;
};

/**
 * 定義起点の新規実行プレースホルダ。T7 で `POST /v1/workflows` と専用 Run 画面遷移を接続する。
 */
export default async function DefinitionRunPlaceholderPage({ params }: Readonly<DefinitionRunPlaceholderPageProps>) {
  const { definitionId } = await params;

  return (
    <main className="mx-auto flex max-w-2xl flex-col gap-5 p-6">
      <h1 className="text-xl font-semibold text-zinc-900">定義起点で実行</h1>
      <p className="text-sm text-zinc-600">displayId: <span className="font-mono break-all">{definitionId}</span></p>

      <div className="space-y-3 rounded-lg border border-emerald-100 bg-emerald-50/50 p-4 text-sm text-emerald-950">
        <p>T7 ではこの画面で入力し、開始直後に Run 専用ページへ遷移します。現行は Playground から同じ定義 id で `ワークフロー開始` してください。</p>
        <p>
          <Link className="text-blue-800 font-medium underline hover:text-blue-950" href={playgroundHref(definitionId)}>
            Playground で定義 id を使って実行
          </Link>
        </p>
        <p>
          <Link className="text-sm text-zinc-700 underline hover:text-zinc-900" href={`/definitions/${encodeURIComponent(definitionId)}`}>
            定義の詳細へ戻る
          </Link>
        </p>
      </div>
    </main>
  );
}
