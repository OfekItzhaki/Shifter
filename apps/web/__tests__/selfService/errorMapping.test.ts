/**
 * Unit tests for self-service error code mapping utility.
 *
 * Feature: self-service-scheduling-ui
 * Task: 3.2 Create error code mapping utility
 *
 * **Validates: Requirements 10.5, 12.1, 12.2**
 */

import { describe, it, expect } from "vitest";
import {
  getSelfServiceErrorMessage,
  getErrorI18nKey,
  GENERIC_ERROR_KEY,
} from "../../lib/utils/selfServiceErrors";

describe("getSelfServiceErrorMessage", () => {
  describe("422 ProblemDetails with detail field", () => {
    it("returns the detail directly for 422 responses", () => {
      const error = {
        response: {
          status: 422,
          data: {
            type: "https://api.shifter.co.il/errors/shift-request-rejected",
            title: "Unprocessable Entity",
            status: 422,
            detail: "המשמרת מלאה. לא ניתן להגיש בקשה.",
          },
        },
      };

      const result = getSelfServiceErrorMessage(error);
      expect(result.message).toBe("המשמרת מלאה. לא ניתן להגיש בקשה.");
      expect(result.isI18nKey).toBe(false);
    });

    it("returns detail directly even for unknown type slugs on 422", () => {
      const error = {
        response: {
          status: 422,
          data: {
            type: "https://api.shifter.co.il/errors/some-unknown-error",
            title: "Unprocessable Entity",
            status: 422,
            detail: "שגיאה מותאמת אישית מהשרת",
          },
        },
      };

      const result = getSelfServiceErrorMessage(error);
      expect(result.message).toBe("שגיאה מותאמת אישית מהשרת");
      expect(result.isI18nKey).toBe(false);
    });
  });

  describe("known error type slugs", () => {
    it("maps shift-request-rejected to the correct i18n key", () => {
      const error = {
        response: {
          status: 400,
          data: {
            type: "https://api.shifter.co.il/errors/shift-request-rejected",
            title: "Bad Request",
            status: 400,
          },
        },
      };

      const result = getSelfServiceErrorMessage(error);
      expect(result.message).toBe("selfService.errors.requestRejected");
      expect(result.isI18nKey).toBe(true);
    });

    it("maps waitlist-rejected to the correct i18n key", () => {
      const error = {
        response: {
          status: 400,
          data: {
            type: "https://api.shifter.co.il/errors/waitlist-rejected",
            title: "Bad Request",
            status: 400,
          },
        },
      };

      const result = getSelfServiceErrorMessage(error);
      expect(result.message).toBe("selfService.errors.waitlistRejected");
      expect(result.isI18nKey).toBe(true);
    });

    it("maps absence, change, and special-leave rejection slugs to fallback keys", () => {
      expect(getSelfServiceErrorMessage({
        response: {
          status: 400,
          data: {
            type: "https://api.shifter.co.il/errors/shift-absence-rejected",
            title: "Bad Request",
            status: 400,
          },
        },
      }).message).toBe("selfService.errors.absenceRejected");

      expect(getSelfServiceErrorMessage({
        response: {
          status: 400,
          data: {
            type: "https://api.shifter.co.il/errors/shift-change-request-rejected",
            title: "Bad Request",
            status: 400,
          },
        },
      }).message).toBe("selfService.errors.changeRequestRejected");

      expect(getSelfServiceErrorMessage({
        response: {
          status: 400,
          data: {
            type: "https://api.shifter.co.il/errors/special-leave-request-rejected",
            title: "Bad Request",
            status: 400,
          },
        },
      }).message).toBe("selfService.errors.specialLeaveRejected");
    });

    it("maps forbidden to the correct i18n key", () => {
      const error = {
        response: {
          status: 403,
          data: {
            type: "https://api.shifter.co.il/errors/forbidden",
            title: "Forbidden",
            status: 403,
          },
        },
      };

      const result = getSelfServiceErrorMessage(error);
      expect(result.message).toBe("selfService.errors.permissionDenied");
      expect(result.isI18nKey).toBe(true);
    });

    it("maps not-found to the correct i18n key", () => {
      const error = {
        response: {
          status: 404,
          data: {
            type: "https://api.shifter.co.il/errors/not-found",
            title: "Not Found",
            status: 404,
          },
        },
      };

      const result = getSelfServiceErrorMessage(error);
      expect(result.message).toBe("selfService.errors.notFound");
      expect(result.isI18nKey).toBe(true);
    });
  });

  describe("non-422 with detail field", () => {
    it("returns detail directly for non-422 responses with unknown slug", () => {
      const error = {
        response: {
          status: 400,
          data: {
            type: "https://api.shifter.co.il/errors/unknown-slug",
            title: "Bad Request",
            status: 400,
            detail: "הודעת שגיאה ספציפית",
          },
        },
      };

      const result = getSelfServiceErrorMessage(error);
      expect(result.message).toBe("הודעת שגיאה ספציפית");
      expect(result.isI18nKey).toBe(false);
    });
  });

  describe("simple error response format", () => {
    it("extracts error from { error: string } response", () => {
      const error = {
        response: {
          status: 400,
          data: {
            error: "Member not in group",
          },
        },
      };

      const result = getSelfServiceErrorMessage(error);
      expect(result.message).toBe("Member not in group");
      expect(result.isI18nKey).toBe(false);
    });

    it("extracts message from { message: string } response", () => {
      const error = {
        response: {
          status: 400,
          data: {
            message: "Already assigned to this slot",
          },
        },
      };

      const result = getSelfServiceErrorMessage(error);
      expect(result.message).toBe("Already assigned to this slot");
      expect(result.isI18nKey).toBe(false);
    });
  });

  describe("fallback for unknown errors", () => {
    it("returns generic error key for errors without response data", () => {
      const error = new Error("Network error");

      const result = getSelfServiceErrorMessage(error);
      expect(result.message).toBe(GENERIC_ERROR_KEY);
      expect(result.isI18nKey).toBe(true);
    });

    it("returns generic error key for null/undefined errors", () => {
      const result = getSelfServiceErrorMessage(null);
      expect(result.message).toBe(GENERIC_ERROR_KEY);
      expect(result.isI18nKey).toBe(true);
    });

    it("returns generic error key for empty response data", () => {
      const error = {
        response: {
          status: 500,
          data: {},
        },
      };

      const result = getSelfServiceErrorMessage(error);
      expect(result.message).toBe(GENERIC_ERROR_KEY);
      expect(result.isI18nKey).toBe(true);
    });
  });
});

