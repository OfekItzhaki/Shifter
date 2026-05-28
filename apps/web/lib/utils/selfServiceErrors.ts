/**
 * Self-service scheduling error code mapping utility.
 *
 * Maps backend ProblemDetails error type slugs to i18n keys under
 * the `selfService.errors.*` namespace. For 422 responses with a
 * `detail` field, the detail is displayed directly (it's already
 * a human-readable message from the backend). The i18n fallback
 * is used only when no specific mapping or detail is available.
 */

/**
 * Mapping from backend error type slugs to i18n keys.
 * The type slug is extracted from the ProblemDetails `type` field
 * (the last segment of the URI, e.g. "shift-request-rejected").
 */
const ERROR_CODE_MAP: Record<string, string> = {
  "shift-request-rejected": "selfService.errors.requestRejected",
  "waitlist-rejected": "selfService.errors.waitlistRejected",
  "slot-full": "selfService.errors.slotFull",
  "max-shifts-reached": "selfService.errors.maxShiftsReached",
  "cancellation-window-closed": "selfService.errors.cancellationWindowClosed",
  "swap-conflict": "selfService.errors.swapConflict",
  "swap-duplicate": "selfService.errors.swapDuplicate",
  "swap-invalid-ownership": "selfService.errors.swapInvalidOwnership",
  "forbidden": "selfService.errors.permissionDenied",
  "not-found": "selfService.errors.notFound",
};

/** The generic fallback i18n key for unknown error codes */
export const GENERIC_ERROR_KEY = "selfService.errors.generic";

/**
 * Shape of an API error response following RFC 7807 ProblemDetails.
 * This matches the structure returned by the backend's ExceptionHandlingMiddleware.
 */
export interface ProblemDetailsError {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  extensions?: Record<string, unknown>;
}

/**
 * Extracts the error type slug from a ProblemDetails `type` URI.
 * e.g. "https://api.shifter.co.il/errors/shift-request-rejected" → "shift-request-rejected"
 */
function extractTypeSlug(type: string | undefined): string | null {
  if (!type) return null;
  const segments = type.split("/");
  return segments[segments.length - 1] || null;
}

/**
 * Extracts a ProblemDetails body from an Axios error response.
 * Returns null if the error doesn't contain a structured response.
 */
function extractProblemDetails(
  error: unknown
): ProblemDetailsError | null {
  const axiosErr = error as {
    response?: { status?: number; data?: ProblemDetailsError };
  };
  if (!axiosErr?.response?.data) return null;
  return axiosErr.response.data;
}

export interface SelfServiceErrorResult {
  /** The message to display — either a direct detail string or an i18n key */
  message: string;
  /** Whether `message` is an i18n key (true) or a direct display string (false) */
  isI18nKey: boolean;
}

/**
 * Get the appropriate error message for a self-service API error.
 *
 * Resolution order:
 * 1. For 422 ProblemDetails responses with a `detail` field → return detail directly
 * 2. For known error type slugs → return the mapped i18n key
 * 3. For unknown errors → return the generic Hebrew fallback i18n key
 *
 * @param error - The caught error from an API call (typically an Axios error)
 * @returns An object with the message string and whether it's an i18n key
 */
export function getSelfServiceErrorMessage(error: unknown): SelfServiceErrorResult {
  const problem = extractProblemDetails(error);

  // If we have a structured ProblemDetails response
  if (problem) {
    const status = problem.status ?? (error as { response?: { status?: number } })?.response?.status;

    // For 422 responses with a detail field, display the detail directly
    // (the backend provides human-readable Hebrew messages for domain validation errors)
    if (status === 422 && problem.detail) {
      return { message: problem.detail, isI18nKey: false };
    }

    // Try to map the error type slug to an i18n key
    const slug = extractTypeSlug(problem.type);
    if (slug && ERROR_CODE_MAP[slug]) {
      return { message: ERROR_CODE_MAP[slug], isI18nKey: true };
    }

    // If there's a detail field on non-422 responses, still use it directly
    if (problem.detail) {
      return { message: problem.detail, isI18nKey: false };
    }
  }

  // Check for simple { error: "..." } response format (used by some controllers)
  const simpleErr = error as {
    response?: { data?: { error?: string; message?: string } };
  };
  if (simpleErr?.response?.data?.error) {
    return { message: simpleErr.response.data.error, isI18nKey: false };
  }
  if (simpleErr?.response?.data?.message) {
    return { message: simpleErr.response.data.message, isI18nKey: false };
  }

  // Fallback: generic error i18n key
  return { message: GENERIC_ERROR_KEY, isI18nKey: true };
}

/**
 * Get the i18n key for a known error code slug.
 * Returns the generic fallback key for unknown codes.
 *
 * @param errorCode - The error type slug (e.g. "shift-request-rejected")
 * @returns The corresponding i18n key
 */
export function getErrorI18nKey(errorCode: string): string {
  return ERROR_CODE_MAP[errorCode] ?? GENERIC_ERROR_KEY;
}
