/**
 * ノード詳細に載せる JSON 風の値を整形する。
 */
export function formatTracePayload(value: unknown): string {
  if (value === undefined) return "";
  if (value === null) return "null";
  if (typeof value === "object") return JSON.stringify(value, null, 2);
  if (typeof value === "string") return value;
  if (typeof value === "number" || typeof value === "boolean" || typeof value === "bigint") return `${value}`;
  if (typeof value === "symbol") return value.description ?? "Symbol";
  if (typeof value === "function") return value.name || "function";
  return "";
}
