/**
 * InactivityTimer — client-side countdown timer for elevated mode sessions.
 *
 * Tracks elapsed time since the last meaningful user interaction and fires
 * callbacks when the timer ticks, when the prompt should appear (timeout),
 * and when the session should be terminated.
 *
 * Uses setInterval (1-second ticks) for countdown display. On visibilitychange,
 * calculates actual elapsed time using Date.now() - lastActivityTimestamp to
 * reconcile drift from throttled timers in background tabs.
 *
 * Requirements: 5.1, 5.2, 5.3, 5.4, 5.6
 */

// ── Types ─────────────────────────────────────────────────────────────────────

export interface InactivityTimerCallbacks {
  /** Called every second with the remaining time in milliseconds. */
  onTick: (remainingMs: number) => void;
  /** Called when the inactivity timer reaches zero (show the activity prompt). */
  onTimeout: () => void;
  /** Called after meaningful user activity resets the timer. */
  onActivity?: () => void;
}

// ── Constants ─────────────────────────────────────────────────────────────────

const TICK_INTERVAL_MS = 1_000;

/** Activity events that reset the inactivity timer. */
const ACTIVITY_EVENTS: Array<keyof DocumentEventMap> = [
  "click",
  "keypress",
  "scroll",
];

// ── InactivityTimer Class ─────────────────────────────────────────────────────

export class InactivityTimer {
  private timeoutMs: number = 0;
  private remainingMs: number = 0;
  private lastActivityTimestamp: number = 0;
  private intervalId: ReturnType<typeof setInterval> | null = null;
  private callbacks: InactivityTimerCallbacks | null = null;
  private boundHandleActivity: () => void;
  private boundHandleVisibilityChange: () => void;
  private running: boolean = false;

  constructor() {
    this.boundHandleActivity = this.handleActivity.bind(this);
    this.boundHandleVisibilityChange =
      this.handleVisibilityChange.bind(this);
  }

  // ── Public API ────────────────────────────────────────────────────────────

  /**
   * Start the inactivity countdown.
   * @param timeoutMs Total timeout duration in milliseconds.
   * @param callbacks Callbacks for tick and timeout events.
   */
  start(
    timeoutMs: number,
    callbacks: InactivityTimerCallbacks,
    lastActivityTimestamp?: number
  ): void {
    // Stop any existing timer before starting a new one
    this.stop();

    this.timeoutMs = timeoutMs;
    this.lastActivityTimestamp = lastActivityTimestamp ?? Date.now();
    this.remainingMs = Math.max(0, timeoutMs - (Date.now() - this.lastActivityTimestamp));
    this.callbacks = callbacks;
    this.running = true;

    this.registerListeners();
    this.startInterval();
  }

  /**
   * Reset the timer to the full configured timeout duration.
   * Called when meaningful user activity is detected.
   */
  reset(): void {
    if (!this.running) return;

    this.remainingMs = this.timeoutMs;
    this.lastActivityTimestamp = Date.now();
  }

  /**
   * Stop and clean up the timer entirely.
   * Removes all event listeners and clears the interval.
   */
  stop(): void {
    this.running = false;
    this.clearInterval();
    this.unregisterListeners();
    this.remainingMs = 0;
    this.lastActivityTimestamp = 0;
    this.callbacks = null;
  }

  /**
   * Reconcile the timer after a visibility change.
   * Calculates actual elapsed time while the tab was hidden and adjusts
   * the remaining time accordingly. If elapsed time exceeds remaining,
   * triggers timeout immediately.
   */
  reconcileAfterVisibilityChange(): void {
    if (!this.running) return;

    const now = Date.now();
    const elapsed = now - this.lastActivityTimestamp;
    const newRemaining = this.timeoutMs - elapsed;

    if (newRemaining <= 0) {
      this.remainingMs = 0;
      this.triggerTimeout();
    } else {
      this.remainingMs = newRemaining;
      this.callbacks?.onTick(this.remainingMs);
    }
  }

  // ── Getters (for testing) ─────────────────────────────────────────────────

  /** Current remaining time in milliseconds. */
  getRemainingMs(): number {
    return this.remainingMs;
  }

  /** Whether the timer is currently running. */
  isRunning(): boolean {
    return this.running;
  }

  /** Last activity timestamp (epoch ms). */
  getLastActivityTimestamp(): number {
    return this.lastActivityTimestamp;
  }

  // ── Private Methods ───────────────────────────────────────────────────────

  private startInterval(): void {
    this.intervalId = setInterval(() => {
      this.tick();
    }, TICK_INTERVAL_MS);
  }

  private clearInterval(): void {
    if (this.intervalId !== null) {
      clearInterval(this.intervalId);
      this.intervalId = null;
    }
  }

  private tick(): void {
    if (!this.running) return;

    // Calculate remaining based on actual elapsed time for accuracy
    const now = Date.now();
    const elapsed = now - this.lastActivityTimestamp;
    this.remainingMs = Math.max(0, this.timeoutMs - elapsed);

    if (this.remainingMs <= 0) {
      this.triggerTimeout();
    } else {
      this.callbacks?.onTick(this.remainingMs);
    }
  }

  private triggerTimeout(): void {
    this.clearInterval();
    this.running = false;
    this.callbacks?.onTimeout();
  }

  private handleActivity(): void {
    this.reset();
    this.callbacks?.onActivity?.();
  }

  private handleVisibilityChange(): void {
    if (typeof document === "undefined") return;

    if (document.visibilityState === "visible") {
      this.reconcileAfterVisibilityChange();
    }
  }

  private registerListeners(): void {
    if (typeof document === "undefined") return;

    for (const event of ACTIVITY_EVENTS) {
      document.addEventListener(event, this.boundHandleActivity, {
        passive: true,
      });
    }

    document.addEventListener(
      "visibilitychange",
      this.boundHandleVisibilityChange
    );
  }

  private unregisterListeners(): void {
    if (typeof document === "undefined") return;

    for (const event of ACTIVITY_EVENTS) {
      document.removeEventListener(event, this.boundHandleActivity);
    }

    document.removeEventListener(
      "visibilitychange",
      this.boundHandleVisibilityChange
    );
  }
}
