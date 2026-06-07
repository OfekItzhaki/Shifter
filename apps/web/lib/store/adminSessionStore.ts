import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";

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
  isElevated: boolean;
  elevatedMode: ElevatedMode | null;
  elevatedGroupId: string | null;
  timeoutDuration: number;
  remainingMs: number;
  lastActivityAt: number;
  isPromptVisible: boolean;
  promptCountdownMs: number;

  /** Populated on exit so side-effect handlers can read mode/groupId/reason after state is cleared. */
  lastExitContext: ExitContext | null;

  enterElevatedMode: (
    mode: ElevatedMode,
    groupId?: string,
    timeoutMinutes?: number
  ) => void;
  exitElevatedMode: (reason: ExitReason) => void;
  resetTimer: () => void;
  showPrompt: () => void;
  dismissPrompt: (response: PromptResponse) => void;
  clearExitContext: () => void;
}

const DEFAULT_TIMEOUT_MINUTES = 15;
const PROMPT_COUNTDOWN_MS = 60_000;

const initialState = {
  isElevated: false,
  elevatedMode: null as ElevatedMode | null,
  elevatedGroupId: null as string | null,
  timeoutDuration: DEFAULT_TIMEOUT_MINUTES,
  remainingMs: 0,
  lastActivityAt: 0,
  isPromptVisible: false,
  promptCountdownMs: 0,
  lastExitContext: null as ExitContext | null,
};

function getSessionRemainingMs(lastActivityAt: number, timeoutMinutes: number): number {
  if (!lastActivityAt) return 0;
  return Math.max(0, timeoutMinutes * 60 * 1000 - (Date.now() - lastActivityAt));
}

function buildExitContext(
  mode: ElevatedMode | null,
  groupId: string | null,
  reason: ExitReason
): ExitContext | null {
  return mode ? { mode, groupId, reason, timestamp: Date.now() } : null;
}

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
          lastActivityAt: Date.now(),
          isPromptVisible: false,
          promptCountdownMs: 0,
          lastExitContext: null,
        });
      },

      exitElevatedMode: (reason: ExitReason) => {
        const { elevatedMode, elevatedGroupId } = get();
        set({
          ...initialState,
          lastExitContext: buildExitContext(elevatedMode, elevatedGroupId, reason),
        });
      },

      resetTimer: () => {
        const { isElevated, timeoutDuration } = get();
        if (!isElevated) return;
        set({
          remainingMs: timeoutDuration * 60 * 1000,
          lastActivityAt: Date.now(),
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
          const { timeoutDuration } = get();
          set({
            isPromptVisible: false,
            promptCountdownMs: 0,
            remainingMs: timeoutDuration * 60 * 1000,
            lastActivityAt: Date.now(),
          });
          return;
        }

        const { elevatedMode, elevatedGroupId } = get();
        set({
          ...initialState,
          lastExitContext: buildExitContext(elevatedMode, elevatedGroupId, "prompt_no"),
        });
      },

      clearExitContext: () => {
        set({ lastExitContext: null });
      },
    }),
    {
      name: "jobuler-admin-session",
      storage: createJSONStorage(() => localStorage),
      partialize: (state) => ({
        isElevated: state.isElevated,
        elevatedMode: state.elevatedMode,
        elevatedGroupId: state.elevatedGroupId,
        timeoutDuration: state.timeoutDuration,
        remainingMs: state.remainingMs,
        lastActivityAt: state.lastActivityAt,
      }),
      merge: (persisted, current) => {
        const persistedState = persisted as Partial<AdminSessionState>;

        if (!persistedState.isElevated || !persistedState.elevatedMode) {
          return current;
        }

        const timeoutDuration = persistedState.timeoutDuration ?? DEFAULT_TIMEOUT_MINUTES;
        const lastActivityAt = persistedState.lastActivityAt ?? 0;
        const remainingMs = getSessionRemainingMs(lastActivityAt, timeoutDuration);

        if (remainingMs <= 0) {
          return current;
        }

        return {
          ...current,
          isElevated: true,
          elevatedMode: persistedState.elevatedMode,
          elevatedGroupId: persistedState.elevatedGroupId ?? null,
          timeoutDuration,
          remainingMs,
          lastActivityAt,
          isPromptVisible: false,
          promptCountdownMs: 0,
          lastExitContext: null,
        };
      },
    }
  )
);
