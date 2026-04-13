/** 実行ビューはグラフ列を広めに確保する */
export default function PlaygroundRunLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <div className="mx-auto max-w-[min(1600px,100%)] px-2 py-4">{children}</div>;
}
