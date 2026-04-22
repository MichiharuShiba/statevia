import Link from "next/link";

type DefinitionEditPlaceholderPageProps = {
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
 * 定義編集のプレースホルダ。T10 で専用 DefinitionEditor に拡張する。現行は Playground への導線を提供する。
 */
export default async function DefinitionEditPlaceholderPage({ params }: Readonly<DefinitionEditPlaceholderPageProps>) {
  const { definitionId } = await params;

  return (
    <main className="mx-auto flex max-w-2xl flex-col gap-5 p-6">
      <h1 className="text-xl font-semibold text-zinc-900">定義の編集</h1>
      <p className="text-sm text-zinc-600">displayId: <span className="font-mono break-all">{definitionId}</span></p>

      <div className="space-y-3 rounded-lg border border-zinc-200 bg-zinc-50/80 p-4 text-sm text-zinc-800">
        <p>専用の YAML エディタ・検証（T10）は次タスクで実装予定です。今は既存の Playground で、definitionId のプリフィル込みの編集と実行の開始ができます。</p>
        <ul className="list-inside list-disc space-y-1">
          <li>
            <Link className="text-blue-700 underline hover:text-blue-900" href={playgroundHref(definitionId)}>
              Playground で定義名・YAML を登録 / 再登録
            </Link>
          </li>
          <li>
            <Link className="text-blue-700 underline hover:text-blue-900" href={`/definitions/${encodeURIComponent(definitionId)}`}>
              定義の詳細へ戻る
            </Link>
          </li>
        </ul>
      </div>
    </main>
  );
}
