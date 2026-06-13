"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import ShifterLogo from "@/components/shell/ShifterLogo";
import { clearAuthGuardCookie, setLocaleCookie } from "@/lib/auth/authGuardCookie";
import { notifyAuthTokenChanged } from "@/lib/auth/tokenState";
import { useEffectiveAuth } from "@/lib/hooks/useEffectiveAuth";
import { LOCALE_META, SUPPORTED_LOCALES, getLocaleDirection } from "@/lib/i18n/locales";
import { buildSupportMailtoHref } from "@/lib/support/contact";
import { LANDING_CONTENT, LANDING_LEGAL_LINKS, type LandingLang } from "./landingContent";

const accentClasses = [
  "border-sky-200 bg-sky-50 text-sky-800",
  "border-emerald-200 bg-emerald-50 text-emerald-800",
  "border-amber-200 bg-amber-50 text-amber-800",
  "border-rose-200 bg-rose-50 text-rose-800",
];

interface LandingPageProps {
  initialLocale: LandingLang;
  supportEmail: string;
}

export default function LandingPage({ initialLocale, supportEmail }: LandingPageProps) {
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
  const finder = c.finder;
  const supportHref = buildSupportMailtoHref("Shifter support", supportEmail);
  const walkthroughHref = buildSupportMailtoHref("Shifter walkthrough", supportEmail);

  useEffect(() => {
    setLocaleCookie(lang);
    document.documentElement.setAttribute("lang", lang);
    document.documentElement.setAttribute("dir", getLocaleDirection(lang));
  }, [lang]);

  function switchLang(nextLang: LandingLang) {
    setLang(nextLang);
  }

  return (
    <div dir={c.dir} className="min-h-screen bg-slate-50 text-slate-950">
      <nav className="sticky top-0 z-50 border-b border-slate-200 bg-white/90 backdrop-blur">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-4 py-3 sm:px-6">
          <Link href="/" className="flex min-w-0 items-center gap-3" aria-label="Shifter home">
            <ShifterLogo size={34} />
            <div className="hidden leading-tight sm:block">
              <p className="text-sm font-bold text-slate-950">Shifter</p>
              <p className="text-xs text-slate-500">{c.nav.tagline}</p>
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
              aria-label={c.nav.menu}
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
                <a href={supportHref} className="inline-flex justify-center rounded-lg bg-slate-950 px-5 py-3 text-sm font-bold text-white hover:bg-slate-800">
                  {c.contact.email}
                </a>
                <a href={walkthroughHref} className="inline-flex justify-center rounded-lg border border-slate-300 px-5 py-3 text-sm font-bold text-slate-800 hover:bg-slate-50">
                  {c.contact.demo}
                </a>
              </div>
            </div>
            <div className="border-t border-slate-200 bg-slate-100 p-6 lg:border-l lg:border-t-0">
              <MiniOpsPanel alerts={c.preview.alerts} c={c.preview} />
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
      <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-2xl shadow-slate-300/60">
        <div className="flex h-[520px] max-h-[70vh] min-h-[440px] bg-slate-50">
          <aside className="hidden w-52 shrink-0 border-e border-slate-200 bg-slate-950 p-3 text-slate-300 lg:block">
            <div className="mb-4 flex items-center gap-2 rounded-lg px-2 py-2">
              <ShifterLogo size={28} />
              <div className="min-w-0">
                <p className="text-sm font-black text-white">Shifter</p>
                <p className="truncate text-[10px] font-semibold text-sky-200">{c.productSubtitle}</p>
              </div>
            </div>
            <div className="space-y-1 text-xs font-semibold">
              {[c.scheduleView, c.pickView, c.operationsView].map((item, index) => (
                <div
                  key={item}
                  className={`flex items-center justify-between rounded-lg px-3 py-2.5 ${
                    index === 2
                      ? "bg-sky-500/15 text-sky-100"
                      : "text-slate-400"
                  }`}
                >
                  <span>{item}</span>
                  {index === 2 ? <span className="h-1.5 w-1.5 rounded-full bg-sky-400" /> : null}
                </div>
              ))}
            </div>
          </aside>

          <div className="min-w-0 flex-1">
            <header className="flex h-14 items-center justify-between border-b border-slate-200 bg-white px-4">
              <div>
                <p className="text-xs font-semibold text-slate-500">{c.workspace}</p>
                <p className="text-sm font-black text-slate-950">{c.desktopTitle}</p>
              </div>
              <button className="rounded-lg bg-slate-950 px-3 py-2 text-xs font-bold text-white">
                {c.publishLabel}
              </button>
            </header>

            <main className="space-y-4 p-4">
              <section className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
                <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-start">
                  <div>
                    <h3 className="text-base font-semibold text-slate-950">{c.desktopTitle}</h3>
                    <p className="mt-1 max-w-md text-sm leading-6 text-slate-500">{c.desktopSubtitle}</p>
                  </div>
                  <span className="inline-flex w-fit rounded-full border border-emerald-200 bg-emerald-50 px-2.5 py-1 text-xs font-medium text-emerald-700">
                    {c.openWindowLabel}
                  </span>
                </div>

                <div className="mt-4 grid grid-cols-3 gap-2">
                  {[
                    ["18", c.slotsLabel],
                    ["5", c.requestsLabel],
                    ["92%", c.coverageLabel],
                  ].map(([value, label], index) => (
                    <div key={label} className="rounded-lg border border-slate-200 bg-slate-50 p-3">
                      <p className={`text-lg font-black ${
                        index === 2 ? "text-emerald-700" : "text-slate-950"
                      }`}>{value}</p>
                      <p className="mt-1 text-xs font-medium text-slate-500">{label}</p>
                    </div>
                  ))}
                </div>
              </section>

              <section className="grid gap-3 md:grid-cols-[0.58fr_0.42fr]">
                <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
                  <div className="mb-3 flex items-center justify-between gap-3">
                    <h3 className="text-sm font-semibold text-slate-950">{c.adminReviewLabel}</h3>
                    <span className="rounded-full border border-amber-200 bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-700">
                      {c.requestsLabel}
                    </span>
                  </div>
                  <div className="space-y-2">
                    {c.alerts.map((alert, index) => (
                      <div key={alert} className="rounded-lg border border-slate-200 bg-white px-3 py-2">
                        <div className="flex items-center gap-2">
                          <span className={`h-2 w-2 rounded-full ${index === 0 ? "bg-amber-500" : "bg-sky-500"}`} />
                          <p className="text-xs font-medium text-slate-700">{alert}</p>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>

                <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
                  <h3 className="text-sm font-semibold text-slate-950">{c.opsSnapshot}</h3>
                  <div className="mt-3 grid grid-cols-3 gap-2">
                    {c.metrics.map((metric) => (
                      <div key={metric.label} className="rounded-lg bg-slate-50 p-2 text-center">
                        <p className="text-base font-black text-slate-950">{metric.value}</p>
                        <p className="mt-1 text-[10px] font-medium text-slate-500">{metric.label}</p>
                      </div>
                    ))}
                  </div>
                  <div className="mt-3 rounded-lg border border-sky-200 bg-sky-50 px-3 py-2 text-xs font-semibold text-sky-700">
                    {c.live}
                  </div>
                </div>
              </section>

              <section className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
                <div className="mb-3 flex items-center justify-between gap-3">
                  <h3 className="text-sm font-semibold text-slate-950">{c.pickView}</h3>
                  <span className="text-xs font-medium text-slate-500">{c.mobileSubtitle}</span>
                </div>
                <div className="space-y-2">
                  {c.shifts.map((shift, index) => (
                    <div key={`${shift.time}-${shift.name}`} className="rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5">
                      <div className="flex items-center justify-between gap-3">
                        <div className="min-w-0">
                          <p className="truncate text-sm font-semibold text-slate-950">{shift.name}</p>
                          <p className="mt-0.5 text-xs text-slate-500">{shift.assignee}</p>
                        </div>
                        <div className="shrink-0 text-end">
                          <p className="text-sm font-black text-slate-950">{shift.time}</p>
                          <span className={`mt-1 inline-flex rounded-full border px-2 py-0.5 text-[11px] font-medium ${
                            index === 0
                              ? "border-emerald-200 bg-emerald-50 text-emerald-700"
                              : index === 1
                                ? "border-amber-200 bg-amber-50 text-amber-700"
                                : "border-sky-200 bg-sky-50 text-sky-700"
                          }`}>
                            {shift.status}
                          </span>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </section>
            </main>
          </div>
        </div>
      </div>

      <div className="mx-auto -mt-12 w-56 overflow-hidden rounded-[28px] border-4 border-slate-950 bg-slate-50 shadow-xl sm:absolute sm:-bottom-8 sm:end-6 sm:mt-0">
        <div className="flex items-center gap-2 border-b border-slate-200 bg-white px-3 py-2">
          <div className="h-8 w-8 rounded-lg bg-slate-100" />
          <div className="min-w-0 flex-1 text-center">
            <p className="truncate text-xs font-semibold text-slate-950">{c.workspace}</p>
          </div>
          <div className="h-8 w-8 rounded-lg bg-slate-100" />
        </div>
        <div className="p-3">
          <p className="text-sm font-semibold text-slate-950">{c.mobileHeader}</p>
          <p className="mt-1 text-xs leading-5 text-slate-500">{c.mobileSubtitle}</p>
        <div className="mt-3 flex gap-1 overflow-hidden rounded-xl bg-slate-100 p-1">
          {c.mobileTabs.map((tab, index) => (
            <span
              key={tab}
              className={`rounded-lg px-2 py-1.5 text-[10px] font-semibold ${index === 0 ? "bg-white text-slate-950 shadow-sm" : "text-slate-500"}`}
            >
              {tab}
            </span>
          ))}
        </div>
          <div className="mt-3 space-y-2">
          {c.shifts.slice(0, 2).map(shift => (
              <div key={shift.name} className="rounded-xl border border-slate-200 bg-white p-3">
              <div className="flex items-center justify-between gap-2">
                <p className="text-xs font-bold text-slate-950">{shift.time}</p>
                <span className="rounded bg-emerald-50 px-1.5 py-0.5 text-[10px] font-bold text-emerald-700">{shift.status}</span>
              </div>
              <p className="mt-1 text-xs text-slate-600">{shift.name}</p>
            </div>
          ))}
          </div>
        </div>
      </div>
    </div>
  );
}

function MiniOpsPanel({ alerts, c }: { alerts: string[]; c: (typeof LANDING_CONTENT)[LandingLang]["preview"] }) {
  return (
    <div className="rounded-lg bg-slate-950 p-5 text-white">
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm font-black">{c.opsSnapshot}</p>
        <span className="rounded-lg bg-emerald-400 px-2.5 py-1 text-xs font-black text-emerald-950">{c.live}</span>
      </div>
      <div className="mt-5 grid grid-cols-3 gap-2">
        {c.metrics.map((metric) => (
          <div key={metric.label} className="rounded-lg bg-white/10 p-3">
            <p className="text-xl font-black">{metric.value}</p>
            <p className="text-xs text-slate-400">{metric.label}</p>
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
