/**
 * Unit tests for InactivityTimer module.
 *
 * Tests core functionality: start, reset, stop, tick countdown,
 * visibility change reconciliation, and activity listener registration.
 *
 * Requirements: 5.1, 5.2, 5.3, 5.4, 5.6
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { InactivityTimer } from "../../lib/session/inactivityTimer";

describe("InactivityTimer", () => {
  let timer: InactivityTimer;
  let onTick: ReturnType<typeof vi.fn<(remainingMs: number) => void>>;
  let onTimeout: ReturnType<typeof vi.fn<() => void>>;

  beforeEach(() => {
    vi.useFakeTimers();
    timer = new InactivityTimer();
    onTick = vi.fn<(remainingMs: number) => void>();
    onTimeout = vi.fn<() => void>();
  });

  afterEach(() => {
    timer.stop();
    vi.useRealTimers();
  });

  describe("start()", () => {
    it("initializes with the given timeout duration", () => {
      timer.start(60_000, { onTick, onTimeout });

      expect(timer.isRunning()).toBe(true);
      expect(timer.getRemainingMs()).toBe(60_000);
    });

    it("registers activity listeners on document", () => {
      const addSpy = vi.spyOn(document, "addEventListener");

      timer.start(60_000, { onTick, onTimeout });

      // click, keypress, scroll + visibilitychange = 4 listeners
      expect(addSpy).toHaveBeenCalledWith("click", expect.any(Function), { passive: true });
      expect(addSpy).toHaveBeenCalledWith("keypress", expect.any(Function), { passive: true });
      expect(addSpy).toHaveBeenCalledWith("scroll", expect.any(Function), { passive: true });
      expect(addSpy).toHaveBeenCalledWith("visibilitychange", expect.any(Function));

      addSpy.mockRestore();
    });

    it("stops any existing timer before starting a new one", () => {
      timer.start(60_000, { onTick, onTimeout });
      timer.start(30_000, { onTick, onTimeout });

      expect(timer.getRemainingMs()).toBe(30_000);
    });
  });

  describe("tick countdown", () => {
    it("calls onTick every second with decreasing remaining time", () => {
      timer.start(5_000, { onTick, onTimeout });

      vi.advanceTimersByTime(1_000);
      expect(onTick).toHaveBeenCalledWith(4_000);

      vi.advanceTimersByTime(1_000);
      expect(onTick).toHaveBeenCalledWith(3_000);
    });

    it("calls onTimeout when remaining reaches zero", () => {
      timer.start(3_000, { onTick, onTimeout });

      vi.advanceTimersByTime(3_000);

      expect(onTimeout).toHaveBeenCalledTimes(1);
    });

    it("stops the interval after timeout fires", () => {
      timer.start(2_000, { onTick, onTimeout });

      vi.advanceTimersByTime(2_000);
      expect(timer.isRunning()).toBe(false);

      // No more ticks after timeout
      onTick.mockClear();
      vi.advanceTimersByTime(5_000);
      expect(onTick).not.toHaveBeenCalled();
    });
  });

  describe("reset()", () => {
    it("resets remaining time to full timeout duration", () => {
      timer.start(10_000, { onTick, onTimeout });

      vi.advanceTimersByTime(5_000);
      timer.reset();

      expect(timer.getRemainingMs()).toBe(10_000);
    });

    it("does nothing if timer is not running", () => {
      timer.reset(); // Should not throw
      expect(timer.getRemainingMs()).toBe(0);
    });

    it("prevents timeout after reset", () => {
      timer.start(3_000, { onTick, onTimeout });

      vi.advanceTimersByTime(2_500);
      timer.reset();
      vi.advanceTimersByTime(2_500);

      expect(onTimeout).not.toHaveBeenCalled();
    });
  });

  describe("stop()", () => {
    it("stops the timer and clears state", () => {
      timer.start(60_000, { onTick, onTimeout });
      timer.stop();

      expect(timer.isRunning()).toBe(false);
      expect(timer.getRemainingMs()).toBe(0);
    });

    it("removes activity listeners from document", () => {
      const removeSpy = vi.spyOn(document, "removeEventListener");

      timer.start(60_000, { onTick, onTimeout });
      timer.stop();

      expect(removeSpy).toHaveBeenCalledWith("click", expect.any(Function));
      expect(removeSpy).toHaveBeenCalledWith("keypress", expect.any(Function));
      expect(removeSpy).toHaveBeenCalledWith("scroll", expect.any(Function));
      expect(removeSpy).toHaveBeenCalledWith("visibilitychange", expect.any(Function));

      removeSpy.mockRestore();
    });

    it("no more ticks after stop", () => {
      timer.start(60_000, { onTick, onTimeout });
      timer.stop();

      onTick.mockClear();
      vi.advanceTimersByTime(5_000);
      expect(onTick).not.toHaveBeenCalled();
    });
  });

  describe("reconcileAfterVisibilityChange()", () => {
    it("adjusts remaining time based on actual elapsed time", () => {
      timer.start(60_000, { onTick, onTimeout });

      // Simulate 30 seconds passing while tab was hidden
      vi.advanceTimersByTime(30_000);
      timer.reconcileAfterVisibilityChange();

      expect(timer.getRemainingMs()).toBe(30_000);
    });

    it("triggers timeout if elapsed time exceeds timeout duration", () => {
      timer.start(10_000, { onTick, onTimeout });

      // Simulate more time passing than the timeout
      vi.advanceTimersByTime(15_000);
      timer.reconcileAfterVisibilityChange();

      expect(onTimeout).toHaveBeenCalledTimes(1);
      expect(timer.getRemainingMs()).toBe(0);
    });

    it("does nothing if timer is not running", () => {
      timer.reconcileAfterVisibilityChange(); // Should not throw
      expect(onTimeout).not.toHaveBeenCalled();
    });

    it("calls onTick with updated remaining when tab becomes visible", () => {
      timer.start(60_000, { onTick, onTimeout });
      onTick.mockClear();

      vi.advanceTimersByTime(20_000);
      timer.reconcileAfterVisibilityChange();

      expect(onTick).toHaveBeenCalledWith(40_000);
    });
  });

  describe("activity event handling", () => {
    it("resets timer on click event", () => {
      timer.start(10_000, { onTick, onTimeout });

      vi.advanceTimersByTime(5_000);
      document.dispatchEvent(new Event("click"));

      // After click, remaining should be back to full
      expect(timer.getRemainingMs()).toBe(10_000);
    });

    it("resets timer on keypress event", () => {
      timer.start(10_000, { onTick, onTimeout });

      vi.advanceTimersByTime(5_000);
      document.dispatchEvent(new Event("keypress"));

      expect(timer.getRemainingMs()).toBe(10_000);
    });

    it("resets timer on scroll event", () => {
      timer.start(10_000, { onTick, onTimeout });

      vi.advanceTimersByTime(5_000);
      document.dispatchEvent(new Event("scroll"));

      expect(timer.getRemainingMs()).toBe(10_000);
    });
  });
});
