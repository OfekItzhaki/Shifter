"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import ShifterLogo from "@/components/shell/ShifterLogo";
import { clearAuthGuardCookie, setLocaleCookie } from "@/lib/auth/authGuardCookie";
import { notifyAuthTokenChanged } from "@/lib/auth/tokenState";
import { useEffectiveAuth } from "@/lib/hooks/useEffectiveAuth";
import { LOCALE_META, SUPPORTED_LOCALES, getLocaleDirection } from "@/lib/i18n/locales";
import { LANDING_CONTENT, LANDING_LEGAL_LINKS, type LandingLang } from "./landingContent";

const accentClasses = [
  "border-sky-200 bg-sky-50 text-sky-800",
  "border-emerald-200 bg-emerald-50 text-emerald-800",
  "border-amber-200 bg-amber-50 text-amber-800",
  "border-rose-200 bg-rose-50 text-rose-800",
];

export default function LandingPage({ initialLocale }: { initialLocale: LandingLang }) {
  const router = useRouter();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [lang, setLang] = useState<LandingLang>(initialLocale);
  const { hasAccessToken } = useEffectiveAuth();

  useEffect(() => {
    if (!hasAccessToken) return;

    const controller = new AbortController();
    const token = localStorage.getItem("access_token");
    if (!token) return;
    fetch(`${process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000"}/auth/me`, {
      headers: { Authorization: `Bearer ${token}` },
      signal: controller.signal,
    })
      .then(res => {
        if (res.ok) {
          router.replace("/schedule/my-missions");
          return;
        }
        localStorage.removeItem("access_token");
        localStorage.removeItem("refresh_token");
        notifyAuthTokenChanged();
        clearAuthGuardCookie();
      })
      .catch(() => undefined);

    return () => controller.abort();
  }, [hasAccessToken, router]);

  const c = LANDING_CONTENT[lang];
  const finder = c.finder ?? LANDING_CONTENT.en.finder!;

  function switchLang(nextLang: LandingLang) {
    setLang(nextLang);
    setLocaleCookie(nextLang);
    document.documentElement.setAttribute("lang", nextLang);
    document.documentElement.setAttribute("dir", getLocaleDirection(nextLang));
  }

  return (
    <div dir={c.dir} className="min-h-screen bg-slate-50 text-slate-950">
      <nav className="sticky top-0 z-50 border-b border-slate-200 bg-white/90 backdrop-blur">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-4 py-3 sm:px-6">
          <Link href="/" className="flex min-w-0 items-center gap-3" aria-label="Shifter home">
            <ShifterLogo size={34} />
            <div className="hidden leading-tight sm:block">
              <p className="text-sm font-bold text-slate-950">Shifter</p>
              <p className="text-xs text-slate-500">Smart Shift Scheduling</p>
            </div>
          </Link>

          <div className="hidden items-center gap-6 text-sm font-medium text-slate-600 lg:flex">
            <a href="#product" className="hover:text-slate-950">{c.nav.product}</a>
            <a href="#features" className="hover:text-slate-950">{c.nav.features}</a>
            <a href="#teams" className="hover:text-slate-950">{c.nav.teams}</a>
            <a href="#faq" className="hover:text-slate-950">{c.nav.faq}</a>
            <a href="#contact" className="hover:text-slate-950">{c.nav.contact}</a>
          </div>

          <div className="flex items-center gap-2">
            <div className="hidden items-center rounded-lg border border-slate-200 bg-slate-100 p-1 sm:flex">
              {SUPPORTED_LOCALES.map(locale => (
                <button
                  key={locale}
                  onClick={() => switchLang(locale)}
                  className={`rounded-md px-2.5 py-1 text-xs font-semibold transition ${
                    lang === locale ? "bg-white text-slate-950 shadow-sm" : "text-slate-500 hover:text-slate-900"
                  }`}
                >
                  {LOCALE_META[locale].label}
                </button>
              ))}
            </div>
            <Link href="/login" className="hidden rounded-lg px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-100 sm:inline-flex">
              {c.nav.signIn}
            </Link>
            <Link href="/register" className="rounded-lg bg-slate-950 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-800">
              {c.nav.getStarted}
            </Link>
            <button
              type="button"
              onClick={() => setMobileMenuOpen(open => !open)}
              className="rounded-lg border border-slate-200 p-2 text-slate-700 lg:hidden"
              aria-label="Menu"
            >
              <span className="block h-0.5 w-5 bg-current" />
              <span className="mt-1.5 block h-0.5 w-5 bg-current" />
              <span className="mt-1.5 block h-0.5 w-5 bg-current" />
            </button>
          </div>
        </div>

        {mobileMenuOpen && (
          <div className="border-t border-slate-200 bg-white px-4 py-4 lg:hidden">
            <div className="mx-auto grid max-w-7xl gap-3 text-sm font-medium text-slate-700">
              {[
                ["#product", c.nav.product],
                ["#features", c.nav.features],
                ["#teams", c.nav.teams],
                ["#faq", c.nav.faq],
                ["#contact", c.nav.contact],
              ].map(([href, label]) => (
                <a key={href} href={href} onClick={() => setMobileMenuOpen(false)} className="rounded-lg px-2 py-2 hover:bg-slate-100">
                  {label}
                </a>
              ))}
              <Link href="/login" onClick={() => setMobileMenuOpen(false)} className="rounded-lg px-2 py-2 text-sky-700 hover:bg-sky-50">
                {c.nav.signIn}
              </Link>
              <div className="flex flex-wrap gap-2 pt-2">
                {SUPPORTED_LOCALES.map(locale => (
                  <button
                    key={locale}
                    onClick={() => {
                      switchLang(locale);
                      setMobileMenuOpen(false);
                    }}
                    className={`rounded-lg border px-3 py-1.5 text-xs font-semibold ${
                      lang === locale ? "border-sky-300 bg-sky-50 text-sky-800" : "border-slate-200 text-slate-500"
                    }`}
                  >
                    {LOCALE_META[locale].label}
                  </button>
                ))}
              </div>
            </div>
          </div>
        )}
      </nav>

      <main>
        <section id="product" className="scroll-mt-24 border-b border-slate-200 bg-white">
          <div className="mx-auto grid max-w-7xl items-center gap-10 px-4 py-12 sm:px-6 sm:py-16 lg:grid-cols-[0.9fr_1.1fr] lg:py-20">
            <div className="max-w-2xl">
              <p className="mb-4 inline-flex rounded-lg border border-sky-200 bg-sky-50 px-3 py-1 text-sm font-semibold text-sky-800">
                {c.hero.eyebrow}
              </p>
              <h1 className="max-w-3xl text-4xl font-black leading-tight tracking-normal text-slate-950 sm:text-5xl lg:text-6xl">
                {c.hero.title}
              </h1>
              <p className="mt-5 max-w-2xl text-lg leading-8 text-slate-600">
                {c.hero.subtitle}
              </p>
              <div className="mt-8 flex flex-col gap-3 sm:flex-row">
                <Link href="/register" className="inline-flex justify-center rounded-lg bg-slate-950 px-6 py-3 text-sm font-bold text-white hover:bg-slate-800">
                  {c.hero.primary}
                </Link>
                <Link href="/login" className="inline-flex justify-center rounded-lg border border-slate-300 bg-white px-6 py-3 text-sm font-bold text-slate-800 hover:bg-slate-50">
                  {c.hero.secondary}
                </Link>
              </div>
              <p className="mt-4 text-sm font-medium text-emerald-700">{c.hero.install}</p>
            </div>

            <ProductPreview c={c.preview} />
          </div>

          <div className="mx-auto grid max-w-7xl grid-cols-2 gap-px border-t border-slate-200 bg-slate-200 sm:grid-cols-4">
            {c.proof.map(item => (
              <div key={item.label} className="bg-slate-50 px-4 py-5 text-center">
                <p className="text-2xl font-black text-slate-950">{item.value}</p>
                <p className="mt-1 text-sm font-medium text-slate-600">{item.label}</p>
              </div>
            ))}
          </div>

          <LandingFinder finder={finder} />
        </section>

        <section id="features" className="scroll-mt-24 px-4 py-16 sm:px-6 lg:py-20">
          <div className="mx-auto max-w-7xl">
            <div className="max-w-3xl">
              <h2 className="text-3xl font-black tracking-normal text-slate-950 sm:text-4xl">{c.features.title}</h2>
              <p className="mt-4 text-lg leading-8 text-slate-600">{c.features.subtitle}</p>
            </div>
            <div className="mt-10 grid gap-4 md:grid-cols-2">
              {c.features.items.map((feature, index) => (
                <article key={feature.title} className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
                  <div className="flex items-start justify-between gap-4">
                    <div>
                      <p className="text-lg font-black text-slate-950">{feature.title}</p>
                      <p className="mt-3 text-sm leading-6 text-slate-600">{feature.desc}</p>
                    </div>
                    <span className={`shrink-0 rounded-lg border px-3 py-1 text-xs font-bold ${accentClasses[index % accentClasses.length]}`}>
                      {feature.detail}
                    </span>
                  </div>
                </article>
              ))}
            </div>
          </div>
        </section>

        <section id="teams" className="scroll-mt-24 bg-slate-950 px-4 py-16 text-white sm:px-6 lg:py-20">
          <div className="mx-auto grid max-w-7xl gap-10 lg:grid-cols-[0.8fr_1.2fr]">
            <div>
              <h2 className="text-3xl font-black tracking-normal sm:text-4xl">{c.teams.title}</h2>
              <p className="mt-4 text-lg leading-8 text-slate-300">{c.teams.subtitle}</p>
            </div>
            <div className="grid gap-3 sm:grid-cols-2">
              {c.teams.items.map((team, index) => (
                <div key={team} className="rounded-lg border border-white/10 bg-white/5 p-4">
                  <div className="mb-4 h-1.5 w-16 rounded-full bg-gradient-to-r from-sky-400 via-emerald-300 to-amber-300" />
                  <p className="font-bold">{team}</p>
                  <p className="mt-2 text-sm text-slate-400">{c.preview.alerts[index % c.preview.alerts.length]}</p>
                </div>
              ))}
            </div>
          </div>
        </section>

        <section className="px-4 py-16 sm:px-6 lg:py-20">
          <div className="mx-auto max-w-7xl">
            <h2 className="max-w-3xl text-3xl font-black tracking-normal text-slate-950 sm:text-4xl">{c.workflow.title}</h2>
            <div className="mt-10 grid gap-4 md:grid-cols-3">
              {c.workflow.steps.map((step, index) => (
                <article key={step.title} className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
                  <span className="inline-flex h-9 w-9 items-center justify-center rounded-lg bg-slate-950 text-sm font-black text-white">
                    {index + 1}
                  </span>
                  <h3 className="mt-5 text-lg font-black text-slate-950">{step.title}</h3>
                  <p className="mt-3 text-sm leading-6 text-slate-600">{step.desc}</p>
                </article>
              ))}
            </div>
          </div>
        </section>

        <section className="border-y border-slate-200 bg-white px-4 py-12 sm:px-6">
          <div className="mx-auto grid max-w-7xl gap-4 md:grid-cols-3">
            {c.trust.map(item => {
              const body = (
                <article className="h-full rounded-lg border border-slate-200 p-5 transition hover:border-slate-300 hover:shadow-sm">
                  <h3 className="font-black text-slate-950">{item.title}</h3>
                  <p className="mt-2 text-sm leading-6 text-slate-600">{item.desc}</p>
                </article>
              );
              return item.href ? (
                <Link key={item.title} href={item.href} className="block">
                  {body}
                </Link>
              ) : (
                <div key={item.title}>{body}</div>
              );
            })}
          </div>
        </section>

        <section id="contact" className="scroll-mt-24 px-4 py-16 sm:px-6 lg:py-20">
          <div className="mx-auto grid max-w-7xl overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm lg:grid-cols-[1fr_0.85fr]">
            <div className="p-6 sm:p-8 lg:p-10">
              <h2 className="text-3xl font-black tracking-normal text-slate-950">{c.contact.title}</h2>
              <p className="mt-4 max-w-2xl text-base leading-7 text-slate-600">{c.contact.subtitle}</p>
              <div className="mt-8 flex flex-col gap-3 sm:flex-row">
                <a href="mailto:support@shifter.app?subject=Shifter%20support" className="inline-flex justify-center rounded-lg bg-slate-950 px-5 py-3 text-sm font-bold text-white hover:bg-slate-800">
                  {c.contact.email}
                </a>
                <a href="mailto:support@shifter.app?subject=Shifter%20walkthrough" className="inline-flex justify-center rounded-lg border border-slate-300 px-5 py-3 text-sm font-bold text-slate-800 hover:bg-slate-50">
                  {c.contact.demo}
                </a>
              </div>
            </div>
            <div className="border-t border-slate-200 bg-slate-100 p-6 lg:border-l lg:border-t-0">
              <MiniOpsPanel alerts={c.preview.alerts} />
            </div>
          </div>
        </section>

        <section id="faq" className="scroll-mt-24 bg-slate-100 px-4 py-16 sm:px-6 lg:py-20">
          <div className="mx-auto max-w-4xl">
            <div className="text-center">
              <h2 className="text-3xl font-black tracking-normal text-slate-950 sm:text-4xl">{c.faq.title}</h2>
              <p className="mt-3 text-base text-slate-600">{c.faq.subtitle}</p>
            </div>
            <div className="mt-10 grid gap-3">
              {c.faq.items.map(item => (
                <details key={item.q} className="group rounded-lg border border-slate-200 bg-white px-5 py-4 shadow-sm">
                  <summary className="flex cursor-pointer list-none items-center justify-between gap-4 text-sm font-black text-slate-950">
                    {item.q}
                    <span className="grid h-7 w-7 shrink-0 place-items-center rounded-lg bg-slate-100 text-slate-500 group-open:rotate-180">v</span>
                  </summary>
                  <p className="mt-4 text-sm leading-6 text-slate-600">{item.a}</p>
                </details>
              ))}
            </div>
          </div>
        </section>

        <section className="bg-white px-4 py-16 text-center sm:px-6 lg:py-20">
          <div className="mx-auto max-w-3xl">
            <h2 className="text-3xl font-black tracking-normal text-slate-950 sm:text-4xl">{c.cta.title}</h2>
            <p className="mt-4 text-lg leading-8 text-slate-600">{c.cta.subtitle}</p>
            <div className="mt-8 flex flex-col justify-center gap-3 sm:flex-row">
              <Link href="/register" className="inline-flex justify-center rounded-lg bg-slate-950 px-6 py-3 text-sm font-bold text-white hover:bg-slate-800">
                {c.cta.primary}
              </Link>
              <Link href="/login" className="inline-flex justify-center rounded-lg border border-slate-300 px-6 py-3 text-sm font-bold text-slate-800 hover:bg-slate-50">
                {c.cta.secondary}
              </Link>
            </div>
          </div>
        </section>
      </main>

      <footer className="border-t border-slate-200 bg-slate-950 px-4 py-8 text-white sm:px-6">
        <div className="mx-auto flex max-w-7xl flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-3">
            <ShifterLogo size={28} />
            <span className="text-sm font-semibold text-slate-300">Shifter &copy; {new Date().getFullYear()}</span>
          </div>
          <div className="flex flex-wrap gap-x-5 gap-y-2 text-sm text-slate-400">
            <a href="#product" className="hover:text-white">{c.footer.product}</a>
            <a href="#faq" className="hover:text-white">{c.footer.faq}</a>
            {LANDING_LEGAL_LINKS[lang].map(link => (
              <Link key={link.href} href={link.href} className="hover:text-white">
                {link.label}
              </Link>
            ))}
            <Link href="/login" className="hover:text-white">{c.footer.signIn}</Link>
          </div>
        </div>
      </footer>
    </div>
  );
}

