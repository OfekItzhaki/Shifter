"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import ShifterLogo from "@/components/shell/ShifterLogo";

/**
 * Marketing landing page for Shifter.
 * If the user is already logged in (has access_token), redirect to /spaces.
 */
export default function LandingPage() {
  const router = useRouter();

  // Redirect authenticated users
  useEffect(() => {
    const token = localStorage.getItem("access_token");
    if (token) {
      router.replace("/spaces");
    }
  }, [router]);

  return (
    <div className="min-h-screen bg-gradient-to-b from-slate-900 via-slate-800 to-slate-900 text-white">
      {/* Navigation */}
      <nav className="flex items-center justify-between px-6 py-4 max-w-6xl mx-auto">
        <div className="flex items-center gap-3">
          <ShifterLogo size={32} />
          <span className="text-lg font-bold">Shifter</span>
        </div>
        <div className="flex items-center gap-3">
          <Link
            href="/login"
            className="text-sm text-slate-300 hover:text-white transition-colors px-4 py-2"
          >
            התחברות
          </Link>
          <Link
            href="/register"
            className="text-sm font-medium bg-blue-500 hover:bg-blue-600 text-white px-5 py-2.5 rounded-xl transition-colors"
          >
            הרשמה חינם
          </Link>
        </div>
      </nav>

      {/* Hero Section */}
      <section className="px-6 pt-16 pb-20 sm:pt-24 sm:pb-28 max-w-4xl mx-auto text-center">
        <div className="inline-flex items-center gap-2 bg-blue-500/10 border border-blue-500/20 rounded-full px-4 py-1.5 mb-6">
          <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
          <span className="text-xs text-blue-300 font-medium">חינם לשימוש • ללא כרטיס אשראי</span>
        </div>

        <h1 className="text-4xl sm:text-5xl lg:text-6xl font-extrabold leading-tight mb-6">
          סידור משמרות
          <br />
          <span className="text-blue-400">חכם ואוטומטי</span>
        </h1>

        <p className="text-lg sm:text-xl text-slate-300 max-w-2xl mx-auto mb-10 leading-relaxed">
          Shifter מייצר סידור עבודה הוגן ומאוזן בלחיצת כפתור.
          <br className="hidden sm:block" />
          בלי אקסלים, בלי כאב ראש, בלי ויכוחים.
        </p>

        <div className="flex flex-col sm:flex-row items-center justify-center gap-4">
          <Link
            href="/register"
            className="w-full sm:w-auto text-center bg-blue-500 hover:bg-blue-600 text-white font-semibold px-8 py-4 rounded-2xl text-base transition-all shadow-lg shadow-blue-500/25 hover:shadow-blue-500/40"
          >
            התחל עכשיו — חינם
          </Link>
          <a
            href="#features"
            className="w-full sm:w-auto text-center text-slate-300 hover:text-white border border-slate-600 hover:border-slate-400 px-8 py-4 rounded-2xl text-base transition-all"
          >
            איך זה עובד?
          </a>
        </div>
      </section>

      {/* Features Section */}
      <section id="features" className="px-6 py-20 max-w-5xl mx-auto">
        <h2 className="text-2xl sm:text-3xl font-bold text-center mb-4">למה Shifter?</h2>
        <p className="text-slate-400 text-center mb-12 max-w-xl mx-auto">
          כל מה שצריך כדי לנהל סידור עבודה — ממקום אחד
        </p>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
          <FeatureCard
            icon="⚡"
            title="סידור אוטומטי"
            description="אלגוריתם חכם שמחלק משמרות בצורה הוגנת תוך שמירה על כל האילוצים"
          />
          <FeatureCard
            icon="📱"
            title="עובד בנייד"
            description="כל חייל רואה את המשמרות שלו בנייד — גם בלי אינטרנט"
          />
          <FeatureCard
            icon="⚖️"
            title="חלוקה הוגנת"
            description="המערכת מאזנת עומס בין כל האנשים ומונעת העמסה על אחד"
          />
          <FeatureCard
            icon="🔒"
            title="אילוצים גמישים"
            description="הגדר מנוחה מינימלית, הגבלות אישיות, כישורים נדרשים ועוד"
          />
          <FeatureCard
            icon="📊"
            title="סטטיסטיקות"
            description="ראה מי עשה כמה משמרות, מי עמוס ומי פנוי — בזמן אמת"
          />
          <FeatureCard
            icon="🔔"
            title="התראות"
            description="כל שינוי בסידור מגיע ישירות לנייד של החיילים"
          />
        </div>
      </section>

      {/* How it works */}
      <section className="px-6 py-20 bg-slate-800/50">
        <div className="max-w-4xl mx-auto">
          <h2 className="text-2xl sm:text-3xl font-bold text-center mb-12">איך זה עובד?</h2>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-8">
            <StepCard number={1} title="הגדר" description="הוסף אנשים, משימות ואילוצים" />
            <StepCard number={2} title="הפעל" description="לחץ על 'צור סידור' והמערכת עושה את השאר" />
            <StepCard number={3} title="פרסם" description="בדוק את הטיוטה ופרסם — כולם מקבלים התראה" />
          </div>
        </div>
      </section>

      {/* Social proof */}
      <section className="px-6 py-20 max-w-4xl mx-auto text-center">
        <div className="grid grid-cols-3 gap-8 mb-12">
          <div>
            <p className="text-3xl sm:text-4xl font-bold text-blue-400">90%</p>
            <p className="text-sm text-slate-400 mt-1">חיסכון בזמן</p>
          </div>
          <div>
            <p className="text-3xl sm:text-4xl font-bold text-emerald-400">0</p>
            <p className="text-sm text-slate-400 mt-1">אקסלים</p>
          </div>
          <div>
            <p className="text-3xl sm:text-4xl font-bold text-amber-400">24/7</p>
            <p className="text-sm text-slate-400 mt-1">גישה מהנייד</p>
          </div>
        </div>
      </section>

      {/* CTA */}
      <section className="px-6 py-20 text-center">
        <div className="max-w-2xl mx-auto bg-gradient-to-br from-blue-600 to-blue-700 rounded-3xl p-10 sm:p-14 shadow-2xl shadow-blue-500/20">
          <h2 className="text-2xl sm:text-3xl font-bold mb-4">מוכן להתחיל?</h2>
          <p className="text-blue-100 mb-8">הרשמה חינם תוך 30 שניות. בלי כרטיס אשראי.</p>
          <Link
            href="/register"
            className="inline-block bg-white text-blue-700 font-bold px-8 py-4 rounded-2xl text-base hover:bg-blue-50 transition-colors shadow-lg"
          >
            צור חשבון חינם
          </Link>
        </div>
      </section>

      {/* Footer */}
      <footer className="px-6 py-8 border-t border-slate-700/50">
        <div className="max-w-5xl mx-auto flex flex-col sm:flex-row items-center justify-between gap-4">
          <div className="flex items-center gap-2">
            <ShifterLogo size={20} />
            <span className="text-sm text-slate-400">Shifter © {new Date().getFullYear()}</span>
          </div>
          <div className="flex items-center gap-6 text-sm text-slate-400">
            <Link href="/login" className="hover:text-white transition-colors">התחברות</Link>
            <Link href="/register" className="hover:text-white transition-colors">הרשמה</Link>
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
