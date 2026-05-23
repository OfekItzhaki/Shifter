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
  remainingMs: 0,
  isPromptVisible: false,
  promptCountdownMs: 0,
  lastExitContext: null as ExitContext | null,
};

// ── Store Implementation ──────────────────────────────────────────────────────
// Persisted to sessionStorage so admin mode survives page refresh but not tab close.
// Exits only on: manual exit, timeout, or prompt dismissal.

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
    set({
      isElevated: true,
      elevatedMode: mode,
      elevatedGroupId: groupId ?? null,
      timeoutDuration: duration,
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
      name: "shifter-admin-session",
      storage: {
        getItem: (name) => {
          const str = sessionStorage.getItem(name);
          return str ? JSON.parse(str) : null;
        },
        setItem: (name, value) => {
          sessionStorage.setItem(name, JSON.stringify(value));
        },
        removeItem: (name) => {
          sessionStorage.removeItem(name);
        },
      },
    }
  )
);