function LandingFinder({ finder }: { finder: NonNullable<(typeof LANDING_CONTENT)[LandingLang]["finder"]> }) {
  const [query, setQuery] = useState("");
  const normalizedQuery = query.trim().toLowerCase();
  const matches = useMemo(() => {
    if (!normalizedQuery) return finder.items;

    return finder.items.filter((item) => {
      const haystack = [
        item.label,
        item.desc,
        ...item.keywords,
      ].join(" ").toLowerCase();
      return haystack.includes(normalizedQuery);
    });
  }, [finder.items, normalizedQuery]);

  return (
    <div className="border-t border-slate-200 bg-slate-50 px-4 py-6 sm:px-6">
      <div className="mx-auto grid max-w-7xl gap-4 lg:grid-cols-[0.35fr_0.65fr] lg:items-start">
        <div>
          <h2 className="text-lg font-black text-slate-950">{finder.title}</h2>
          <div className="mt-3 flex items-center gap-2 rounded-lg border border-slate-300 bg-white px-3 py-2 shadow-sm focus-within:border-sky-400 focus-within:ring-2 focus-within:ring-sky-100">
            <span aria-hidden="true" className="text-sm font-black text-slate-400">⌕</span>
            <input
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder={finder.placeholder}
              className="min-w-0 flex-1 bg-transparent text-sm text-slate-900 outline-none placeholder:text-slate-400"
              type="search"
            />
          </div>
        </div>

        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          {matches.length === 0 ? (
            <p className="rounded-lg border border-slate-200 bg-white px-4 py-3 text-sm text-slate-600">
              {finder.empty}
            </p>
          ) : (
            matches.slice(0, 6).map((item) => {
              const body = (
                <span className="block h-full rounded-lg border border-slate-200 bg-white p-4 text-start shadow-sm transition hover:border-sky-200 hover:shadow-md">
                  <span className="block text-sm font-black text-slate-950">{item.label}</span>
                  <span className="mt-2 block text-xs leading-5 text-slate-600">{item.desc}</span>
                </span>
              );

              return item.href.startsWith("/") ? (
                <Link key={item.label} href={item.href} className="block">
                  {body}
                </Link>
              ) : (
                <a key={item.label} href={item.href} className="block">
                  {body}
                </a>
              );
            })
          )}
        </div>
      </div>
    </div>
  );
}

