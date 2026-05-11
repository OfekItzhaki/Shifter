"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import ShifterLogo from "@/components/shell/ShifterLogo";
import { getMySpaces } from "@/lib/api/spaces";

export default function LandingPage() {
  const router = useRouter();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [checking, setChecking] = useState(true);

  useEffect(() => {
    const token = localStorage.getItem("access_token");
    if (!token) { setChecking(false); return; }
    getMySpaces().then(() => router.replace("/spaces")).catch(() => {
      localStorage.removeItem("access_token");
      localStorage.removeItem("refresh_token");
      setChecking(false);
    });
  }, [router]);

  if (checking) return null;

  return (
    <div className="min-h-screen bg-gradient-to-b from-slate-900 via-slate-800 to-slate-900 text-white">
      <nav className="sticky top-0 z-50 backdrop-blur-md bg-slate-900/80 border-b border-slate-700/50">
        <div className="flex items-center justify-between px-6 py-3 max-w-6xl mx-auto">
          <div className="flex items-center gap-3">
            <ShifterLogo size={28} />
            <span className="text-lg font-bold">Shifter</span>
          </div>
          <div className="hidden sm:flex items-center gap-6 text-sm text-slate-300">
            <a href="#features" className="hover:text-white transition-colors">Features</a>
            <a href="#how-it-works" className="hover:text-white transition-colors">How it Works</a>
            <a href="#about" className="hover:text-white transition-colors">About</a>
            <a href="#faq" className="hover:text-white transition-colors">FAQ</a>
          </div>
          <div className="flex items-center gap-2">
            <Link href="/login" className="text-sm text-slate-300 hover:text-white transition-colors px-4 py-2 border border-slate-600 hover:border-slate-400 rounded-xl hidden sm:inline-block">
              Sign In
            </Link>
            <Link href="/register" className="text-sm font-medium bg-blue-500 hover:bg-blue-600 text-white px-5 py-2.5 rounded-xl transition-colors">
              Get Started Free
            </Link>
            <button onClick={() => setMobileMenuOpen(o => !o)} className="sm:hidden p-2 text-slate-300" aria-label="Menu">
              <svg width="20" height="20" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M4 6h16M4 12h16M4 18h16" /></svg>
            </button>
          </div>
        </div>
        {mobileMenuOpen && (
          <div className="sm:hidden border-t border-slate-700/50 px-6 py-4 space-y-3 bg-slate-900/95">
            <a href="#features" onClick={() => setMobileMenuOpen(false)} className="block text-sm text-slate-300">Features</a>
            <a href="#how-it-works" onClick={() => setMobileMenuOpen(false)} className="block text-sm text-slate-300">How it Works</a>
            <a href="#about" onClick={() => setMobileMenuOpen(false)} className="block text-sm text-slate-300">About</a>
            <a href="#faq" onClick={() => setMobileMenuOpen(false)} className="block text-sm text-slate-300">FAQ</a>
            <Link href="/login" onClick={() => setMobileMenuOpen(false)} className="block text-sm text-blue-400 font-medium">Sign In</Link>
          </div>
        )}
      </nav>

      <section className="px-6 pt-16 pb-20 sm:pt-24 sm:pb-28 max-w-4xl mx-auto text-center">
        <div className="inline-flex items-center gap-2 bg-blue-500/10 border border-blue-500/20 rounded-full px-4 py-1.5 mb-6">
          <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
          <span className="text-xs text-blue-300 font-medium">Free to use &bull; No credit card required</span>
        </div>
        <h1 className="text-4xl sm:text-5xl lg:text-6xl font-extrabold leading-tight mb-6">
          Smart Shift<br /><span className="text-blue-400">Scheduling</span>
        </h1>
        <p className="text-lg sm:text-xl text-slate-300 max-w-2xl mx-auto mb-10 leading-relaxed">
          Shifter generates fair, balanced shift schedules at the click of a button.<br className="hidden sm:block" />
          No spreadsheets. No headaches. No arguments.
        </p>
        <div className="flex flex-col sm:flex-row items-center justify-center gap-4">
          <Link href="/register" className="w-full sm:w-auto text-center bg-blue-500 hover:bg-blue-600 text-white font-semibold px-8 py-4 rounded-2xl text-base transition-all shadow-lg shadow-blue-500/25">
            Get Started &mdash; It&apos;s Free
          </Link>
          <Link href="/login" className="w-full sm:w-auto text-center text-slate-300 hover:text-white border border-slate-600 hover:border-slate-400 px-8 py-4 rounded-2xl text-base transition-all">
            I have an account &mdash; Sign In
          </Link>
        </div>
      </section>

      <section id="features" className="px-6 py-20 max-w-5xl mx-auto scroll-mt-20">
        <h2 className="text-2xl sm:text-3xl font-bold text-center mb-4">Why Shifter?</h2>
        <p className="text-slate-400 text-center mb-12 max-w-xl mx-auto">Everything you need to manage shift schedules — in one place</p>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          <FeatureCard icon="⚡" title="Auto Scheduling" description="Smart algorithm distributes shifts fairly while respecting all constraints" />
          <FeatureCard icon="📱" title="Mobile First" description="Everyone sees their shifts on mobile — even offline with no internet" />
          <FeatureCard icon="⚖️" title="Fair Distribution" description="The system balances workload across all people and prevents overloading" />
          <FeatureCard icon="🔒" title="Flexible Constraints" description="Set minimum rest, personal restrictions, required qualifications and more" />
          <FeatureCard icon="📊" title="Statistics" description="See who did how many shifts, who's overloaded, who's available — in real time" />
          <FeatureCard icon="🔔" title="Notifications" description="Every schedule change is pushed directly to team members' phones" />
        </div>
      </section>

      <section id="how-it-works" className="px-6 py-20 bg-slate-800/50 scroll-mt-20">
        <div className="max-w-4xl mx-auto">
          <h2 className="text-2xl sm:text-3xl font-bold text-center mb-12">How it Works</h2>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-8">
            <StepCard number={1} title="Define" description="Add people, tasks, and constraints to your group" />
            <StepCard number={2} title="Generate" description="Click 'Create Schedule' — the algorithm does the rest" />
            <StepCard number={3} title="Publish" description="Review the draft and publish — everyone gets notified" />
          </div>
        </div>
      </section>

      <section id="about" className="px-6 py-20 max-w-4xl mx-auto scroll-mt-20">
        <h2 className="text-2xl sm:text-3xl font-bold text-center mb-4">About</h2>
        <p className="text-slate-400 text-center mb-10 max-w-xl mx-auto">The story behind Shifter</p>
        <div className="bg-slate-800/60 border border-slate-700/50 rounded-2xl p-8 sm:p-10 space-y-5 text-slate-300 leading-relaxed">
          <p><strong className="text-white">Shifter</strong> was born from the real need of team leaders who spend hours building shift schedules in Excel — and someone always ends up unhappy.</p>
          <p>The system uses a constraint-optimization algorithm (CP-SAT) that distributes shifts fairly and balanced, while respecting all constraints: minimum rest, required qualifications, personal preferences, and more.</p>
          <p>The platform is built mobile-first — because team members need to check their schedule in the field, even without internet. Every change is pushed directly to their phone.</p>
          <p>Shifter works for military teams, security, factories, hospitals, restaurants — anywhere that needs smart, fair shift distribution.</p>
        </div>
        <div className="grid grid-cols-3 gap-6 mt-12">
          <div className="text-center"><p className="text-3xl sm:text-4xl font-bold text-blue-400">90%</p><p className="text-sm text-slate-400 mt-1">Time Saved</p></div>
          <div className="text-center"><p className="text-3xl sm:text-4xl font-bold text-emerald-400">0</p><p className="text-sm text-slate-400 mt-1">Spreadsheets</p></div>
          <div className="text-center"><p className="text-3xl sm:text-4xl font-bold text-amber-400">24/7</p><p className="text-sm text-slate-400 mt-1">Mobile Access</p></div>
        </div>
      </section>

      <section id="faq" className="px-6 py-20 bg-slate-800/50 scroll-mt-20">
        <div className="max-w-3xl mx-auto">
          <h2 className="text-2xl sm:text-3xl font-bold text-center mb-4">FAQ</h2>
          <p className="text-slate-400 text-center mb-10">Answers to common questions</p>
          <div className="space-y-4">
            <FaqItem question="Is it really free?" answer="Yes. The basic plan is completely free and works for most teams. Advanced plans with extra features will be available in the future." />
            <FaqItem question="How many people can I add?" answer="The free plan supports up to 30 people per group. Enough for most platoons and teams." />
            <FaqItem question="Is my data secure?" answer="Absolutely. All data is encrypted, passwords are hashed with BCrypt, and communication uses HTTPS. Full isolation between groups." />
            <FaqItem question="Can I view the schedule offline?" answer="Yes! The app caches your latest schedule on your device. Even without signal, you can see your shifts." />
            <FaqItem question="What if someone can't make their shift?" answer="The admin can mark 'can't make it' and re-run the algorithm — it automatically finds a replacement." />
            <FaqItem question="Can I import people from Excel?" answer="Yes. You can import people and tasks from CSV or Excel files with one click." />
          </div>
        </div>
      </section>

      <section className="px-6 py-20 text-center">
        <div className="max-w-2xl mx-auto bg-gradient-to-br from-blue-600 to-blue-700 rounded-3xl p-10 sm:p-14 shadow-2xl shadow-blue-500/20">
          <h2 className="text-2xl sm:text-3xl font-bold mb-4">Ready to get started?</h2>
          <p className="text-blue-100 mb-8">Sign up free in 30 seconds. No credit card needed.</p>
          <div className="flex flex-col sm:flex-row items-center justify-center gap-4">
            <Link href="/register" className="w-full sm:w-auto inline-block bg-white text-blue-700 font-bold px-8 py-4 rounded-2xl text-base hover:bg-blue-50 transition-colors shadow-lg">
              Create Free Account
            </Link>
            <Link href="/login" className="w-full sm:w-auto inline-block text-blue-100 hover:text-white border border-blue-400/50 hover:border-white px-8 py-4 rounded-2xl text-base transition-colors">
              Sign In to Existing Account
            </Link>
          </div>
        </div>
      </section>

      <footer className="px-6 py-8 border-t border-slate-700/50">
        <div className="max-w-5xl mx-auto flex flex-col sm:flex-row items-center justify-between gap-4">
          <div className="flex items-center gap-2">
            <ShifterLogo size={20} />
            <span className="text-sm text-slate-400">Shifter &copy; {new Date().getFullYear()}</span>
          </div>
          <div className="flex items-center gap-6 text-sm text-slate-400">
            <a href="#about" className="hover:text-white transition-colors">About</a>
            <a href="#faq" className="hover:text-white transition-colors">FAQ</a>
            <Link href="/terms" className="hover:text-white transition-colors">Terms</Link>
            <Link href="/privacy" className="hover:text-white transition-colors">Privacy</Link>
            <Link href="/login" className="hover:text-white transition-colors">Sign In</Link>
          </div>
        </div>
      </footer>
    </div>
  );
}

