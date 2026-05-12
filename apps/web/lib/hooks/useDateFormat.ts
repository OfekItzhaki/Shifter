import { useAuthStore } from "@/lib/store/authStore";
import {
  formatDate,
  formatDateLong,
  formatDateTime,
  formatDateTimeShort,
  formatTime,
  formatDateRange,
} from "@/lib/utils/dateFormat";
import type { TimeFormatOption } from "@/lib/utils/dateFormat";

/**
 * Returns locale-aware date formatting functions.
 * Priority: stored preferredLocale → browser navigator.language → "he-IL"
 * Respects the user's 24h/12h time format preference.
 */
export function useDateFormat() {
  const locale = useAuthStore(s => s.preferredLocale);
  const timeFormat: TimeFormatOption = useAuthStore(s => s.timeFormat);

  // Use browser locale if available and stored locale is the generic default
  const effectiveLocale = typeof window !== "undefined" && locale === "he"
    ? navigator.language || locale
    : locale;

  return {
    fDate:      (d: string | Date | null | undefined) => formatDate(d, effectiveLocale, timeFormat),
    fDateLong:  (d: string | Date | null | undefined) => formatDateLong(d, effectiveLocale, timeFormat),
    fDateTime:  (d: string | Date | null | undefined) => formatDateTime(d, effectiveLocale, timeFormat),
    fDateShort: (d: string | Date | null | undefined) => formatDateTimeShort(d, effectiveLocale, timeFormat),
    fTime:      (d: string | Date | null | undefined) => formatTime(d, effectiveLocale, timeFormat),
    fRange:     (from: string | Date | null | undefined, to: string | Date | null | undefined) =>
                  formatDateRange(from, to, effectiveLocale, timeFormat),
    locale: effectiveLocale,
    timeFormat,
  };
}
