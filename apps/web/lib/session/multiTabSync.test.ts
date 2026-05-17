import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { MultiTabSync, createMultiTabSync, SyncMessage } from "./multiTabSync";

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeBroadcastChannelMock() {
  const instances: Array<{ onmessage: ((e: MessageEvent) => void) | null }> =
    [];

  class MockBroadcastChannel {
    onmessage: ((e: MessageEvent) => void) | null = null;
    closed = false;

    constructor(public name: string) {
      instances.push(this);
    }

    postMessage(data: unknown) {
      // Deliver to all OTHER instances (simulates cross-tab behavior)
      for (const inst of instances) {
        if (inst !== this && inst.onmessage && !this.closed) {
          inst.onmessage(new MessageEvent("message", { data }));
        }
      }
    }

    close() {
      this.closed = true;
      const idx = instances.indexOf(this);
      if (idx >= 0) instances.splice(idx, 1);
    }
  }

  return { MockBroadcastChannel, instances };
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe("MultiTabSync", () => {
  let originalBroadcastChannel: typeof globalThis.BroadcastChannel;

  beforeEach(() => {
    originalBroadcastChannel = globalThis.BroadcastChannel;
  });

  afterEach(() => {
    globalThis.BroadcastChannel = originalBroadcastChannel;
  });

  describe("with BroadcastChannel available", () => {
    it("broadcasts messages to subscribers on other instances", () => {
      const { MockBroadcastChannel } = makeBroadcastChannelMock();
      globalThis.BroadcastChannel =
        MockBroadcastChannel as unknown as typeof BroadcastChannel;

      const sync1 = createMultiTabSync();
      const sync2 = createMultiTabSync();

      const handler = vi.fn();
      sync2.subscribe(handler);

      const msg: SyncMessage = {
        type: "activity_reset",
        timestamp: Date.now(),
        groupId: "group-1",
        mode: "management",
      };

      sync1.broadcast(msg);

      expect(handler).toHaveBeenCalledTimes(1);
      expect(handler).toHaveBeenCalledWith(msg);

      sync1.destroy();
      sync2.destroy();
    });

    it("does not deliver messages to the broadcasting tab itself", () => {
      const { MockBroadcastChannel } = makeBroadcastChannelMock();
      globalThis.BroadcastChannel =
        MockBroadcastChannel as unknown as typeof BroadcastChannel;

      const sync = createMultiTabSync();
      const handler = vi.fn();
      sync.subscribe(handler);

      sync.broadcast({
        type: "session_exit",
        timestamp: Date.now(),
      });

      expect(handler).not.toHaveBeenCalled();

      sync.destroy();
    });

    it("stops receiving messages after destroy()", () => {
      const { MockBroadcastChannel } = makeBroadcastChannelMock();
      globalThis.BroadcastChannel =
        MockBroadcastChannel as unknown as typeof BroadcastChannel;

      const sync1 = createMultiTabSync();
      const sync2 = createMultiTabSync();

      const handler = vi.fn();
      sync2.subscribe(handler);
      sync2.destroy();

      sync1.broadcast({
        type: "prompt_shown",
        timestamp: Date.now(),
      });

      expect(handler).not.toHaveBeenCalled();

      sync1.destroy();
    });

    it("does not broadcast after destroy()", () => {
      const { MockBroadcastChannel } = makeBroadcastChannelMock();
      globalThis.BroadcastChannel =
        MockBroadcastChannel as unknown as typeof BroadcastChannel;

      const sync1 = createMultiTabSync();
      const sync2 = createMultiTabSync();

      const handler = vi.fn();
      sync2.subscribe(handler);
      sync1.destroy();

      sync1.broadcast({
        type: "activity_reset",
        timestamp: Date.now(),
      });

      expect(handler).not.toHaveBeenCalled();

      sync2.destroy();
    });
  });

  describe("with localStorage fallback", () => {
    beforeEach(() => {
      // Remove BroadcastChannel to force fallback
      // @ts-expect-error - intentionally removing for test
      delete globalThis.BroadcastChannel;
    });

    it("uses localStorage setItem/removeItem to broadcast", () => {
      const setItemSpy = vi.spyOn(Storage.prototype, "setItem");
      const removeItemSpy = vi.spyOn(Storage.prototype, "removeItem");

      const sync = createMultiTabSync();

      const msg: SyncMessage = {
        type: "session_exit",
        timestamp: 1234567890,
        mode: "platform",
      };

      sync.broadcast(msg);

      expect(setItemSpy).toHaveBeenCalledWith(
        "admin-session-sync-msg",
        JSON.stringify(msg)
      );
      expect(removeItemSpy).toHaveBeenCalledWith("admin-session-sync-msg");

      sync.destroy();
      setItemSpy.mockRestore();
      removeItemSpy.mockRestore();
    });

    it("dispatches messages received via storage events", () => {
      const sync = createMultiTabSync();
      const handler = vi.fn();
      sync.subscribe(handler);

      const msg: SyncMessage = {
        type: "prompt_dismissed",
        timestamp: Date.now(),
        groupId: "g-2",
      };

      // Simulate a storage event from another tab
      const event = new StorageEvent("storage", {
        key: "admin-session-sync-msg",
        newValue: JSON.stringify(msg),
      });
      window.dispatchEvent(event);

      expect(handler).toHaveBeenCalledTimes(1);
      expect(handler).toHaveBeenCalledWith(msg);

      sync.destroy();
    });

    it("ignores storage events for unrelated keys", () => {
      const sync = createMultiTabSync();
      const handler = vi.fn();
      sync.subscribe(handler);

      const event = new StorageEvent("storage", {
        key: "some-other-key",
        newValue: "some-value",
      });
      window.dispatchEvent(event);

      expect(handler).not.toHaveBeenCalled();

      sync.destroy();
    });

    it("ignores storage events with null newValue", () => {
      const sync = createMultiTabSync();
      const handler = vi.fn();
      sync.subscribe(handler);

      const event = new StorageEvent("storage", {
        key: "admin-session-sync-msg",
        newValue: null,
      });
      window.dispatchEvent(event);

      expect(handler).not.toHaveBeenCalled();

      sync.destroy();
    });

    it("ignores malformed JSON in storage events", () => {
      const sync = createMultiTabSync();
      const handler = vi.fn();
      sync.subscribe(handler);

      const event = new StorageEvent("storage", {
        key: "admin-session-sync-msg",
        newValue: "not-valid-json{{{",
      });
      window.dispatchEvent(event);

      expect(handler).not.toHaveBeenCalled();

      sync.destroy();
    });

    it("removes storage listener on destroy()", () => {
      const removeListenerSpy = vi.spyOn(window, "removeEventListener");

      const sync = createMultiTabSync();
      sync.destroy();

      expect(removeListenerSpy).toHaveBeenCalledWith(
        "storage",
        expect.any(Function)
      );

      removeListenerSpy.mockRestore();
    });
  });

  describe("subscribe/unsubscribe", () => {
    it("supports multiple handlers", () => {
      const { MockBroadcastChannel } = makeBroadcastChannelMock();
      globalThis.BroadcastChannel =
        MockBroadcastChannel as unknown as typeof BroadcastChannel;

      const sync1 = createMultiTabSync();
      const sync2 = createMultiTabSync();

      const handler1 = vi.fn();
      const handler2 = vi.fn();
      sync2.subscribe(handler1);
      sync2.subscribe(handler2);

      sync1.broadcast({ type: "activity_reset", timestamp: Date.now() });

      expect(handler1).toHaveBeenCalledTimes(1);
      expect(handler2).toHaveBeenCalledTimes(1);

      sync1.destroy();
      sync2.destroy();
    });

    it("unsubscribe removes a specific handler", () => {
      const { MockBroadcastChannel } = makeBroadcastChannelMock();
      globalThis.BroadcastChannel =
        MockBroadcastChannel as unknown as typeof BroadcastChannel;

      const sync1 = createMultiTabSync();
      const sync2 = createMultiTabSync();

      const handler1 = vi.fn();
      const handler2 = vi.fn();
      sync2.subscribe(handler1);
      sync2.subscribe(handler2);
      sync2.unsubscribe(handler1);

      sync1.broadcast({ type: "session_exit", timestamp: Date.now() });

      expect(handler1).not.toHaveBeenCalled();
      expect(handler2).toHaveBeenCalledTimes(1);

      sync1.destroy();
      sync2.destroy();
    });

    it("handler errors do not break other handlers", () => {
      const { MockBroadcastChannel } = makeBroadcastChannelMock();
      globalThis.BroadcastChannel =
        MockBroadcastChannel as unknown as typeof BroadcastChannel;

      const sync1 = createMultiTabSync();
      const sync2 = createMultiTabSync();

      const badHandler = vi.fn(() => {
        throw new Error("oops");
      });
      const goodHandler = vi.fn();

      sync2.subscribe(badHandler);
      sync2.subscribe(goodHandler);

      sync1.broadcast({ type: "prompt_shown", timestamp: Date.now() });

      expect(badHandler).toHaveBeenCalledTimes(1);
      expect(goodHandler).toHaveBeenCalledTimes(1);

      sync1.destroy();
      sync2.destroy();
    });
  });

  describe("createMultiTabSync factory", () => {
    it("returns a MultiTabSync instance", () => {
      const { MockBroadcastChannel } = makeBroadcastChannelMock();
      globalThis.BroadcastChannel =
        MockBroadcastChannel as unknown as typeof BroadcastChannel;

      const sync = createMultiTabSync();
      expect(sync).toBeInstanceOf(MultiTabSync);
      sync.destroy();
    });
  });
});
