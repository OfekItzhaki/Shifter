"use client";

import { useLocale, useTranslations } from "next-intl";
import {
  formatRestDuration,
  getRestColorClass,
  type SupportedLocale,
} from "@/lib/utils/restDuration";

interface RestDurationBadgeProps {
  restHours: number;
  minRestThresholdHours: number;
}

/**
 * Inline badge showing the rest duration between consecutive assignments.
 * Displays a localized duration string (e.g. "8h rest") with color-coding
 * based on the group's minimum rest threshold.
 */
export default function RestDurationBadge({
  restHours,
  minRestThresholdHours,
}: RestDurationBadgeProps) {
  const locale = useLocale() as SupportedLocale;
  const t = useTranslations("schedule");

  const durationText = formatRestDuration(restHours, locale);
  const colorClass = getRestColorClass(restHours, minRestThresholdHours);
  const restLabel = t("rest.label");

  return (
    <span className={`text-[10px] font-normal ${colorClass}`}>
      {durationText} {restLabel}
    </span>
  );
}
