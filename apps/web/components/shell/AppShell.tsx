"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { useAuthStore } from "@/lib/store/authStore";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useRouter, usePathname } from "next/navigation";
import Link from "next/link";
import NotificationBell from "@/components/shell/NotificationBell";
import ShifterLogo from "@/components/shell/ShifterLogo";
import LanguageSwitcher from "@/components/LanguageSwitcher";
import { getMySpaces } from "@/lib/api/spaces";
import { getMe } from "@/lib/api/auth";

interface AppShellProps { children: React.ReactNode; }

const S = {
  sidebar: { width: 256, background: "#0f172a", display: "flex", flexDirection: "column" as const, height: "100vh", position: "fixed" as const, top: 0, left: 0, zIndex: 30, overflowY: "auto" as const },
  logo: { padding: "20px 16px", borderBottom: "1px solid rgba(255,255,255,0.08)", display: "flex", alignItems: "center", gap: 10, textDecoration: "none" },
  logoIcon: { width: 32, height: 32, borderRadius: 8, background: "#3b82f6", display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 },
  nav: { flex: 1, padding: "12px 12px", display: "flex", flexDirection: "column" as const, gap: 2 },
  navLink: (active: boolean, admin: boolean) => ({
    display: "flex", alignItems: "center", gap: 10, padding: "9px 12px", borderRadius: 8,
    textDecoration: "none", fontSize: 14, fontWeight: 500, transition: "background 0.15s",
    background: active ? (admin ? "rgba(245,158,11,0.15)" : "rgba(59,130,246,0.15)") : "transparent",
    color: active ? (admin ? "#fbbf24" : "#93c5fd") : (admin ? "rgba(251,191,36,0.7)" : "#94a3b8"),
  }),
  bottom: { padding: "12px", borderTop: "1px solid rgba(255,255,255,0.08)" },
  userInfo: { padding: "8px 12px", marginBottom: 4 },
  logoutBtn: { display: "flex", alignItems: "center", gap: 10, width: "100%", padding: "9px 12px", borderRadius: 8, background: "none", border: "none", cursor: "pointer", color: "#64748b", fontSize: 14, textAlign: "left" as const },
  topbar: (admin: boolean) => ({ height: 56, display: "flex", alignItems: "center", justifyContent: "flex-end", padding: "0 24px", borderBottom: `1px solid ${admin ? "#fde68a" : "#e2e8f0"}`, background: admin ? "#fffbeb" : "white", position: "sticky" as const, top: 0, zIndex: 20 }),
  main: { marginLeft: 256, display: "flex", flexDirection: "column" as const, minHeight: "100vh", width: "calc(100vw - 256px)" },
  content: { flex: 1, padding: 32, background: "#f8fafc", width: "100%" },
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
  const { displayName: storedDisplayName, logout } = useAuthStore();
  const { currentSpaceId, currentSpaceName, setCurrentSpace } = useSpaceStore();
  const router = useRouter();
  const [resolvedName, setResolvedName] = useState<string | null>(storedDisplayName);
  const [sidebarOpen, setSidebarOpen] = useState(false);

  // Fetch display name from API on mount, but only if not already in store
  useEffect(() => {
    if (storedDisplayName) {
      setResolvedName(storedDisplayName);
      return;
    }
    getMe().then(me => {
      if (me.displayName) setResolvedName(me.displayName);
    }).catch(() => {
      // Fall back to whatever Zustand has
      if (storedDisplayName) setResolvedName(storedDisplayName);
    });
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const displayName = resolvedName;

  useEffect(() => {
    getMySpaces().then(spaces => {
      if (spaces.length === 0) return;
      const storedIsValid = currentSpaceId && spaces.some(s => s.id === currentSpaceId);
      if (!storedIsValid) {
        setCurrentSpace(spaces[0].id, spaces[0].name);
      }
    }).catch(() => {});
  }, [currentSpaceId]);

  async function handleLogout() { await logout(); router.push("/login"); }

  const ic = (d: string) => (
    <svg width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}>
      <path strokeLinecap="round" strokeLinejoin="round" d={d} />
    </svg>
  );

  return (
    <div style={{ display: "flex" }}>
      {/* Mobile overlay */}
      {sidebarOpen && (
        <div
          style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,0.5)", zIndex: 29 }}
          onClick={() => setSidebarOpen(false)}
        />
      )}

      {/* Sidebar */}
      <aside style={{
        ...S.sidebar,
        transform: sidebarOpen ? "translateX(0)" : undefined,
      }}
        className={`sidebar-nav ${sidebarOpen ? "sidebar-open" : ""}`}
      >
        <div style={{ ...S.logo, textDecoration: "none" }}>
          <Link href="/spaces" style={{ display: "flex", alignItems: "center", gap: 10, textDecoration: "none", flex: 1, minWidth: 0 }}>
            <ShifterLogo size={32} />
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ color: "white", fontWeight: 700, fontSize: 14, lineHeight: 1.2 }}>Shifter</div>
              {currentSpaceName && <div style={{ color: "#64748b", fontSize: 11, marginTop: 1 }}>{currentSpaceName}</div>}
            </div>
          </Link>
          {/* NotificationBell is OUTSIDE the Link so clicks don't navigate */}
          <NotificationBell />
        </div>

        <nav style={S.nav}>
          <NavItem href="/profile" label={t("nav.myProfile")} icon={ic("M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z")} />
          <NavItem href="/schedule/my-missions" label={t("nav.myMissions")} icon={ic("M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01")} />
          <NavItem href="/groups" label={t("nav.myGroups")} icon={ic("M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z")} />
        </nav>

        <div style={S.bottom}>
          {/* Language switcher */}
          <LanguageSwitcher />

          {/* User info — always shown */}
          <div style={{ ...S.userInfo, display: "flex", alignItems: "center", gap: 10 }}>
            <div style={{
              width: 32, height: 32, borderRadius: "50%", background: "#3b82f6",
              display: "flex", alignItems: "center", justifyContent: "center",
              color: "white", fontSize: 13, fontWeight: 700, flexShrink: 0
            }}>
              {displayName ? displayName.charAt(0).toUpperCase() : "?"}
            </div>
            <div style={{ minWidth: 0 }}>
              <div style={{ color: "#94a3b8", fontSize: 10, marginBottom: 1 }}>{t("auth.loggedInAs")}</div>
              <div style={{ color: "white", fontSize: 13, fontWeight: 600, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                {displayName ?? "—"}
              </div>
            </div>
          </div>
          <button onClick={handleLogout} style={S.logoutBtn} aria-label={t("auth.logout")} data-testid="logout-btn">
            <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
            </svg>
            {t("auth.logout")}
          </button>
          <div style={{ padding: "4px 12px 8px", color: "#334155", fontSize: 11, opacity: 0.4, textAlign: "center" }}>
            v{process.env.NEXT_PUBLIC_APP_VERSION ?? "1.5.0"}
          </div>
        </div>
      </aside>

      {/* Main */}
      <div style={S.main} className="main-content">
        <header style={S.topbar(false)} className="mobile-topbar flex items-center gap-3 px-4">
          <button
            onClick={() => setSidebarOpen(o => !o)}
            style={{ background: "none", border: "none", cursor: "pointer", padding: 4, color: "#64748b" }}
            aria-label="Toggle menu"
          >
            <svg width="22" height="22" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M4 6h16M4 12h16M4 18h16" />
            </svg>
          </button>
          <span style={{ fontWeight: 700, fontSize: 15, color: "#0f172a" }}>Shifter</span>
        </header>
        <header style={{ ...S.topbar(false), display: "none" }} className="desktop-topbar">
          {/* desktop topbar — empty, admin mode indicator shown per-group */}
        </header>
        <main style={S.content}>{children}</main>
      </div>
    </div>
  );
}
