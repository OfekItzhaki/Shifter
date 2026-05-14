"use client";

const BADGE_STYLES: Record<string, string> = {
  hard: "bg-red-50 text-red-700 border-red-200",
  normal: "bg-slate-100 text-slate-600 border-slate-200",
  easy: "bg-emerald-50 text-emerald-700 border-emerald-200",
};

const BADGE_LABELS: Record<string, string> = {
  hard: "קשה",
  normal: "רגיל",
  easy: "קל",
};

interface BurdenBadgeProps {
  level: "hard" | "normal" | "easy";
}

export default function BurdenBadge({ level }: BurdenBadgeProps) {
  const style = BADGE_STYLES[level] ?? BADGE_STYLES.normal;
  const label = BADGE_LABELS[level] ?? level;

  return (
    <span
      className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border ${style}`}
    >
      {label}
    </span>
  );
}
