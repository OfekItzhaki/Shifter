"use client";

import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useTranslations } from "next-intl";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useDateFormat } from "@/lib/hooks/useDateFormat";
import {
  useNotifications,
  useDismissNotification,
  useDismissAllNotifications,
} from "@/lib/query/hooks/useNotifications";

export default function NotificationBell({ variant = "dark" }: { variant?: "light" | "dark" }) {
  const t = useTranslations("notifications");
  const { currentSpaceId } = useSpaceStore();
  const { fDateTime } = useDateFormat();
  const [open, setOpen] = useState(false);
  const [dropdownPos, setDropdownPos] = useState<{ top: number; left: number }>({ top: 60, left: 16 });
  const buttonRef = useRef<HTMLButtonElement>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const [mounted, setMounted] = useState(false);

  const { data: notifications = [] } = useNotifications(currentSpaceId);
  const dismissOne = useDismissNotification(currentSpaceId);
  const dismissAll = useDismissAllNotifications(currentSpaceId);

  // Wait for client mount before rendering portal
  useEffect(() => { setMounted(true); }, []);

  // Close on outside click
  useEffect(() => {
    function handler(e: MouseEvent) {
      const target = e.target as Node;
      if (
        buttonRef.current && !buttonRef.current.contains(target) &&
        dropdownRef.current && !dropdownRef.current.contains(target)
      ) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  function handleToggle(e: React.MouseEvent) {
    e.preventDefault();
    e.stopPropagation();
    if (!open && buttonRef.current) {
      const rect = buttonRef.current.getBoundingClientRect();
      setDropdownPos({ top: rect.bottom + 8, left: rect.left });
    }
    setOpen(o => !o);
  }

  const unreadCount = notifications.filter(n => !n.isRead).length;

  function eventIcon(eventType: string) {
    if (eventType === "solver_failed") return "❌";
    if (eventType === "solver_infeasible") return "⚠️";
    if (eventType === "solver_no_tasks") return "📋";
    if (eventType === "solver_no_people") return "👥";
    if (eventType === "solver_completed") return "✅";
    return "🔔";
  }

  const dropdown = open && mounted ? createPortal(
    <div
      ref={dropdownRef}
      className="bg-white border border-gray-200 rounded-xl shadow-xl"
      style={{
        position: "fixed",
        top: dropdownPos.top,
        left: dropdownPos.left,
        width: 340,
        maxHeight: "70vh",
        overflowY: "auto",
        direction: "rtl",
        zIndex: 99999,
      }}
      onClick={e => e.stopPropagation()}
    >
      <div className="flex items-center justify-between px-4 py-3 border-b" style={{ direction: "ltr" }}>
        <span className="text-sm font-semibold">{t("title")}</span>
        {unreadCount > 0 && (
          <button
            type="button"
            onClick={(e) => { e.preventDefault(); e.stopPropagation(); dismissAll.mutate(); }}
            className="text-xs text-blue-600 hover:underline">
            {t("markAllRead")}
          </button>
        )}
      </div>

      <div className="divide-y divide-gray-100" style={{ direction: "ltr" }}>
        {notifications.length === 0 ? (
          <p className="text-xs text-gray-400 text-center py-6">{t("noNotifications")}</p>
        ) : notifications.map(n => (
          <div key={n.id}
            className={`px-4 py-3 flex gap-3 ${n.isRead ? "opacity-50" : "bg-blue-50/40"}`}>
            <span className="text-base mt-0.5 flex-shrink-0">{eventIcon(n.eventType)}</span>
            <div className="flex-1 min-w-0">
              <p className="text-xs font-semibold text-gray-800">{n.title}</p>
              <p className="text-xs text-gray-600 mt-0.5 leading-relaxed">{n.body}</p>
              <p className="text-[10px] text-gray-400 mt-1.5">
                {fDateTime(n.createdAt)}
              </p>
            </div>
            {!n.isRead && (
              <button
                type="button"
                onClick={(e) => { e.preventDefault(); e.stopPropagation(); dismissOne.mutate(n.id); }}
                className="text-gray-300 hover:text-gray-500 flex-shrink-0 self-start mt-0.5 text-base leading-none"
                aria-label="Dismiss">×</button>
            )}
          </div>
        ))}
      </div>
    </div>,
    document.body
  ) : null;

  return (
    <>
      <button
        ref={buttonRef}
        onClick={handleToggle}
        className={`relative p-1.5 rounded-lg ${variant === "dark" ? "hover:bg-white/10 text-slate-400 hover:text-white" : "hover:bg-gray-100 text-gray-600"}`}
        aria-label="Notifications"
        type="button"
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
      {dropdown}
    </>
  );
}
