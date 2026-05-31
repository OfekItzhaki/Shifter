/**
 * Rest duration computation utilities.
 *
 * Pure functions that compute the time gap between a person's consecutive
 * assignments, format the result for display, and determine color-coding
 * based on a minimum rest threshold.
 *
 * All timestamp arithmetic uses UTC ISO 8601 strings directly —
 * no timezone conversion is needed for gap computation since both
 * endpoints are in the same timezone (UTC).
 */

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface RestDurationInput {
  personId: string;
  slotStartsAt: string;
  slotEndsAt: string;
}

export interface RestDurationEntry {
  personId: string;
  slotStartsAt: string;
  slotEndsAt: string;
  nextSlotStartsAt: string;
  restHours: number;
}

export type SupportedLocale = "en" | "he" | "ru";

// ---------------------------------------------------------------------------
// computeRestDurations
// ---------------------------------------------------------------------------

/**
 * Computes rest durations between consecutive assignments for each person.
 * Considers ALL assignments across task types.
 *
 * - Groups assignments by `personId`
 * - Sorts each group chronologically by `slotStartsAt` (ascending)
 * - Computes the gap between each consecutive pair
 * - Skips assignments with missing/invalid personId, slotStartsAt, or slotEndsAt
 * - Clamps negative gaps (overlapping assignments) to 0
 *
 * Returns one entry per assignment that has a subsequent assignment for the same person.
 */
export function computeRestDurations(
  assignments: RestDurationInput[]
): RestDurationEntry[] {
  // Group valid assignments by personId
  const grouped = new Map<string, RestDurationInput[]>();

  for (const assignment of assignments) {
    // Skip assignments with missing/invalid fields
    if (!assignment.personId || !assignment.slotStartsAt || !assignment.slotEndsAt) {
      continue;
    }

    const startMs = new Date(assignment.slotStartsAt).getTime();
    const endMs = new Date(assignment.slotEndsAt).getTime();
    if (isNaN(startMs) || isNaN(endMs)) {
      continue;
    }

    const group = grouped.get(assignment.personId);
    if (group) {
      group.push(assignment);
    } else {
      grouped.set(assignment.personId, [assignment]);
    }
  }

  const results: RestDurationEntry[] = [];

  for (const [personId, personAssignments] of grouped) {
    // Sort by slotStartsAt ascending (ISO 8601 string comparison is safe)
    personAssignments.sort((a, b) => a.slotStartsAt.localeCompare(b.slotStartsAt));

    // Compute gap for each consecutive pair
    for (let i = 0; i < personAssignments.length - 1; i++) {
      const current = personAssignments[i];
      const next = personAssignments[i + 1];

      const currentEndMs = new Date(current.slotEndsAt).getTime();
      const nextStartMs = new Date(next.slotStartsAt).getTime();

      const gapMs = nextStartMs - currentEndMs;
      const restHours = Math.max(gapMs / 3_600_000, 0);

      results.push({
        personId,
        slotStartsAt: current.slotStartsAt,
        slotEndsAt: current.slotEndsAt,
        nextSlotStartsAt: next.slotStartsAt,
        restHours,
      });
    }
  }

  return results;
}

// ---------------------------------------------------------------------------
// formatRestDuration
// ---------------------------------------------------------------------------

const LOCALE_ABBREVIATIONS: Record<SupportedLocale, { hours: string; days: string }> = {
  en: { hours: "h", days: "d" },
  he: { hours: "ש", days: "י" },
  ru: { hours: "ч", days: "д" },
};

/**
 * Formats a rest duration in hours into a localized string.
 *
 * - >= 24h: "1d 4h" / "1י 4ש" / "1д 4ч"
 * - < 24h: "8h" / "8ש" / "8ч"
 *
 * Uses Math.floor for integer display values.
 */
export function formatRestDuration(
  hours: number,
  locale: SupportedLocale
): string {
  const abbrev = LOCALE_ABBREVIATIONS[locale];
  const totalHoursFloored = Math.floor(hours);

  if (hours >= 24) {
    const days = Math.floor(hours / 24);
    const remainingHours = Math.floor(hours % 24);
    return `${days}${abbrev.days} ${remainingHours}${abbrev.hours}`;
  }

  return `${totalHoursFloored}${abbrev.hours}`;
}

// ---------------------------------------------------------------------------
// getRestColorClass
// ---------------------------------------------------------------------------

/**
 * Returns the Tailwind color class based on rest vs threshold comparison.
 *
 * - Below threshold → red (fatigue risk)
 * - Exactly at threshold → amber (borderline)
 * - Above threshold → neutral slate
 */
export function getRestColorClass(
  restHours: number,
  minRestThresholdHours: number
): string {
  if (restHours < minRestThresholdHours) {
    return "text-red-600";
  }
  if (restHours === minRestThresholdHours) {
    return "text-amber-600";
  }
  return "text-slate-500";
}
