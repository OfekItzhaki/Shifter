import { create } from "zustand";
import { persist } from "zustand/middleware";

// ── Types ─────────────────────────────────────────────────────────────────────

export type ElevatedMode = "management" | "platform";
export type ExitReason = "manual" | "timeout" | "prompt_no" | "sync";
export type PromptResponse = "yes" | "no";

/** Context captured at the moment of exit, before state is cleared. */
export interface ExitContext {
  mode: ElevatedMode;
  groupId: string | null;
  reason: ExitReason;
  timestamp: number;
}

interface AdminSessionState {
  // State
  isElevated: boolean;
  elevatedMode: ElevatedMode | null;
  elevatedGroupId: string | null;
  timeoutDuration: number; // minutes, captured at session start
  lastActivityAt: number;
  remainingMs: number;
  isPromptVisible: boolean;
  promptCountdownMs: number;

  /** Populated on exit so side-effect handlers can read mode/groupId/reason after state is cleared. */
  lastExitContext: ExitContext | null;

  // Actions
  enterElevatedMode: (
    mode: ElevatedMode,
    groupId?: string,
    timeoutMinutes?: number
  ) => void;
  exitElevatedMode: (reason: ExitReason) => void;
  resetTimer: () => void;
  showPrompt: () => void;
  dismissPrompt: (response: PromptResponse) => void;
  /** Clear the exit context after side effects have been processed. */
  clearExitContext: () => void;
}

// ── Constants ─────────────────────────────────────────────────────────────────

const DEFAULT_TIMEOUT_MINUTES = 15;
const PROMPT_COUNTDOWN_MS = 60_000; // 60 seconds

// ── Initial State ─────────────────────────────────────────────────────────────

const initialState = {
  isElevated: false,
  elevatedMode: null as ElevatedMode | null,
  elevatedGroupId: null as string | null,
  timeoutDuration: DEFAULT_TIMEOUT_MINUTES,
  lastActivityAt: 0,
  remainingMs: 0,
  isPromptVisible: false,
  promptCountdownMs: 0,
  lastExitContext: null as ExitContext | null,
};

// ── Store Implementation ──────────────────────────────────────────────────────
export const useAdminSessionStore = create<AdminSessionState>()(
  persist(
    (set, get) => ({
  ...initialState,

  enterElevatedMode: (
    mode: ElevatedMode,
    groupId?: string,
    timeoutMinutes?: number
  ) => {
    const duration = timeoutMinutes ?? DEFAULT_TIMEOUT_MINUTES;
    const now = Date.now();
    set({
      isElevated: true,
      elevatedMode: mode,
      elevatedGroupId: groupId ?? null,
      timeoutDuration: duration,
      lastActivityAt: now,
      remainingMs: duration * 60 * 1000,
      isPromptVisible: false,
      promptCountdownMs: 0,
    });
  },

  exitElevatedMode: (reason: ExitReason) => {
    const { elevatedMode, elevatedGroupId } = get();
    set({
      isElevated: false,
      elevatedMode: null,
      elevatedGroupId: null,
      timeoutDuration: DEFAULT_TIMEOUT_MINUTES,
      lastActivityAt: 0,
      remainingMs: 0,
      isPromptVisible: false,
      promptCountdownMs: 0,
      lastExitContext: elevatedMode
        ? { mode: elevatedMode, groupId: elevatedGroupId, reason, timestamp: Date.now() }
        : null,
    });
  },

  resetTimer: () => {
    const { isElevated, timeoutDuration } = get();
    if (!isElevated) return;
    set({
      lastActivityAt: Date.now(),
      remainingMs: timeoutDuration * 60 * 1000,
      isPromptVisible: false,
      promptCountdownMs: 0,
    });
  },

  showPrompt: () => {
    const { isElevated } = get();
    if (!isElevated) return;
    set({
      isPromptVisible: true,
      promptCountdownMs: PROMPT_COUNTDOWN_MS,
    });
  },

  dismissPrompt: (response: PromptResponse) => {
    if (response === "yes") {
      // User confirmed activity — reset the inactivity timer
      const { timeoutDuration } = get();
      set({
        isPromptVisible: false,
        promptCountdownMs: 0,
        lastActivityAt: Date.now(),
        remainingMs: timeoutDuration * 60 * 1000,
      });
    } else {
      // User said "no" or countdown expired — exit elevated mode via proper exit flow
      const { elevatedMode, elevatedGroupId } = get();
      set({
        isElevated: false,
        elevatedMode: null,
        elevatedGroupId: null,
        timeoutDuration: DEFAULT_TIMEOUT_MINUTES,
        lastActivityAt: 0,
        remainingMs: 0,
        isPromptVisible: false,
        promptCountdownMs: 0,
        lastExitContext: elevatedMode
          ? { mode: elevatedMode, groupId: elevatedGroupId, reason: "prompt_no", timestamp: Date.now() }
          : null,
      });
    }
  },

  clearExitContext: () => {
    set({ lastExitContext: null });
  },
}),
    {
      name: "jobuler-admin-session",
      partialize: (state) => ({
        isElevated: state.isElevated,
        elevatedMode: state.elevatedMode,
        elevatedGroupId: state.elevatedGroupId,
        timeoutDuration: state.timeoutDuration,
        lastActivityAt: state.lastActivityAt,
        remainingMs: state.remainingMs,
      }),
      onRehydrateStorage: () => (state) => {
        if (!state?.isElevated || state.lastActivityAt <= 0)
          return;

        const timeoutMs = state.timeoutDuration * 60 * 1000;
        const elapsedMs = Date.now() - state.lastActivityAt;
        if (elapsedMs >= timeoutMs + PROMPT_COUNTDOWN_MS) {
          state.exitElevatedMode("timeout");
        } else {
          state.remainingMs = Math.max(0, timeoutMs - elapsedMs);
        }
      },
    }
  )
);