function FeatureCard({ icon, title, description }: { icon: string; title: string; description: string }) {
  return (
    <div className="bg-slate-800/60 border border-slate-700/50 rounded-2xl p-6 hover:border-slate-600 transition-colors">
      <span className="text-2xl mb-3 block">{icon}</span>
      <h3 className="text-base font-semibold mb-2">{title}</h3>
      <p className="text-sm text-slate-400 leading-relaxed">{description}</p>
    </div>
  );
}

function StepCard({ number, title, description }: { number: number; title: string; description: string }) {
  return (
    <div className="text-center">
      <div className="w-12 h-12 rounded-full bg-blue-500/20 border border-blue-500/30 flex items-center justify-center mx-auto mb-4">
        <span className="text-blue-400 font-bold text-lg">{number}</span>
      </div>
      <h3 className="text-base font-semibold mb-2">{title}</h3>
      <p className="text-sm text-slate-400">{description}</p>
    </div>
  );
}

function FaqItem({ question, answer }: { question: string; answer: string }) {
  return (
    <details className="group bg-slate-800/60 border border-slate-700/50 rounded-xl overflow-hidden">
      <summary className="flex items-center justify-between px-6 py-4 cursor-pointer list-none">
        <span className="text-sm font-medium text-white">{question}</span>
        <svg className="w-4 h-4 text-slate-400 group-open:rotate-180 transition-transform flex-shrink-0 ml-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
        </svg>
      </summary>
      <div className="px-6 pb-4 text-sm text-slate-400 leading-relaxed">{answer}</div>
    </details>
  );
}
