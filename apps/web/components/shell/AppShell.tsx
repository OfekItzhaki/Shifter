"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { useAuthStore } from "@/lib/store/authStore";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useRouter, usePathname } from "next/navigation";
import Link from "next/link";
import NotificationBell from "@/components/shell/NotificationBell";

interface AppShellProps { children: React.ReactNode; }

const S = {
  sidebar: { width: 256, background: "#0f172a", display: "flex", flexDirection: "column" as const, height: "100vh", position: "fixed" as const, top: 0, left: 0, zIndex: 30, overflowY: "auto" as const },
  logo: { padding: "20px 16px", borderBottom: "1px solid rgba(255,255,255,0.08)", display: "flex", alignItems: "center", gap: 10, textDecoration: "none" },
  logoIcon: { width: 32, height: 32, borderRadius: 8, background: "#3b82f6", display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 },
  nav: { flex: 1, padding: "12px 12px", display: "flex", flexDirection: "column" as const, gap: 2 },
  sectionLabel: { fontSize: 10, fontWeight: 600, color: "#64748b", textTransform: "uppercase" as const, letterSpacing: "0.08em", padding: "8px 12px 4px" },
  navLink: (active: boolean, admin: boolean) => ({
    display: "flex", alignItems: "center", gap: 10, padding: "9px 12px", borderRadius: 8,
    textDecoration: "none", fontSize: 14, fontWeight: 500, transition: "background 0.15s",
    background: active ? (admin ? "rgba(245,158,11,0.15)" : "rgba(59,130,246,0.15)") : "transparent",
    color: active ? (admin ? "#fbbf24" : "#93c5fd") : (admin ? "rgba(251,191,36,0.7)" : "#94a3b8"),
  }),
  bottom: { padding: "12px", borderTop: "1px solid rgba(255,255,255,0.08)" },
  userInfo: { padding: "8px 12px", marginBottom: 4 },
  logoutBtn: { display: "flex", alignItems: "center", gap: 10, width: "100%", padding: "9px 12px", borderRadius: 8, background: "none", border: "none", cursor: "pointer", color: "#64748b", fontSize: 14, textAlign: "left" as const },
  topbar: (admin: boolean) => ({ height: 56, display: "flex", alignItems: "center", justifyContent: "space-between", padding: "0 24px", borderBottom: `1px solid ${admin ? "#fde68a" : "#e2e8f0"}`, background: admin ? "#fffbeb" : "white", position: "sticky" as const, top: 0, zIndex: 20 }),
  adminBadge: { display: "flex", alignItems: "center", gap: 6, padding: "4px 10px", borderRadius: 20, background: "#fef3c7", border: "1px solid #fde68a", fontSize: 11, fontWeight: 600, color: "#92400e", textTransform: "uppercase" as const, letterSpacing: "0.05em" },
  adminBtn: (exit: boolean) => ({ display: "flex", alignItems: "center", gap: 6, padding: "6px 12px", borderRadius: 8, border: `1px solid ${exit ? "#fde68a" : "#e2e8f0"}`, background: exit ? "#fef3c7" : "#f8fafc", color: exit ? "#92400e" : "#475569", fontSize: 12, fontWeight: 500, cursor: "pointer" }),
  main: { marginLeft: 256, display: "flex", flexDirection: "column" as const, minHeight: "100vh" },
  content: { flex: 1, padding: 32, background: "#f8fafc" },
};

function NavItem({ href, label, icon, admin }: { href: string; label: string; icon: React.ReactNode; admin?: boolean }) {
  const pathname = usePathname();
  const active = pathname === href || pathname.startsWith(href + "/");
  return (
    <Link href={href} style={S.navLink(active, !!admin)}>
      <span style={{ flexShrink: 0, display: "flex" }}>{icon}</span>
      <span>{label}</span>
      {active && <span style={{ marginLeft: "auto", width: 6, height: 6, borderRadius: "50%", background: admin ? "#fbbf24" : "#3b82f6" }} />}
    </Link>
  );
}

export default function AppShell({ children }: AppShellProps) {
  const t = useTranslations();
  const { displayName, isAdminMode, enterAdminMode, exitAdminMode, logout } = useAuthStore();
  const { currentSpaceName } = useSpaceStore();
  const router = useRouter();

  async function handleLogout() { await logout(); router.push("/login"); }

  const ic = (d: string) => (
    <svg width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}>
      <path strokeLinecap="round" strokeLinejoin="round" d={d} />
    </svg>
  );

  return (
    <div style={{ display: "flex" }}>
      {/* Sidebar */}
      <aside style={S.sidebar}>
        <Link href="/spaces" style={S.logo}>
          <div style={S.logoIcon}>
            <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="white" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M13 10V3L4 14h7v7l9-11h-7z" />
            </svg>
          </div>
          <div>
            <div style={{ color: "white", fontWeight: 700, fontSize: 14, lineHeight: 1.2 }}>Jobuler</div>
            {currentSpaceName && <div style={{ color: "#64748b", fontSize: 11, marginTop: 1 }}>{currentSpaceName}</div>}
          </div>
        </Link>

        <nav style={S.nav}>
          <div style={S.sectionLabel}>Schedule</div>
          <NavItem href="/schedule/today" label={t("nav.today")} icon={ic("M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z")} />
          <NavItem href="/schedule/tomorrow" label={t("nav.tomorrow")} icon={ic("M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z")} />

          {isAdminMode && (
            <>
              <div style={{ ...S.sectionLabel, marginTop: 12, color: "rgba(245,158,11,0.6)" }}>Admin</div>
              <NavItem href="/admin/schedule" label={t("admin.title")} admin icon={ic("M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2")} />
              <NavItem href="/admin/groups" label={t("admin.groups")} admin icon={ic("M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10")} />
              <NavItem href="/admin/tasks" label={t("admin.tasks")} admin icon={ic("M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z")} />
              <NavItem href="/admin/constraints" label={t("admin.constraints")} admin icon={ic("M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z")} />
              <NavItem href="/admin/logs" label={t("nav.logs")} admin icon={ic("M4 6h16M4 10h16M4 14h16M4 18h16")} />
            </>
          )}
        </nav>

        <div style={S.bottom}>
          {displayName && (
            <div style={S.userInfo}>
              <div style={{ color: "#64748b", fontSize: 11 }}>Signed in as</div>
              <div style={{ color: "white", fontSize: 13, fontWeight: 500, marginTop: 2 }}>{displayName}</div>
            </div>
          )}
          <button onClick={handleLogout} style={S.logoutBtn}>
            <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
            </svg>
            {t("auth.logout")}
          </button>
        </div>
      </aside>

      {/* Main */}
      <div style={S.main}>
        <header style={S.topbar(isAdminMode)}>
          <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
            {isAdminMode && (
              <div style={S.adminBadge}>
                <svg width="12" height="12" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
                </svg>
                Admin Mode
              </div>
            )}
          </div>
          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <NotificationBell />
            {isAdminMode ? (
              <button onClick={exitAdminMode} style={S.adminBtn(true)}>{t("admin.exitAdmin")}</button>
            ) : (
              <button onClick={enterAdminMode} style={S.adminBtn(false)}>
                <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
                </svg>
                {t("admin.enterAdmin")}
              </button>
            )}
          </div>
        </header>
        <main style={S.content}>{children}</main>
      </div>
    </div>
  );
}
