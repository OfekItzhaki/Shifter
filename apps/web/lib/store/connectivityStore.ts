import { create } from "zustand";

export type ConnectivityStatus = "online" | "offline" | "server-unavailable";

interface ConnectivityState {
  status: ConnectivityStatus;
  lastOnlineAt: number | null;
  isConnected: boolean;

  goOffline: () => void;
  goOnline: () => void;
  setServerUnavailable: () => void;
  setServerRecovered: () => void;
}

export const useConnectivityStore = create<ConnectivityState>()((set, get) => ({
  status: "online",
  lastOnlineAt: null,
  get isConnected() {
    return get().status === "online";
  },

  goOffline: () =>
    set({
      status: "offline",
      lastOnlineAt: Date.now(),
    }),

  goOnline: () =>
    set({
      status: "online",
    }),

  setServerUnavailable: () => {
    const { status } = get();
    // Don't override offline — device offline takes priority
    if (status === "offline") return;
    set({
      status: "server-unavailable",
      lastOnlineAt: Date.now(),
    });
  },

  setServerRecovered: () => {
    const { status } = get();
    // Only recover from server-unavailable, not from offline
    if (status !== "server-unavailable") return;
    set({
      status: "online",
    });
  },
}));
