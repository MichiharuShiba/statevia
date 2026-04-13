/** Playground 配下は幅を子レイアウトに任せる（実行ビューは run/layout で広げる） */
export default function PlaygroundLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <div className="w-full">{children}</div>;
}
