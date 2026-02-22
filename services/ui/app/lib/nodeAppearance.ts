export type NodeAppearance = {
  label: string;
  icon: string;
  shapeClass: string;
  diamond: boolean;
};

function normalizeNodeType(nodeType: string): string {
  return nodeType.trim().toUpperCase();
}

export function getNodeAppearance(nodeType: string): NodeAppearance {
  const normalized = normalizeNodeType(nodeType);
  switch (normalized) {
    case "START":
      return { label: "START", icon: "▶", shapeClass: "rounded-none", diamond: false };
    case "SUCCESS":
    case "SUCCEEDED":
    case "COMPLETED":
      return { label: "SUCCESS", icon: "✓", shapeClass: "rounded-none", diamond: false };
    case "FAILED":
      return { label: "FAILED", icon: "⚠", shapeClass: "rounded-2xl", diamond: false };
    case "CANCELED":
      return { label: "CANCELED", icon: "✕", shapeClass: "rounded-2xl", diamond: false };
    case "WAIT":
    case "WAITING":
      return { label: "WAIT", icon: "⏸", shapeClass: "rounded-[22px]", diamond: false };
    case "FORK":
      return { label: "FORK", icon: "⇄", shapeClass: "rounded-2xl", diamond: false };
    case "JOIN":
      return { label: "JOIN", icon: "⇅", shapeClass: "rounded-2xl", diamond: true };
    case "TASK":
      return { label: "TASK", icon: "▢", shapeClass: "rounded-2xl", diamond: false };
    default:
      return { label: normalized || "TASK", icon: "▢", shapeClass: "rounded-2xl", diamond: false };
  }
}