function ProductPreview({ c }: { c: (typeof LANDING_CONTENT)[LandingLang]["preview"] }) {
  return (
    <div className="relative">
      <div className="rounded-lg border border-slate-200 bg-slate-950 p-3 shadow-2xl shadow-slate-300/50">
        <div className="rounded-lg bg-slate-900 p-4">
          <div className="flex items-center justify-between border-b border-white/10 pb-4">
            <div>
              <p className="text-xs font-semibold uppercase text-sky-300">{c.workspace}</p>
              <p className="mt-1 text-lg font-black text-white">{c.scheduleTitle}</p>
              <p className="text-sm text-slate-400">{c.scheduleSubtitle}</p>
            </div>
            <div className="hidden rounded-lg bg-emerald-400 px-3 py-2 text-xs font-black text-emerald-950 sm:block">
              Publish
            </div>
          </div>

          <div className="mt-4 grid gap-3 lg:grid-cols-[1fr_0.65fr]">
            <div className="grid gap-3">
              {c.shifts.map((shift, index) => (
                <div key={`${shift.time}-${shift.name}`} className="rounded-lg border border-white/10 bg-white/[0.04] p-4">
                  <div className="flex items-center justify-between gap-3">
                    <div>
                      <p className="text-sm font-black text-white">{shift.name}</p>
                      <p className="mt-1 text-xs text-slate-400">{shift.time}</p>
                    </div>
                    <span className={`rounded-lg px-2.5 py-1 text-xs font-bold ${
                      index === 0 ? "bg-emerald-400/15 text-emerald-200" : index === 1 ? "bg-amber-400/15 text-amber-200" : "bg-sky-400/15 text-sky-200"
                    }`}>
                      {shift.status}
                    </span>
                  </div>
                  <div className="mt-4 grid grid-cols-5 gap-1.5">
                    {[0, 1, 2, 3, 4].map(block => (
                      <span key={block} className={`h-2 rounded-full ${block <= index + 1 ? "bg-sky-400" : "bg-white/10"}`} />
                    ))}
                  </div>
                </div>
              ))}
            </div>

            <div className="grid gap-3">
              <div className="rounded-lg border border-sky-400/30 bg-sky-400/10 p-4 text-sky-100">
                <p className="text-xs font-bold uppercase text-sky-300">{c.importLabel}</p>
                <div className="mt-4 grid grid-cols-4 gap-1.5">
                  {[80, 44, 64, 90, 55, 74, 36, 70].map((height, index) => (
                    <span key={index} className="rounded-t bg-sky-300/80" style={{ height: `${height}px` }} />
                  ))}
                </div>
              </div>
              <div className="rounded-lg border border-amber-300/30 bg-amber-300/10 p-4 text-amber-100">
                <p className="text-sm font-black">{c.aiLabel}</p>
                <div className="mt-3 space-y-2">
                  {c.alerts.map(alert => (
                    <p key={alert} className="rounded-lg bg-black/20 px-3 py-2 text-xs text-amber-50">{alert}</p>
                  ))}
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div className="mx-auto -mt-10 w-48 rounded-[28px] border-4 border-slate-950 bg-white p-3 shadow-xl sm:absolute sm:-bottom-8 sm:end-6 sm:mt-0">
        <div className="mb-3 h-1.5 w-14 rounded-full bg-slate-200 mx-auto" />
        <p className="text-sm font-black text-slate-950">{c.phoneTitle}</p>
        <p className="text-xs text-slate-500">{c.phoneSubtitle}</p>
        <div className="mt-4 space-y-2">
          {c.shifts.slice(0, 2).map(shift => (
            <div key={shift.name} className="rounded-lg border border-slate-200 bg-slate-50 p-3">
              <p className="text-xs font-bold text-slate-950">{shift.time}</p>
              <p className="text-xs text-slate-600">{shift.name}</p>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function MiniOpsPanel({ alerts }: { alerts: string[] }) {
  return (
    <div className="rounded-lg bg-slate-950 p-5 text-white">
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm font-black">Ops snapshot</p>
        <span className="rounded-lg bg-emerald-400 px-2.5 py-1 text-xs font-black text-emerald-950">Live</span>
      </div>
      <div className="mt-5 grid grid-cols-3 gap-2">
        {[18, 7, 3].map((value, index) => (
          <div key={value} className="rounded-lg bg-white/10 p-3">
            <p className="text-xl font-black">{value}</p>
            <p className="text-xs text-slate-400">{["shifts", "roles", "alerts"][index]}</p>
          </div>
        ))}
      </div>
      <div className="mt-5 space-y-2">
        {alerts.map(alert => (
          <p key={alert} className="rounded-lg border border-white/10 bg-white/[0.04] px-3 py-2 text-xs text-slate-300">
            {alert}
          </p>
        ))}
      </div>
    </div>
  );
}
