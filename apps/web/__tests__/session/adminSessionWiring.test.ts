/**
 * Unit tests for useAdminSessionWiring hook.
 *
 * Tests the integration between InactivityTimer, MultiTabSync, and
 * adminSessionStore — verifying that:
 * - Timer starts when entering elevated mode
 * - Timer stops when exiting elevated mode
 * - API calls reset the timer
 * - Multi-tab sync messages propagate correctly
 * - Session exit is broadcast to other tabs
 *
 * Requirements: 5.3, 11.1, 11.2, 11.3
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useAdminSessionStore } from "../../lib/store/adminSessionStore";
import { useAdminSessionWiring } from "../../lib/hooks/useAdminSessionWiring";
import { apiClient } from "../../lib/api/client";

// ── Mocks ─────────────────────────────────────────────────────────────────────

// Mock InactivityTimer
const mockTimerStart = vi.fn();
const mockTimerReset = vi.fn();
const mockTimerStop = vi.fn();

vi.mock("../../lib/session/inactivityTimer", () => ({
  InactivityTimer: vi.fn().mockImplementation(() => ({
    start: mockTimerStart,
    reset: mockTimerReset,
    stop: mockTimerStop,
  })),
}));

// Mock MultiTabSync
const mockBroadcast = vi.fn();
const mockSubscribe = vi.fn();
const mockDestroy = vi.fn();

vi.mock("../../lib/session/multiTabSync", () => ({
  createMultiTabSync: vi.fn(() => ({
    broadcast: mockBroadcast,
    subscribe: mockSubscribe,
    destroy: mockDestroy,
  })),
}));

// ── Helpers ───────────────────────────────────────────────────────────────────

function resetStore() {
  const store = useAdminSessionStore.getState();
  if (store.isElevated) {
    store.exitElevatedMode("manual");
  }
  store.clearExitContext();
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe("useAdminSessionWiring", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    resetStore();
  });

  afterEach(() => {
    resetStore();
  });

  it("starts the inactivity timer when entering elevated mode", () => {
    const { rerender } = renderHook(() => useAdminSessionWiring());

    // Enter elevated mode
    act(() => {
      useAdminSessionStore.getState().enterElevatedMode("management", "group-1", 10);
    });
    rerender();

    expect(mockTimerStart).toHaveBeenCalledWith(
      10 * 60 * 1000,
      expect.objectContaining({
        onTick: expect.any(Function),
        onTimeout: expect.any(Function),
      })
    );
  });

  it("stops the inactivity timer when exiting elevated mode", () => {
    const { rerender } = renderHook(() => useAdminSessionWiring());

    // Enter then exit
    act(() => {
      useAdminSessionStore.getState().enterElevatedMode("management", "group-1", 15);
    });
    rerender();

    act(() => {
      useAdminSessionStore.getState().exitElevatedMode("manual");
    });
    rerender();

    expect(mockTimerStop).toHaveBeenCalled();
  });

  it("creates MultiTabSync when entering elevated mode", () => {
    const { rerender } = renderHook(() => useAdminSessionWiring());

    act(() => {
      useAdminSessionStore.getState().enterElevatedMode("platform", undefined, 20);
    });
    rerender();

    expect(mockSubscribe).toHaveBeenCalledWith(expect.any(Function));
  });

  it("destroys MultiTabSync when exiting elevated mode", () => {
    const { rerender } = renderHook(() => useAdminSessionWiring());

    act(() => {
      useAdminSessionStore.getState().enterElevatedMode("management", "g1", 15);
    });
    rerender();

    act(() => {
      useAdminSessionStore.getState().exitElevatedMode("manual");
    });
    rerender();

    expect(mockDestroy).toHaveBeenCalled();
  });

  it("broadcasts session_exit when transitioning from elevated to non-elevated", () => {
    const { rerender } = renderHook(() => useAdminSessionWiring());

    act(() => {
      useAdminSessionStore.getState().enterElevatedMode("management", "group-1", 15);
    });
    rerender();

    // Clear any previous broadcast calls (e.g. from API interceptor setup)
    mockBroadcast.mockClear();

    act(() => {
      useAdminSessionStore.getState().exitElevatedMode("manual");
    });
    rerender();

    expect(mockBroadcast).toHaveBeenCalledWith(
      expect.objectContaining({
        type: "session_exit",
        groupId: "group-1",
        mode: "management",
      })
    );
  });

  it("resets timer on activity_reset message from another tab", () => {
    const { rerender } = renderHook(() => useAdminSessionWiring());

    act(() => {
      useAdminSessionStore.getState().enterElevatedMode("management", "g1", 15);
    });
    rerender();

    // Get the handler that was passed to subscribe
    const handler = mockSubscribe.mock.calls[0][0];

    // Simulate receiving an activity_reset message
    act(() => {
      handler({ type: "activity_reset", timestamp: Date.now() });
    });

    expect(mockTimerReset).toHaveBeenCalled();
  });

  it("exits elevated mode on session_exit message from another tab", () => {
    const { rerender } = renderHook(() => useAdminSessionWiring());

    act(() => {
      useAdminSessionStore.getState().enterElevatedMode("management", "g1", 15);
    });
    rerender();

    const handler = mockSubscribe.mock.calls[0][0];

    act(() => {
      handler({ type: "session_exit", timestamp: Date.now(), mode: "management" });
    });

    expect(useAdminSessionStore.getState().isElevated).toBe(false);
  });

  it("resets timer on prompt_dismissed message from another tab", () => {
    const { rerender } = renderHook(() => useAdminSessionWiring());

    act(() => {
      useAdminSessionStore.getState().enterElevatedMode("management", "g1", 15);
    });
    rerender();

    const handler = mockSubscribe.mock.calls[0][0];

    act(() => {
      handler({ type: "prompt_dismissed", timestamp: Date.now() });
    });

    expect(mockTimerReset).toHaveBeenCalled();
  });

  it("registers API interceptor when elevated and removes it when not", () => {
    const ejectSpy = vi.spyOn(apiClient.interceptors.request, "eject");
    const { rerender } = renderHook(() => useAdminSessionWiring());

    act(() => {
      useAdminSessionStore.getState().enterElevatedMode("management", "g1", 15);
    });
    rerender();

    // Exit elevated mode — interceptor should be ejected
    act(() => {
      useAdminSessionStore.getState().exitElevatedMode("manual");
    });
    rerender();

    expect(ejectSpy).toHaveBeenCalled();
    ejectSpy.mockRestore();
  });

  it("calls showPrompt on timer timeout callback", () => {
    const { rerender } = renderHook(() => useAdminSessionWiring());

    act(() => {
      useAdminSessionStore.getState().enterElevatedMode("management", "g1", 15);
    });
    rerender();

    // Get the onTimeout callback passed to timer.start
    const callbacks = mockTimerStart.mock.calls[0][1];

    act(() => {
      callbacks.onTimeout();
    });

    expect(useAdminSessionStore.getState().isPromptVisible).toBe(true);
  });

  it("broadcasts prompt_shown when timer times out", () => {
    const { rerender } = renderHook(() => useAdminSessionWiring());

    act(() => {
      useAdminSessionStore.getState().enterElevatedMode("management", "g1", 15);
    });
    rerender();

    const callbacks = mockTimerStart.mock.calls[0][1];

    act(() => {
      callbacks.onTimeout();
    });

    expect(mockBroadcast).toHaveBeenCalledWith(
      expect.objectContaining({
        type: "prompt_shown",
        groupId: "g1",
        mode: "management",
      })
    );
  });
});
