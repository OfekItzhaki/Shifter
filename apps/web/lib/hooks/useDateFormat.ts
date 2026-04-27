import { useAuthStore } from "@/lib/store/authStore";
import {
  formatDate,
  formatDateLong,
  formatDateTime,
  formatDateTimeShort,
  formatTime,
  formatDateRange,
} from "@/lib/utils/dateFormat";

/**
 * Returns locale-aware date formatting functions.
 * Priority: stored preferredLocale → browser navigator.language → "he-IL"
 */
export function useDateFormat() {
  const locale = useAuthStore(s => s.preferredLocale);

  // Use browser locale if available and stored locale is the generic default
  const effectiveLocale = typeof window !== "undefined" && locale === "he"
    ? navigator.language || locale
    : locale;

  return {
    fDate:      (d: string | Date | null | undefined) => formatDate(d, effectiveLocale),
    fDateLong:  (d: string | Date | null | undefined) => formatDateLong(d, effectiveLocale),
    fDateTime:  (d: string | Date | null | undefined) => formatDateTime(d, effectiveLocale),
    fDateShort: (d: string | Date | null | undefined) => formatDateTimeShort(d, effectiveLocale),
    fTime:      (d: string | Date | null | undefined) => formatTime(d, effectiveLocale),
    fRange:     (from: string | Date | null | undefined, to: string | Date | null | undefined) =>
                  formatDateRange(from, to, effectiveLocale),
    locale: effectiveLocale,
  };
}
