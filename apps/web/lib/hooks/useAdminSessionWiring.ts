"use client";

/**
 * useAdminSessionWiring — Connects the InactivityTimer and MultiTabSync
 * to the adminSessionStore. This hook is the central integration point
 * that starts/stops the timer when elevated mode changes, resets the timer
 * on API calls, and synchronizes state across browser tabs.
 *
 * Requirements: 5.1, 5.3, 11.1, 11.2, 11.3
 */

import { useEffect, useRef } from "react";
import { useAdminSessionStore } from "@/lib/store/adminSessionStore";
import { InactivityTimer } from "@/lib/session/inactivityTimer";
import {
  MultiTabSync,
  createMultiTabSync,
  SyncMessage,
} from "@/lib/session/multiTabSync";
import { apiClient } from "@/lib/api/client";
import type { InternalAxiosRequestConfig } from "axios";

export function useAdminSessionWiring(): void {
  const isElevated = useAdminSessionStore((s) => s.isElevated);
  const elevatedMode = useAdminSessionStore((s) => s.elevatedMode);
  const elevatedGroupId = useAdminSessionStore((s) => s.elevatedGroupId);
  const timeoutDuration = useAdminSessionStore((s) => s.timeoutDuration);
  const resetTimer = useAdminSessionStore((s) => s.resetTimer);
  const showPrompt = useAdminSessionStore((s) => s.showPrompt);
  const exitElevatedMode = useAdminSessionStore((s) => s.exitElevatedMode);
  const isPromptVisible = useAdminSessionStore((s) => s.isPromptVisible);
  const lastExitContext = useAdminSessionStore((s) => s.lastExitContext);

  const timerRef = useRef<InactivityTimer | null>(null);
  const syncRef = useRef<MultiTabSync | null>(null);
  const interceptorIdRef = useRef<number | null>(null);

  // Keep a ref to lastExitContext so the cleanup function can access it
  const lastExitContextRef = useRef(lastExitContext);
  lastExitContextRef.current = lastExitContext;

  // ── Start/stop InactivityTimer based on elevated mode ───────────────────
  useEffect(() => {
    if (!isElevated) {
      if (timerRef.current) {
        timerRef.current.stop();
        timerRef.current = null;
      }
      return;
    }

    const timer = new InactivityTimer();
    timerRef.current = timer;

    const timeoutMs = timeoutDuration * 60 * 1000;
    timer.start(timeoutMs, {
      onTick: (_remainingMs: number) => {
        // Timer's primary job is to detect when timeout occurs.
      },
      onTimeout: () => {
        showPrompt();
        syncRef.current?.broadcast({
          type: "prompt_shown",
          timestamp: Date.now(),
          groupId: elevatedGroupId ?? undefined,
          mode: elevatedMode ?? undefined,
        });
      },
    });

    return () => {
      timer.stop();
      timerRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isElevated, timeoutDuration]);

  // ── API call interceptor — reset timer on successful API calls ──────────
  useEffect(() => {
    if (!isElevated) {
      if (interceptorIdRef.current !== null) {
        apiClient.interceptors.request.eject(interceptorIdRef.current);
        interceptorIdRef.current = null;
      }
      return;
    }

    const id = apiClient.interceptors.request.use(
      (config: InternalAxiosRequestConfig) => {
        if (timerRef.current) {
          timerRef.current.reset();
        }
        syncRef.current?.broadcast({
          type: "activity_reset",
          timestamp: Date.now(),
        });
        return config;
      }
    );
    interceptorIdRef.current = id;

    return () => {
      apiClient.interceptors.request.eject(id);
      interceptorIdRef.current = null;
    };
  }, [isElevated]);

  // ── MultiTabSync — connect to store ─────────────────────────────────────
  useEffect(() => {
    if (!isElevated) {
      return;
    }

    const sync = createMultiTabSync();
    syncRef.current = sync;

    const handleSyncMessage = (message: SyncMessage) => {
      switch (message.type) {
        case "activity_reset":
          if (timerRef.current) {
            timerRef.current.reset();
          }
          resetTimer();
          break;

        case "session_exit":
          exitElevatedMode("sync");
          break;

        case "prompt_shown":
          // Suppress duplicate — defer to the tab that showed it first.
          break;

        case "prompt_dismissed":
          if (timerRef.current) {
            timerRef.current.reset();
          }
          resetTimer();
          break;
      }
    };

    sync.subscribe(handleSyncMessage);

    return () => {
      // Broadcast session_exit before destroying (if exit context is available)
      if (lastExitContextRef.current) {
        sync.broadcast({
          type: "session_exit",
          timestamp: Date.now(),
          groupId: lastExitContextRef.current.groupId ?? undefined,
          mode: lastExitContextRef.current.mode ?? undefined,
        });
      }
      sync.destroy();
      syncRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isElevated]);

  // ── Broadcast prompt_dismissed when prompt is dismissed with "yes" ──────
  const prevPromptRef = useRef(isPromptVisible);

  useEffect(() => {
    const wasVisible = prevPromptRef.current;
    prevPromptRef.current = isPromptVisible;

    if (wasVisible && !isPromptVisible && isElevated) {
      syncRef.current?.broadcast({
        type: "prompt_dismissed",
        timestamp: Date.now(),
        groupId: elevatedGroupId ?? undefined,
        mode: elevatedMode ?? undefined,
      });
    }
  }, [isPromptVisible, isElevated, elevatedGroupId, elevatedMode]);
}
