import Link from "next/link";

type DefinitionDetailPageProps = {
  params: Promise<{
    definitionId: string;
  }>;
};

/**
 * Definition 詳細ページ（T4 で内容を拡張予定）。
 */
export default async function DefinitionDetailPage({ params }: Readonly<DefinitionDetailPageProps>) {
  const { definitionId } = await params;

  return (
    <main className="mx-auto flex max-w-3xl flex-col gap-5 p-6">
      <header className="space-y-1">
        <h1 className="text-xl font-semibold text-zinc-900">Definition 詳細</h1>
        <p className="text-sm text-zinc-600">
          displayId: <span className="font-mono">{definitionId}</span>
        </p>
      </header>

      <section className="rounded-lg border border-zinc-200 bg-white p-4 text-sm text-zinc-700">
        このページは T4 でメタ情報・関連 Workflow 導線・編集/実行開始導線を追加予定です。
      </section>

      <nav className="flex flex-wrap gap-3 text-sm">
        <Link href="/definitions" className="text-blue-700 underline hover:text-blue-900">
          Definition 一覧へ戻る
        </Link>
        <Link href="/playground" className="text-blue-700 underline hover:text-blue-900">
          Playground
        </Link>
      </nav>
    </main>
  );
}
