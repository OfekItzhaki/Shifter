/**
 * Client-side validation utilities for self-service scheduling forms.
 *
 * Each function returns a ValidationResult with a boolean `valid` flag
 * and an optional `errorKey` containing an i18n message key for display.
 *
 * These validations run before API calls to provide instant feedback.
 */

export interface ValidationResult {
  valid: boolean;
  errorKey?: string;
}

/**
 * Validates that a shift template's start time is strictly before its end time.
 *
 * @param startTime - Start time string in "HH:mm" or "HH:mm:ss" format
 * @param endTime - End time string in "HH:mm" or "HH:mm:ss" format
 * @returns ValidationResult — invalid if startTime >= endTime
 */
export function validateTemplateTimeRange(
  startTime: string,
  endTime: string
): ValidationResult {
  if (startTime >= endTime) {
    return { valid: false, errorKey: "selfService.errors.startTimeAfterEndTime" };
  }
  return { valid: true };
}

/**
 * Validates self-service configuration fields against allowed ranges.
 *
 * Rules:
 * - minShiftsPerCycle: 0–100
 * - maxShiftsPerCycle: 1–100
 * - minShiftsPerCycle must be <= maxShiftsPerCycle
 * - requestWindowOpenOffsetHours: 1–720
 * - requestWindowCloseOffsetHours: 1–720
 * - requestWindowOpenOffsetHours must be greater than requestWindowCloseOffsetHours
 * - cancellationCutoffHours: 1–720
 * - waitlistOfferMinutes: 15–1440
 * - cycleDurationDays: 1–30
 *
 * @param config - The configuration object to validate
 * @returns ValidationResult — invalid with appropriate errorKey if any field is out of range
 */
export function validateSelfServiceConfig(config: {
  minShiftsPerCycle: number;
  maxShiftsPerCycle: number;
  requestWindowOpenOffsetHours: number;
  requestWindowCloseOffsetHours: number;
  cancellationCutoffHours: number;
  maxAbsencesPerCycle?: number;
  maxLateCancellationsPerCycle?: number;
  lateCancellationWindowHours?: number;
  waitlistOfferMinutes: number;
  cycleDurationDays: number;
}): ValidationResult {
  const {
    minShiftsPerCycle,
    maxShiftsPerCycle,
    requestWindowOpenOffsetHours,
    requestWindowCloseOffsetHours,
    cancellationCutoffHours,
    maxAbsencesPerCycle = 3,
    maxLateCancellationsPerCycle = 2,
    lateCancellationWindowHours = 24,
    waitlistOfferMinutes,
    cycleDurationDays,
  } = config;

  // minShiftsPerCycle: 0–100
  if (minShiftsPerCycle < 0 || minShiftsPerCycle > 100) {
    return { valid: false, errorKey: "selfService.errors.minShiftsOutOfRange" };
  }

  // maxShiftsPerCycle: 1–100
  if (maxShiftsPerCycle < 1 || maxShiftsPerCycle > 100) {
    return { valid: false, errorKey: "selfService.errors.maxShiftsOutOfRange" };
  }

  // min must be <= max
  if (minShiftsPerCycle > maxShiftsPerCycle) {
    return { valid: false, errorKey: "selfService.errors.minExceedsMax" };
  }

  // requestWindowOpenOffsetHours: 1–720
  if (requestWindowOpenOffsetHours < 1 || requestWindowOpenOffsetHours > 720) {
    return { valid: false, errorKey: "selfService.errors.openOffsetOutOfRange" };
  }

  // requestWindowCloseOffsetHours: 1–720
  if (requestWindowCloseOffsetHours < 1 || requestWindowCloseOffsetHours > 720) {
    return { valid: false, errorKey: "selfService.errors.closeOffsetOutOfRange" };
  }

  // cancellationCutoffHours: 1–720
  if (requestWindowOpenOffsetHours <= requestWindowCloseOffsetHours) {
    return { valid: false, errorKey: "selfService.errors.openOffsetMustBeGreaterThanClose" };
  }

  if (cancellationCutoffHours < 1 || cancellationCutoffHours > 720) {
    return { valid: false, errorKey: "selfService.errors.cutoffOutOfRange" };
  }

  if (maxLateCancellationsPerCycle < 0 || maxLateCancellationsPerCycle > 100) {
    return { valid: false, errorKey: "selfService.errors.maxLateCancellationsOutOfRange" };
  }

  if (maxAbsencesPerCycle < 0 || maxAbsencesPerCycle > 100) {
    return { valid: false, errorKey: "selfService.errors.maxAbsencesOutOfRange" };
  }

  if (lateCancellationWindowHours < 1 || lateCancellationWindowHours > 720) {
    return { valid: false, errorKey: "selfService.errors.lateCancellationWindowOutOfRange" };
  }

  // waitlistOfferMinutes: 15–1440
  if (waitlistOfferMinutes < 15 || waitlistOfferMinutes > 1440) {
    return { valid: false, errorKey: "selfService.errors.waitlistOfferOutOfRange" };
  }

  // cycleDurationDays: 1–30
  if (cycleDurationDays < 1 || cycleDurationDays > 30) {
    return { valid: false, errorKey: "selfService.errors.cycleDurationOutOfRange" };
  }

  return { valid: true };
}

/**
 * Validates a cancellation reason string.
 *
 * Rules:
 * - Must not be empty (after trimming)
 * - Must not exceed 500 characters
 *
 * @param reason - The cancellation reason text
 * @returns ValidationResult — invalid if empty or exceeds 500 characters
 */
export function validateCancellationReason(reason: string): ValidationResult {
  const trimmed = reason.trim();

  if (trimmed.length === 0) {
    return { valid: false, errorKey: "selfService.errors.cancellationReasonRequired" };
  }

  if (trimmed.length > 500) {
    return { valid: false, errorKey: "selfService.errors.cancellationReasonTooLong" };
  }

  return { valid: true };
}
