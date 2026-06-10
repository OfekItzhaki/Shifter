"use client";

import { useEffect, useMemo, useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { useAuthStore } from "@/lib/store/authStore";
import { useRouter, usePathname } from "next/navigation";
import Link from "next/link";
import NotificationBell from "@/components/shell/NotificationBell";
import ShifterLogo from "@/components/shell/ShifterLogo";
import LanguageSwitcher from "@/components/LanguageSwitcher";
import DarkModeToggle from "@/components/DarkModeToggle";
import VerificationBanner from "@/components/shell/VerificationBanner";
import LegalLinks from "@/components/legal/LegalLinks";
import OnboardingProvider from "@/components/onboarding/OnboardingProvider";
import OnboardingPanel from "@/components/onboarding/OnboardingPanel";
import SpaceSwitcher from "@/components/shell/SpaceSwitcher";
import { useSpaceGuard } from "@/lib/hooks/useSpaceGuard";
import { useHasMounted } from "@/lib/hooks/useHasMounted";
import { getMe } from "@/lib/api/auth";

interface AppShellProps { children: React.ReactNode; }

const S = {
  sidebar: { width: 256, background: "var(--sidebar-bg)", borderInlineEnd: "1px solid var(--sidebar-border)", display: "flex", flexDirection: "column" as const, height: "100dvh", minHeight: 0, maxHeight: "100dvh", position: "fixed" as const, top: 0, left: 0, zIndex: 30, overflowY: "auto" as const },
  logo: { padding: "14px 12px", borderBottom: "1px solid var(--sidebar-section-border)", background: "var(--sidebar-bg)", display: "flex", alignItems: "center", gap: 8, textDecoration: "none" },
  nav: { flex: 1, padding: "12px 12px", display: "flex", flexDirection: "column" as const, gap: 2 },
  navLink: (active: boolean, admin: boolean) => ({
    display: "flex", alignItems: "center", gap: 10, padding: "9px 12px", borderRadius: 8,
    textDecoration: "none", fontSize: 14, fontWeight: 500, transition: "background 0.15s",
    background: active ? (admin ? "var(--sidebar-admin-active-bg)" : "var(--sidebar-link-active-bg)") : "transparent",
    color: active ? (admin ? "var(--sidebar-admin-active-fg)" : "var(--sidebar-link-active-fg)") : (admin ? "var(--sidebar-admin-fg)" : "var(--sidebar-link-fg)"),
  }),
  bottom: { padding: "12px", borderTop: "1px solid var(--sidebar-section-border)" },
  userInfo: { padding: "8px 12px", marginBottom: 4 },
  logoutBtn: { display: "flex", alignItems: "center", gap: 10, width: "100%", padding: "9px 12px", borderRadius: 8, background: "none", border: "none", cursor: "pointer", color: "var(--sidebar-muted)", fontSize: 14, textAlign: "start" as const },
  topbar: (admin: boolean) => ({ height: 56, display: "flex", alignItems: "center", justifyContent: "flex-end", padding: "0 24px", borderBottom: `1px solid ${admin ? "#fde68a" : "var(--border-color, #e2e8f0)"}`, background: admin ? "#fffbeb" : "var(--main-bg, #f8fafc)", position: "sticky" as const, top: 0, zIndex: 20 }),
  main: { marginLeft: 256, display: "flex", flexDirection: "column" as const, minHeight: "100dvh", width: "calc(100vw - 256px)", background: "var(--main-bg, #f8fafc)" },
  content: { flex: 1, padding: "clamp(16px, 3vw, 32px)", width: "100%", maxWidth: "1400px", margin: "0 auto", display: "flex", flexDirection: "column" as const, alignItems: "center" },
};

function NavItem({ href, label, icon, admin, onNavigate }: { href: string; label: string; icon: React.ReactNode; admin?: boolean; onNavigate?: () => void }) {
  const pathname = usePathname();
  const active = pathname === href || pathname.startsWith(href + "/");
  return (
    <Link href={href} style={S.navLink(active, !!admin)} onClick={onNavigate}>
      <span style={{ flexShrink: 0, display: "flex" }}>{icon}</span>
      <span>{label}</span>
      {active && <span style={{ marginInlineStart: "auto", width: 6, height: 6, borderRadius: "50%", background: admin ? "#fbbf24" : "#0ea5e9" }} />}
    </Link>
  );
}

function NavSection({ label, admin }: { label: string; admin?: boolean }) {
  const lineColor = admin ? "rgba(251,191,36,0.35)" : "rgba(148,163,184,0.24)";
  const textColor = admin ? "var(--sidebar-admin-fg)" : "var(--sidebar-muted)";
  return (
    <div
      style={{
        margin: "14px 4px 4px",
        padding: "10px 8px 4px",
        display: "flex",
        alignItems: "center",
        gap: 8,
      }}
    >
      <span
        style={{
          color: textColor,
          fontSize: 11,
          fontWeight: 700,
          textTransform: "uppercase",
          flexShrink: 0,
        }}
      >
        {label}
      </span>
      <div style={{ flex: 1, height: 1, backgroundColor: lineColor }} />
    </div>
  );
}


export default function AppShell({ children }: AppShellProps) {
  const t = useTranslations();
  const locale = useLocale();
  const hasMounted = useHasMounted();
  const { displayName: storedDisplayName, logout, isPlatformAdmin } = useAuthStore();
  const router = useRouter();
  const [resolvedName, setResolvedName] = useState<string | null>(null);
  const [sidebarOpen, setSidebarOpen] = useState(false);

  // Space membership guard — handles redirects, migration, and space validation
  const { isReady: spaceGuardReady } = useSpaceGuard();

  // Fetch display name from API on mount, but only if not already in store
  useEffect(() => {
    if (!hasMounted) return;

    getMe().then(me => {
      if (me.displayName) setResolvedName(me.displayName);
      useAuthStore.getState().syncFromMe(me);
    }).catch(() => {
      if (storedDisplayName) setResolvedName(storedDisplayName);
    });
  }, [hasMounted, storedDisplayName]);

  const displayName = useMemo(() => resolvedName ?? storedDisplayName, [resolvedName, storedDisplayName]);
  const showPlatformAdmin = hasMounted && isPlatformAdmin;
  const dir = locale === "he" ? "rtl" : "ltr";

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
        dir={dir}
      >
        <div style={{ ...S.logo, textDecoration: "none" }}>
          <Link href="/home" className="sidebar-brand-home-indicator" aria-label="Go to home" title="Go to home">
            {ic("M3 12l9-9 9 9M5 10v10a1 1 0 001 1h4m4 0h4a1 1 0 001-1V10M9 21v-6a1 1 0 011-1h4a1 1 0 011 1v6")}
          </Link>
          <Link href="/home" className="sidebar-brand-link" aria-label="Go to home" title="Go to home">
            <span className="sidebar-brand-lockup">
              <span className="sidebar-brand-logo-chip">
                <ShifterLogo size={28} variant="icon" className="sidebar-brand-logo" />
              </span>
              <span className="sidebar-brand-copy">
                <span className="sidebar-brand-name">Shifter</span>
                <span className="sidebar-brand-tagline">Smart Shift Scheduling</span>
              </span>
            </span>
          </Link>
          {/* NotificationBell is OUTSIDE the Link so clicks don't navigate */}
          <NotificationBell variant="light" />
        </div>

        {/* Space switcher and current-space actions */}
        <div style={{ padding: "4px 12px 10px", borderBottom: "1px solid var(--sidebar-section-border)" }}>
          <SpaceSwitcher />
          <NavSection label={t("nav.sections.space")} />
          <div style={{ marginTop: 2 }}>
            <NavItem href="/spaces/settings" label={t("spaces.settings")} icon={ic("M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4")} onNavigate={() => setSidebarOpen(false)} />
          </div>
        </div>

        <nav style={S.nav}>
          <NavSection label={t("nav.sections.work")} />
          {/* Primary — daily use */}
          <NavItem href="/schedule/my-missions" label={t("nav.myMissions")} icon={ic("M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01")} onNavigate={() => setSidebarOpen(false)} />
          <NavItem href="/groups" label={t("nav.myGroups")} icon={ic("M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z")} onNavigate={() => setSidebarOpen(false)} />

          {/* Account */}
          <NavSection label={t("nav.sections.account")} />
          <NavItem href="/profile" label={t("nav.myProfile")} icon={ic("M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z")} onNavigate={() => setSidebarOpen(false)} />
          <NavItem href="/settings" label={t("nav.settings")} icon={ic("M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.066 2.573c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.573 1.066c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.066-2.573c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z")} onNavigate={() => setSidebarOpen(false)} />
          {/* Admin */}
          {showPlatformAdmin && (
            <>
              <NavSection label={t("nav.sections.admin")} admin />
              <NavItem href="/platform" label={t("nav.platform")} icon={ic("M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4")} admin onNavigate={() => setSidebarOpen(false)} />
            </>
          )}
        </nav>

        <div style={S.bottom}>
          {/* Language switcher */}
          <LanguageSwitcher />
          {/* Dark mode toggle */}
          <DarkModeToggle />

          {/* User info — always shown */}
          <div style={{ ...S.userInfo, display: "flex", alignItems: "center", gap: 10 }}>
            <div style={{
              width: 32, height: 32, borderRadius: "50%", background: "#0ea5e9",
              display: "flex", alignItems: "center", justifyContent: "center",
              color: "white", fontSize: 13, fontWeight: 700, flexShrink: 0
            }}>
              {displayName ? displayName.charAt(0).toUpperCase() : "?"}
            </div>
            <div style={{ minWidth: 0 }}>
              <div style={{ color: "var(--sidebar-muted)", fontSize: 10, marginBottom: 1 }}>{t("auth.loggedInAs")}</div>
              <div style={{ color: "var(--sidebar-text)", fontSize: 13, fontWeight: 600, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
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
          <div style={{ padding: "4px 12px 8px", color: "var(--sidebar-muted)", fontSize: 11, textAlign: "center" }}>
            v{process.env.NEXT_PUBLIC_APP_VERSION ?? "dev"}
            <span style={{ margin: "0 4px" }}>·</span>
            <a href="https://ofeklabs.com" target="_blank" rel="noopener noreferrer" style={{ color: "inherit", textDecoration: "none" }}>
              ofeklabs.com
            </a>
          </div>
          <div style={{ padding: "0 12px 12px" }}>
            <LegalLinks compact />
          </div>
        </div>
      </aside>

      {/* Main */}
      <div style={S.main} className="main-content">
        <header style={S.topbar(false)} className="mobile-topbar flex items-center gap-3 px-4">
          <button
            onClick={() => setSidebarOpen(o => !o)}
            style={{ background: "none", border: "none", cursor: "pointer", padding: 8, borderRadius: 8 }}
            className="text-slate-900 dark:text-slate-100"
            aria-label="Toggle menu"
          >
            <svg width="22" height="22" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M4 6h16M4 12h16M4 18h16" />
            </svg>
          </button>
          <div style={{ flex: 1, display: "flex", alignItems: "center", gap: 8 }}>
            <ShifterLogo size={24} />
            <span className="font-bold text-[15px] text-slate-900 dark:text-white">Shifter</span>
          </div>
          <NotificationBell />
        </header>
        <header style={{ ...S.topbar(false), display: "none" }} className="desktop-topbar">
          {/* desktop topbar — empty, admin mode indicator shown per-group */}
        </header>
        <OnboardingProvider>
          <main style={S.content} className="bg-slate-50 dark:bg-slate-900">
            <VerificationBanner />
            {children}
          </main>
          <OnboardingPanel />
        </OnboardingProvider>
      </div>
    </div>
  );
}