describe("getErrorI18nKey", () => {
  it("returns mapped key for known error codes", () => {
    expect(getErrorI18nKey("shift-request-rejected")).toBe("selfService.errors.requestRejected");
    expect(getErrorI18nKey("shift-absence-rejected")).toBe("selfService.errors.absenceRejected");
    expect(getErrorI18nKey("shift-change-request-rejected")).toBe("selfService.errors.changeRequestRejected");
    expect(getErrorI18nKey("special-leave-request-rejected")).toBe("selfService.errors.specialLeaveRejected");
    expect(getErrorI18nKey("waitlist-rejected")).toBe("selfService.errors.waitlistRejected");
    expect(getErrorI18nKey("slot-full")).toBe("selfService.errors.slotFull");
    expect(getErrorI18nKey("max-shifts-reached")).toBe("selfService.errors.maxShiftsReached");
    expect(getErrorI18nKey("cancellation-window-closed")).toBe("selfService.errors.cancellationWindowClosed");
    expect(getErrorI18nKey("swap-conflict")).toBe("selfService.errors.swapConflict");
    expect(getErrorI18nKey("swap-duplicate")).toBe("selfService.errors.swapDuplicate");
    expect(getErrorI18nKey("swap-invalid-ownership")).toBe("selfService.errors.swapInvalidOwnership");
    expect(getErrorI18nKey("forbidden")).toBe("selfService.errors.permissionDenied");
    expect(getErrorI18nKey("not-found")).toBe("selfService.errors.notFound");
  });

  it("returns generic fallback key for unknown error codes", () => {
    expect(getErrorI18nKey("unknown-error")).toBe(GENERIC_ERROR_KEY);
    expect(getErrorI18nKey("")).toBe(GENERIC_ERROR_KEY);
    expect(getErrorI18nKey("some-random-slug")).toBe(GENERIC_ERROR_KEY);
  });
});
