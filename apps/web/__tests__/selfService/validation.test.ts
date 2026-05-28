/**
 * Unit tests for self-service validation utilities.
 *
 * Feature: self-service-scheduling-ui
 * Task: 2.1 Create self-service validation utilities
 *
 * **Validates: Requirements 3.3, 4.3, 4.4, 6.4**
 */

import { describe, it, expect } from "vitest";
import {
  validateTemplateTimeRange,
  validateSelfServiceConfig,
  validateCancellationReason,
} from "../../lib/utils/selfServiceValidation";

describe("validateTemplateTimeRange", () => {
  it("returns valid when startTime is before endTime", () => {
    const result = validateTemplateTimeRange("08:00", "16:00");
    expect(result).toEqual({ valid: true });
  });

  it("returns invalid when startTime equals endTime", () => {
    const result = validateTemplateTimeRange("10:00", "10:00");
    expect(result.valid).toBe(false);
    expect(result.errorKey).toBe("selfService.errors.startTimeAfterEndTime");
  });

  it("returns invalid when startTime is after endTime", () => {
    const result = validateTemplateTimeRange("18:00", "08:00");
    expect(result.valid).toBe(false);
    expect(result.errorKey).toBe("selfService.errors.startTimeAfterEndTime");
  });

  it("handles HH:mm:ss format correctly", () => {
    const result = validateTemplateTimeRange("08:00:00", "16:00:00");
    expect(result).toEqual({ valid: true });
  });

  it("rejects when start equals end in HH:mm:ss format", () => {
    const result = validateTemplateTimeRange("12:30:00", "12:30:00");
    expect(result.valid).toBe(false);
  });
});

