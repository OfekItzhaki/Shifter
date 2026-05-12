"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import ShifterLogo from "@/components/shell/ShifterLogo";
import { LANDING_CONTENT, type LandingLang } from "./landingContent";

export default function LandingPage() {
  const router = useRouter();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [checking, setChecking] = useState(true);
  const [lang, setLang] = useState<LandingLang>("en");

  useEffect(() => {
    // Detect language from cookie or browser
    const cookieLang = document.cookie.match(/locale=(\w+)/)?.[1];
    if (cookieLang === "he" || cookieLang === "ru") setLang(cookieLang);
  }, []);

  useEffect(() => {
    const token = localStorage.getItem("access_token");
    if (!token) { setChecking(false); return; }
    const controller = new AbortController();
    fetch(`${process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000"}/auth/me`, {
      headers: { Authorization: `Bearer ${token}` },
      signal: controller.signal,
    }).then(res => {
      if (res.ok) router.replace("/schedule/today");
      else { localStorage.removeItem("access_token"); localStorage.removeItem("refresh_token"); document.cookie = "access_token=; path=/; max-age=0"; setChecking(false); }
    }).catch(() => { setChecking(false); });
    return () => controller.abort();
  }, [router]);

  const c = LANDING_CONTENT[lang];

  function switchLang(l: LandingLang) {
    setLang(l);
    document.cookie = `locale=${l}; path=/; max-age=31536000; SameSite=Strict`;
  }

  return (
    <div className="min-h-screen bg-gradient-to-b from-slate-900 via-slate-800 to-slate-900 text-white" dir={c.dir}>
      {/* Nav */}
      <nav className="sticky top-0 z-50 backdrop-blur-md bg-slate-900/80 border-b border-slate-700/50">
        <div className="flex items-center justify-between px-6 py-3 max-w-6xl mx-auto">
          <div className="flex items-center gap-3">
            <ShifterLogo size={28} />
            <span className="text-lg font-bold">Shifter</span>
          </div>
          <div className="hidden sm:flex items-center gap-6 text-sm text-slate-300">
            <a href="#features" className="hover:text-white transition-colors">{c.nav.features}</a>
            <a href="#how-it-works" className="hover:text-white transition-colors">{c.nav.howItWorks}</a>
            <a href="#about" className="hover:text-white transition-colors">{c.nav.about}</a>
            <a href="#faq" className="hover:text-white transition-colors">{c.nav.faq}</a>
          </div>
          <div className="flex items-center gap-2">
            {/* Language switcher */}
            <div className="hidden sm:flex items-center gap-1 mr-2">
              {(["en", "he", "ru"] as LandingLang[]).map(l => (
                <button key={l} onClick={() => switchLang(l)} className={`text-xs px-2 py-1 rounded ${lang === l ? "bg-blue-500/30 text-blue-300 font-bold" : "text-slate-400 hover:text-white"}`}>
                  {l === "en" ? "EN" : l === "he" ? "עב" : "RU"}
                </button>
              ))}
            </div>
            <Link href="/login" className="text-sm text-slate-300 hover:text-white transition-colors px-4 py-2 border border-slate-600 hover:border-slate-400 rounded-xl hidden sm:inline-block">
              {c.nav.signIn}
            </Link>
            <Link href="/register" className="text-sm font-medium bg-blue-500 hover:bg-blue-600 text-white px-5 py-2.5 rounded-xl transition-colors">
              {c.nav.getStarted}
            </Link>
            <button onClick={() => setMobileMenuOpen(o => !o)} className="sm:hidden p-2 text-slate-300" aria-label="Menu">
              <svg width="20" height="20" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M4 6h16M4 12h16M4 18h16" /></svg>
            </button>
          </div>
        </div>
        {mobileMenuOpen && (
          <div className="sm:hidden border-t border-slate-700/50 px-6 py-4 space-y-3 bg-slate-900/95">
            <a href="#features" onClick={() => setMobileMenuOpen(false)} className="block text-sm text-slate-300">{c.nav.features}</a>
            <a href="#how-it-works" onClick={() => setMobileMenuOpen(false)} className="block text-sm text-slate-300">{c.nav.howItWorks}</a>
            <a href="#about" onClick={() => setMobileMenuOpen(false)} className="block text-sm text-slate-300">{c.nav.about}</a>
            <a href="#faq" onClick={() => setMobileMenuOpen(false)} className="block text-sm text-slate-300">{c.nav.faq}</a>
            <Link href="/login" onClick={() => setMobileMenuOpen(false)} className="block text-sm text-blue-400 font-medium">{c.nav.signIn}</Link>
            <div className="flex gap-2 pt-2">
              {(["en", "he", "ru"] as LandingLang[]).map(l => (
                <button key={l} onClick={() => { switchLang(l); setMobileMenuOpen(false); }} className={`text-xs px-3 py-1.5 rounded ${lang === l ? "bg-blue-500/30 text-blue-300 font-bold" : "text-slate-400"}`}>
                  {l === "en" ? "EN" : l === "he" ? "עב" : "RU"}
                </button>
              ))}
            </div>
          </div>
        )}
      </nav>

      {/* Hero */}
      <section className="px-6 pt-16 pb-20 sm:pt-24 sm:pb-28 max-w-4xl mx-auto text-center">
        <h1 className="text-4xl sm:text-5xl lg:text-6xl font-extrabold leading-tight mb-6">
          {c.hero.title1}<br /><span className="text-blue-400">{c.hero.title2}</span>
        </h1>
        <p className="text-lg sm:text-xl text-slate-300 max-w-2xl mx-auto mb-10 leading-relaxed">{c.hero.subtitle}</p>
        <div className="flex flex-col sm:flex-row items-center justify-center gap-4">
          <Link href="/register" className="w-full sm:w-auto text-center bg-blue-500 hover:bg-blue-600 text-white font-semibold px-8 py-4 rounded-2xl text-base transition-all shadow-lg shadow-blue-500/25">{c.hero.cta}</Link>
          <Link href="/login" className="w-full sm:w-auto text-center text-slate-300 hover:text-white border border-slate-600 hover:border-slate-400 px-8 py-4 rounded-2xl text-base transition-all">{c.hero.signIn}</Link>
        </div>
      </section>

      {/* Features */}
      <section id="features" className="px-6 py-20 max-w-5xl mx-auto scroll-mt-20">
        <h2 className="text-2xl sm:text-3xl font-bold text-center mb-4">{c.features.title}</h2>
        <p className="text-slate-400 text-center mb-12 max-w-xl mx-auto">{c.features.subtitle}</p>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          {c.features.items.map(f => (
            <div key={f.title} className="bg-slate-800/60 border border-slate-700/50 rounded-2xl p-6 hover:border-slate-600 transition-colors">
              <span className="text-2xl mb-3 block">{f.icon}</span>
              <h3 className="text-base font-semibold mb-2">{f.title}</h3>
              <p className="text-sm text-slate-400 leading-relaxed">{f.desc}</p>
            </div>
          ))}
        </div>
      </section>

      {/* How it works */}
      <section id="how-it-works" className="px-6 py-20 bg-slate-800/50 scroll-mt-20">
        <div className="max-w-4xl mx-auto">
          <h2 className="text-2xl sm:text-3xl font-bold text-center mb-12">{c.howItWorks.title}</h2>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-8">
            {c.howItWorks.steps.map((s, i) => (
              <div key={i} className="text-center">
                <div className="w-12 h-12 rounded-full bg-blue-500/20 border border-blue-500/30 flex items-center justify-center mx-auto mb-4">
                  <span className="text-blue-400 font-bold text-lg">{i + 1}</span>
                </div>
                <h3 className="text-base font-semibold mb-2">{s.title}</h3>
                <p className="text-sm text-slate-400">{s.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* About */}
      <section id="about" className="px-6 py-20 max-w-4xl mx-auto scroll-mt-20">
        <h2 className="text-2xl sm:text-3xl font-bold text-center mb-4">{c.about.title}</h2>
        <p className="text-slate-400 text-center mb-10 max-w-xl mx-auto">{c.about.subtitle}</p>
        <div className="bg-slate-800/60 border border-slate-700/50 rounded-2xl p-8 sm:p-10 space-y-5 text-slate-300 leading-relaxed">
          {c.about.paragraphs.map((p, i) => <p key={i}>{i === 0 ? <><strong className="text-white">Shifter</strong> {p.replace("Shifter ", "")}</> : p}</p>)}
        </div>
        <div className="grid grid-cols-3 gap-6 mt-12">
          {c.about.stats.map(s => (
            <div key={s.label} className="text-center">
              <p className="text-3xl sm:text-4xl font-bold text-blue-400">{s.value}</p>
              <p className="text-sm text-slate-400 mt-1">{s.label}</p>
            </div>
          ))}
        </div>
      </section>

      {/* FAQ */}
      <section id="faq" className="px-6 py-20 bg-slate-800/50 scroll-mt-20">
        <div className="max-w-3xl mx-auto">
          <h2 className="text-2xl sm:text-3xl font-bold text-center mb-4">{c.faq.title}</h2>
          <p className="text-slate-400 text-center mb-10">{c.faq.subtitle}</p>
          <div className="space-y-4">
            {c.faq.items.map(item => (
              <details key={item.q} className="group bg-slate-800/60 border border-slate-700/50 rounded-xl overflow-hidden">
                <summary className="flex items-center justify-between px-6 py-4 cursor-pointer list-none">
                  <span className="text-sm font-medium text-white">{item.q}</span>
                  <svg className={`w-4 h-4 text-slate-400 group-open:rotate-180 transition-transform flex-shrink-0 ${c.dir === "rtl" ? "mr-3" : "ml-3"}`} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" /></svg>
                </summary>
                <div className="px-6 pb-4 text-sm text-slate-400 leading-relaxed">{item.a}</div>
              </details>
            ))}
          </div>
        </div>
      </section>

      {/* CTA */}
      <section className="px-6 py-20 text-center">
        <div className="max-w-2xl mx-auto bg-gradient-to-br from-blue-600 to-blue-700 rounded-3xl p-10 sm:p-14 shadow-2xl shadow-blue-500/20">
          <h2 className="text-2xl sm:text-3xl font-bold mb-4">{c.cta.title}</h2>
          <p className="text-blue-100 mb-8">{c.cta.subtitle}</p>
          <div className="flex flex-col sm:flex-row items-center justify-center gap-4">
            <Link href="/register" className="w-full sm:w-auto inline-block bg-white text-blue-700 font-bold px-8 py-4 rounded-2xl text-base hover:bg-blue-50 transition-colors shadow-lg">{c.cta.primary}</Link>
            <Link href="/login" className="w-full sm:w-auto inline-block text-blue-100 hover:text-white border border-blue-400/50 hover:border-white px-8 py-4 rounded-2xl text-base transition-colors">{c.cta.secondary}</Link>
          </div>
        </div>
      </section>

      {/* Footer */}
      <footer className="px-6 py-8 border-t border-slate-700/50">
        <div className="max-w-5xl mx-auto flex flex-col sm:flex-row items-center justify-between gap-4">
          <div className="flex items-center gap-2">
            <ShifterLogo size={20} />
            <span className="text-sm text-slate-400">Shifter &copy; {new Date().getFullYear()}</span>
          </div>
          <div className="flex items-center gap-6 text-sm text-slate-400">
            <a href="#about" className="hover:text-white transition-colors">{c.footer.about}</a>
            <a href="#faq" className="hover:text-white transition-colors">{c.footer.faq}</a>
            <Link href="/terms" className="hover:text-white transition-colors">{c.footer.terms}</Link>
            <Link href="/privacy" className="hover:text-white transition-colors">{c.footer.privacy}</Link>
            <Link href="/login" className="hover:text-white transition-colors">{c.footer.signIn}</Link>
          </div>
        </div>
      </footer>
    </div>
  );
}
