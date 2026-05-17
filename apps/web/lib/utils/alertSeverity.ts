export const SEVERITY_BADGE: Record<string, { bg: string; text: string; border: string; labelKey: string }> = {
  info:     { bg: "bg-blue-50",  text: "text-blue-700",  border: "border-blue-200",  labelKey: "info" },
  warning:  { bg: "bg-amber-50", text: "text-amber-700", border: "border-amber-200", labelKey: "warning" },
  critical: { bg: "bg-red-50",   text: "text-red-700",   border: "border-red-200",   labelKey: "critical" },
};

export function getSeverityBadge(severity: string) {
  return SEVERITY_BADGE[severity.toLowerCase()] ?? SEVERITY_BADGE["info"];
}