describe("validateSelfServiceConfig", () => {
  const validConfig = {
    minShiftsPerCycle: 2,
    maxShiftsPerCycle: 5,
    requestWindowOpenOffsetHours: 48,
    requestWindowCloseOffsetHours: 12,
    cancellationCutoffHours: 24,
    waitlistOfferMinutes: 60,
    cycleDurationDays: 7,
  };

  it("returns valid for a correct configuration", () => {
    const result = validateSelfServiceConfig(validConfig);
    expect(result).toEqual({ valid: true });
  });

  it("rejects when minShiftsPerCycle > maxShiftsPerCycle", () => {
    const result = validateSelfServiceConfig({
      ...validConfig,
      minShiftsPerCycle: 10,
      maxShiftsPerCycle: 5,
    });
    expect(result.valid).toBe(false);
    expect(result.errorKey).toBe("selfService.errors.minExceedsMax");
  });

  it("allows minShiftsPerCycle equal to maxShiftsPerCycle", () => {
    const result = validateSelfServiceConfig({
      ...validConfig,
      minShiftsPerCycle: 5,
      maxShiftsPerCycle: 5,
    });
    expect(result).toEqual({ valid: true });
  });

  it("rejects minShiftsPerCycle below 0", () => {
    const result = validateSelfServiceConfig({
      ...validConfig,
      minShiftsPerCycle: -1,
    });
    expect(result.valid).toBe(false);
    expect(result.errorKey).toBe("selfService.errors.minShiftsOutOfRange");
  });

  it("rejects minShiftsPerCycle above 100", () => {
    const result = validateSelfServiceConfig({
      ...validConfig,
      minShiftsPerCycle: 101,
      maxShiftsPerCycle: 101,
    });
    expect(result.valid).toBe(false);
    expect(result.errorKey).toBe("selfService.errors.minShiftsOutOfRange");
  });

  it("allows minShiftsPerCycle of 0", () => {
    const result = validateSelfServiceConfig({
      ...validConfig,
      minShiftsPerCycle: 0,
    });
    expect(result).toEqual({ valid: true });
  });

  it("rejects maxShiftsPerCycle below 1", () => {
    const result = validateSelfServiceConfig({
      ...validConfig,
      minShiftsPerCycle: 0,
      maxShiftsPerCycle: 0,
    });
    expect(result.valid).toBe(false);
    expect(result.errorKey).toBe("selfService.errors.maxShiftsOutOfRange");
  });

  it("rejects maxShiftsPerCycle above 100", () => {
    const result = validateSelfServiceConfig({
      ...validConfig,
      maxShiftsPerCycle: 101,
    });
    expect(result.valid).toBe(false);
    expect(result.errorKey).toBe("selfService.errors.maxShiftsOutOfRange");
  });

  it("rejects requestWindowOpenOffsetHours below 1", () => {
    const result = validateSelfServiceConfig({
      ...validConfig,
      requestWindowOpenOffsetHours: 0,
    });
    expect(result.valid).toBe(false);
    expect(result.errorKey).toBe("selfService.errors.openOffsetOutOfRange");
  });

  it("rejects requestWindowOpenOffsetHours above 720", () => {
    const result = validateSelfServiceConfig({
      ...validConfig,
      requestWindowOpenOffsetHours: 721,
    });
    expect(result.valid).toBe(false);
    expect(result.errorKey).toBe("selfService.errors.openOffsetOutOfRange");
  });

  it("rejects requestWindowCloseOffsetHours below 1", () => {
    const result = validateSelfServiceConfig({
      ...validConfig,
      requestWindowCloseOffsetHours: 0,
    });
    expect(result.valid).toBe(false);
    expect(result.errorKey).toBe("selfService.errors.closeOffsetOutOfRange");
  });

  it("rejects requestWindowCloseOffsetHours above 720", () => {
    const result = validateSelfServiceConfig({
      ...validConfig,
      requestWindowCloseOffsetHours: 721,
    });
    expect(result.valid).toBe(false);
    expect(result.errorKey).toBe("selfService.errors.closeOffsetOutOfRange");
  });

  it("rejects cancellationCutoffHours below 1", () => {
    const result = validateSelfServiceConfig({
      ...validConfig,
      cancellationCutoffHours: 0,
    });
    expect(result.valid).toBe(false);
    expect(result.errorKey).toBe("selfService.errors.cutoffOutOfRange");
  });

  it("rejects cancellationCutoffHours above 720", () => {
    const result = validateSelfServiceConfig({
      ...validConfig,
      cancellationCutoffHours: 721,
    });
    expect(result.valid).toBe(false);
    expect(result.errorKey).toBe("selfService.errors.cutoffOutOfRange");
  });

  it("rejects waitlistOfferMinutes below 1", () => {
    const result = validateSelfServiceConfig({
      ...validConfig,
      waitlistOfferMinutes: 0,
    });
    expect(result.valid).toBe(false);
    expect(result.errorKey).toBe("selfService.errors.waitlistOfferOutOfRange");
  });

  it("rejects waitlistOfferMinutes above 1440", () => {
    const result = validateSelfServiceConfig({
      ...validConfig,
      waitlistOfferMinutes: 1441,
    });
    expect(result.valid).toBe(false);
    expect(result.errorKey).toBe("selfService.errors.waitlistOfferOutOfRange");
  });

  it("rejects cycleDurationDays below 1", () => {
    const result = validateSelfServiceConfig({
      ...validConfig,
      cycleDurationDays: 0,
    });
    expect(result.valid).toBe(false);
    expect(result.errorKey).toBe("selfService.errors.cycleDurationOutOfRange");
  });

  it("rejects cycleDurationDays above 365", () => {
    const result = validateSelfServiceConfig({
      ...validConfig,
      cycleDurationDays: 366,
    });
    expect(result.valid).toBe(false);
    expect(result.errorKey).toBe("selfService.errors.cycleDurationOutOfRange");
  });

  it("accepts boundary values (min=0, max=100, offsets=720, waitlist=1440, cycle=365)", () => {
    const result = validateSelfServiceConfig({
      minShiftsPerCycle: 0,
      maxShiftsPerCycle: 100,
      requestWindowOpenOffsetHours: 720,
      requestWindowCloseOffsetHours: 720,
      cancellationCutoffHours: 720,
      waitlistOfferMinutes: 1440,
      cycleDurationDays: 365,
    });
    expect(result).toEqual({ valid: true });
  });

  it("accepts minimum boundary values (min=0, max=1, offsets=1, waitlist=1, cycle=1)", () => {
    const result = validateSelfServiceConfig({
      minShiftsPerCycle: 0,
      maxShiftsPerCycle: 1,
      requestWindowOpenOffsetHours: 1,
      requestWindowCloseOffsetHours: 1,
      cancellationCutoffHours: 1,
      waitlistOfferMinutes: 1,
      cycleDurationDays: 1,
    });
    expect(result).toEqual({ valid: true });
  });
});

describe("validateCancellationReason", () => {
  it("returns valid for a normal reason", () => {
    const result = validateCancellationReason("I have a doctor appointment");
    expect(result).toEqual({ valid: true });
  });

  it("rejects an empty string", () => {
    const result = validateCancellationReason("");
    expect(result.valid).toBe(false);
    expect(result.errorKey).toBe("selfService.errors.cancellationReasonRequired");
  });

  it("rejects a whitespace-only string", () => {
    const result = validateCancellationReason("   ");
    expect(result.valid).toBe(false);
    expect(result.errorKey).toBe("selfService.errors.cancellationReasonRequired");
  });

  it("rejects a reason exceeding 500 characters", () => {
    const longReason = "a".repeat(501);
    const result = validateCancellationReason(longReason);
    expect(result.valid).toBe(false);
    expect(result.errorKey).toBe("selfService.errors.cancellationReasonTooLong");
  });

  it("accepts a reason of exactly 500 characters", () => {
    const reason = "a".repeat(500);
    const result = validateCancellationReason(reason);
    expect(result).toEqual({ valid: true });
  });

  it("accepts a single character reason", () => {
    const result = validateCancellationReason("x");
    expect(result).toEqual({ valid: true });
  });

  it("trims whitespace before checking length", () => {
    // 500 chars + surrounding whitespace should still be valid
    const reason = "  " + "a".repeat(500) + "  ";
    const result = validateCancellationReason(reason);
    expect(result).toEqual({ valid: true });
  });
});
