"use client";

import { useEffect, useRef, useState } from "react";
import {
  getNotifications, dismissNotification, dismissAllNotifications,
  NotificationDto,
} from "@/lib/api/notifications";
import { useSpaceStore } from "@/lib/store/spaceStore";

export default function NotificationBell() {
  const { currentSpaceId } = useSpaceStore();
  const [notifications, setNotifications] = useState<NotificationDto[]>([]);
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  async function load() {
    if (!currentSpaceId) return;
    try {
      const data = await getNotifications(currentSpaceId);
      setNotifications(data);
    } catch { /* silent — bell is non-critical */ }
  }

  useEffect(() => {
    load();
    const interval = setInterval(load, 30_000); // poll every 30s
    return () => clearInterval(interval);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentSpaceId]);

  // Close on outside click
  useEffect(() => {
    function handler(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const unreadCount = notifications.filter(n => !n.isRead).length;

  async function handleDismiss(id: string) {
    if (!currentSpaceId) return;
    await dismissNotification(currentSpaceId, id);
    setNotifications(prev => prev.map(n => n.id === id ? { ...n, isRead: true } : n));
  }

  async function handleDismissAll() {
    if (!currentSpaceId) return;
    await dismissAllNotifications(currentSpaceId);
    setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
  }

  function eventIcon(eventType: string) {
    if (eventType === "solver_failed") return "❌";
    if (eventType === "solver_infeasible") return "⚠️";
    return "✅";
  }

  return (
    <div ref={ref} className="relative">
      <button
        onClick={() => setOpen(!open)}
        className="relative p-2 rounded-lg hover:bg-gray-100 text-gray-600"
        aria-label="Notifications"
      >
        <svg xmlns="http://www.w3.org/2000/svg" className="h-5 w-5" fill="none"
          viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round"
            d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6 6 0 10-12 0v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
        </svg>
        {unreadCount > 0 && (
          <span className="absolute top-1 right-1 h-4 w-4 rounded-full bg-red-500 text-white text-[10px] flex items-center justify-center font-bold">
            {unreadCount > 9 ? "9+" : unreadCount}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute right-0 mt-2 w-80 bg-white border border-gray-200 rounded-xl shadow-lg z-50">
          <div className="flex items-center justify-between px-4 py-3 border-b">
            <span className="text-sm font-semibold">התראות</span>
            {unreadCount > 0 && (
              <button onClick={handleDismissAll}
                className="text-xs text-blue-600 hover:underline">
                סמן הכל כנקרא
              </button>
            )}
          </div>

          <div className="max-h-80 overflow-y-auto divide-y divide-gray-100">
            {notifications.length === 0 ? (
              <p className="text-xs text-gray-400 text-center py-6">אין התראות</p>
            ) : notifications.map(n => (
              <div key={n.id}
                className={`px-4 py-3 flex gap-3 ${n.isRead ? "opacity-60" : "bg-blue-50/40"}`}>
                <span className="text-base mt-0.5">{eventIcon(n.eventType)}</span>
                <div className="flex-1 min-w-0">
                  <p className="text-xs font-semibold text-gray-800 truncate">{n.title}</p>
                  <p className="text-xs text-gray-500 mt-0.5 line-clamp-2">{n.body}</p>
                  <p className="text-[10px] text-gray-400 mt-1">
                    {new Date(n.createdAt).toLocaleString([], { dateStyle: "short", timeStyle: "short" })}
                  </p>
                </div>
                {!n.isRead && (
                  <button onClick={() => handleDismiss(n.id)}
                    className="text-gray-300 hover:text-gray-500 text-xs self-start mt-0.5"
                    aria-label="Dismiss">×</button>
                )}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
