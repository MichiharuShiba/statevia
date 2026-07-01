/** ノード形状の種別（React Flow 描画用）。 */
export type NodeShapeKind = "stadium" | "roundedRect" | "gatewayFork" | "gatewayJoin";

/** ノード種別ごとの見た目属性。 */
export type NodeAppearance = {
  label: string;
  icon: string;
  shapeKind: NodeShapeKind;
};

function normalizeNodeType(nodeType: string): string {
  return nodeType.trim().toUpperCase();
}

/** nodeType からノード見た目を返す。 */
export function getNodeAppearance(nodeType: string): NodeAppearance {
  const normalized = normalizeNodeType(nodeType);
  switch (normalized) {
    case "START":
      return { label: "START", icon: "▶", shapeKind: "stadium" };
    case "END":
      return { label: "END", icon: "✓", shapeKind: "stadium" };
    case "SUCCESS":
    case "SUCCEEDED":
    case "COMPLETED":
      return { label: "SUCCESS", icon: "✓", shapeKind: "stadium" };
    case "FAILED":
      return { label: "FAILED", icon: "⚠", shapeKind: "stadium" };
    case "CANCELED":
      return { label: "CANCELED", icon: "✕", shapeKind: "stadium" };
    case "WAIT":
    case "WAITING":
      return { label: "WAIT", icon: "⏸", shapeKind: "roundedRect" };
    case "ACTION":
    case "TASK":
      return { label: normalized === "ACTION" ? "ACTION" : "TASK", icon: "▢", shapeKind: "roundedRect" };
    case "FORK":
      return { label: "FORK", icon: "⇄", shapeKind: "gatewayFork" };
    case "JOIN":
      return { label: "JOIN", icon: "⇅", shapeKind: "gatewayJoin" };
    default:
      return { label: normalized || "TASK", icon: "▢", shapeKind: "roundedRect" };
  }
}
