/**
 * MultiTabSync — Cross-tab synchronization for admin session state.
 *
 * Uses BroadcastChannel as the primary transport with a localStorage + storage
 * event fallback for browsers that don't support BroadcastChannel.
 *
 * Requirements: 11.1, 11.2, 11.3
 */

// ── Types ─────────────────────────────────────────────────────────────────────

export type SyncMessageType =
  | "activity_reset"
  | "session_exit"
  | "prompt_shown"
  | "prompt_dismissed";

export interface SyncMessage {
  type: SyncMessageType;
  timestamp: number;
  groupId?: string;
  mode?: "management" | "platform";
}

export type SyncHandler = (message: SyncMessage) => void;

// ── Constants ─────────────────────────────────────────────────────────────────

const CHANNEL_NAME = "admin-session-sync";
const STORAGE_KEY = "admin-session-sync-msg";

// ── MultiTabSync Class ────────────────────────────────────────────────────────

export class MultiTabSync {
  private channel: BroadcastChannel | null = null;
  private handlers: Set<SyncHandler> = new Set();
  private storageListener: ((event: StorageEvent) => void) | null = null;
  private destroyed = false;

  constructor() {
    this.initTransport();
  }

  /**
   * Broadcast a message to all other tabs.
   */
  broadcast(message: SyncMessage): void {
    if (this.destroyed) return;

    if (this.channel) {
      this.channel.postMessage(message);
    } else {
      // Fallback: write to localStorage to trigger storage events in other tabs.
      // The storage event only fires in *other* tabs, not the current one.
      try {
        window.localStorage.setItem(STORAGE_KEY, JSON.stringify(message));
        // Remove immediately — we only need the event trigger, not persistent storage.
        window.localStorage.removeItem(STORAGE_KEY);
      } catch {
        // localStorage may be unavailable (private browsing, quota exceeded).
        // Graceful degradation: each tab operates independently.
      }
    }
  }

  /**
   * Subscribe to messages from other tabs.
   */
  subscribe(handler: SyncHandler): void {
    this.handlers.add(handler);
  }

  /**
   * Unsubscribe a previously registered handler.
   */
  unsubscribe(handler: SyncHandler): void {
    this.handlers.delete(handler);
  }

  /**
   * Tear down the sync channel and remove all listeners.
   */
  destroy(): void {
    if (this.destroyed) return;
    this.destroyed = true;

    if (this.channel) {
      this.channel.close();
      this.channel = null;
    }

    if (this.storageListener) {
      window.removeEventListener("storage", this.storageListener);
      this.storageListener = null;
    }

    this.handlers.clear();
  }

  // ── Private ───────────────────────────────────────────────────────────────

  private initTransport(): void {
    if (typeof window === "undefined") return;

    if (typeof BroadcastChannel !== "undefined") {
      this.channel = new BroadcastChannel(CHANNEL_NAME);
      this.channel.onmessage = (event: MessageEvent<SyncMessage>) => {
        this.dispatch(event.data);
      };
    } else {
      // Fallback: listen for storage events from other tabs.
      this.storageListener = (event: StorageEvent) => {
        if (event.key !== STORAGE_KEY || !event.newValue) return;
        try {
          const message = JSON.parse(event.newValue) as SyncMessage;
          this.dispatch(message);
        } catch {
          // Malformed message — ignore.
        }
      };
      window.addEventListener("storage", this.storageListener);
    }
  }

  private dispatch(message: SyncMessage): void {
    if (this.destroyed) return;
    for (const handler of this.handlers) {
      try {
        handler(message);
      } catch {
        // Don't let one handler's error break others.
      }
    }
  }
}

// ── Factory ───────────────────────────────────────────────────────────────────

/**
 * Create a new MultiTabSync instance. Call `destroy()` when done (e.g. on
 * component unmount or when exiting elevated mode).
 */
export function createMultiTabSync(): MultiTabSync {
  return new MultiTabSync();
}
