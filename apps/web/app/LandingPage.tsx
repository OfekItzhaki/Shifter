"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import ShifterLogo from "@/components/shell/ShifterLogo";

/**
 * Marketing landing page for Shifter.
 * If the user is already logged in (has access_token), redirect to /spaces.
 */
export default function LandingPage() {
  const router = useRouter();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

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
      <nav className="sticky top-0 z-50 backdrop-blur-md bg-slate-900/80 border-b border-slate-700/50">
        <div className="flex items-center justify-between px-6 py-3 max-w-6xl mx-auto">
          <div className="flex items-center gap-3">
            <ShifterLogo size={28} />
            <span className="text-lg font-bold">Shifter</span>
          </div>

          {/* Desktop nav links */}
          <div className="hidden sm:flex items-center gap-6 text-sm text-slate-300">
            <a href="#features" className="hover:text-white transition-colors">יתרונות</a>
            <a href="#how-it-works" className="hover:text-white transition-colors">איך זה עובד</a>
            <a href="#about" className="hover:text-white transition-colors">אודות</a>
            <a href="#faq" className="hover:text-white transition-colors">שאלות נפוצות</a>
          </div>

          {/* Auth buttons */}
          <div className="flex items-center gap-2">
            <Link
              href="/login"
              className="text-sm text-slate-300 hover:text-white transition-colors px-4 py-2 border border-slate-600 hover:border-slate-400 rounded-xl hidden sm:inline-block"
            >
              כניסה למערכת
            </Link>
            <Link
              href="/register"
              className="text-sm font-medium bg-blue-500 hover:bg-blue-600 text-white px-5 py-2.5 rounded-xl transition-colors"
            >
              הרשמה חינם
            </Link>
            {/* Mobile menu button */}
            <button
              onClick={() => setMobileMenuOpen(o => !o)}
              className="sm:hidden p-2 text-slate-300 hover:text-white"
              aria-label="תפריט"
            >
              <svg width="20" height="20" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M4 6h16M4 12h16M4 18h16" />
              </svg>
            </button>
          </div>
        </div>

        {/* Mobile menu */}
        {mobileMenuOpen && (
          <div className="sm:hidden border-t border-slate-700/50 px-6 py-4 space-y-3 bg-slate-900/95">
            <a href="#features" onClick={() => setMobileMenuOpen(false)} className="block text-sm text-slate-300 hover:text-white">יתרונות</a>
            <a href="#how-it-works" onClick={() => setMobileMenuOpen(false)} className="block text-sm text-slate-300 hover:text-white">איך זה עובד</a>
            <a href="#about" onClick={() => setMobileMenuOpen(false)} className="block text-sm text-slate-300 hover:text-white">אודות</a>
            <a href="#faq" onClick={() => setMobileMenuOpen(false)} className="block text-sm text-slate-300 hover:text-white">שאלות נפוצות</a>
            <Link href="/login" onClick={() => setMobileMenuOpen(false)} className="block text-sm text-blue-400 font-medium">כניסה למערכת</Link>
          </div>
        )}
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
          <Link
            href="/login"
            className="w-full sm:w-auto text-center text-slate-300 hover:text-white border border-slate-600 hover:border-slate-400 px-8 py-4 rounded-2xl text-base transition-all"
          >
            יש לי חשבון — כניסה
          </Link>
        </div>
      </section>

      {/* Features Section */}
      <section id="features" className="px-6 py-20 max-w-5xl mx-auto scroll-mt-20">
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
      <section id="how-it-works" className="px-6 py-20 bg-slate-800/50 scroll-mt-20">
        <div className="max-w-4xl mx-auto">
          <h2 className="text-2xl sm:text-3xl font-bold text-center mb-12">איך זה עובד?</h2>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-8">
            <StepCard number={1} title="הגדר" description="הוסף אנשים, משימות ואילוצים לקבוצה שלך" />
            <StepCard number={2} title="הפעל" description="לחץ על 'צור סידור' — האלגוריתם עושה את השאר" />
            <StepCard number={3} title="פרסם" description="בדוק את הטיוטה ופרסם — כולם מקבלים התראה בנייד" />
          </div>
        </div>
      </section>

      {/* About Section */}
      <section id="about" className="px-6 py-20 max-w-4xl mx-auto scroll-mt-20">
        <h2 className="text-2xl sm:text-3xl font-bold text-center mb-4">אודות</h2>
        <p className="text-slate-400 text-center mb-10 max-w-xl mx-auto">
          הסיפור מאחורי Shifter
        </p>

        <div className="bg-slate-800/60 border border-slate-700/50 rounded-2xl p-8 sm:p-10 space-y-5 text-slate-300 leading-relaxed">
          <p>
            <strong className="text-white">Shifter</strong> נולד מתוך הצורך האמיתי של מפקדים ומנהלי צוותים
            שמבלים שעות על סידור משמרות באקסל — ובסוף תמיד מישהו לא מרוצה.
          </p>
          <p>
            המערכת משתמשת באלגוריתם אופטימיזציה (CP-SAT) שמחלק משמרות בצורה הוגנת ומאוזנת,
            תוך שמירה על כל האילוצים: מנוחה מינימלית, כישורים נדרשים, העדפות אישיות ועוד.
          </p>
          <p>
            הפלטפורמה בנויה עם דגש על חוויית משתמש בנייד — כי חיילים צריכים לראות את הסידור
            שלהם בשטח, גם בלי אינטרנט. כל שינוי בסידור מגיע ישירות לנייד.
          </p>
          <p>
            Shifter מתאים לצוותים צבאיים, שמירה, מפעלים, בתי חולים — כל מקום שצריך
            לחלק משמרות בצורה חכמה והוגנת.
          </p>
        </div>

        {/* Stats */}
        <div className="grid grid-cols-3 gap-6 mt-12">
          <div className="text-center">
            <p className="text-3xl sm:text-4xl font-bold text-blue-400">90%</p>
            <p className="text-sm text-slate-400 mt-1">חיסכון בזמן</p>
          </div>
          <div className="text-center">
            <p className="text-3xl sm:text-4xl font-bold text-emerald-400">0</p>
            <p className="text-sm text-slate-400 mt-1">אקסלים</p>
          </div>
          <div className="text-center">
            <p className="text-3xl sm:text-4xl font-bold text-amber-400">24/7</p>
            <p className="text-sm text-slate-400 mt-1">גישה מהנייד</p>
          </div>
        </div>
      </section>

      {/* FAQ Section */}
      <section id="faq" className="px-6 py-20 bg-slate-800/50 scroll-mt-20">
        <div className="max-w-3xl mx-auto">
          <h2 className="text-2xl sm:text-3xl font-bold text-center mb-4">שאלות נפוצות</h2>
          <p className="text-slate-400 text-center mb-10">תשובות לשאלות הכי נפוצות</p>

          <div className="space-y-4">
            <FaqItem
              question="האם זה באמת חינם?"
              answer="כן. התוכנית הבסיסית חינמית לחלוטין ומתאימה לרוב הצוותים. בעתיד יהיו תוכניות מתקדמות עם יכולות נוספות."
            />
            <FaqItem
              question="כמה אנשים אפשר להוסיף לקבוצה?"
              answer="בתוכנית החינמית אפשר להוסיף עד 30 אנשים לקבוצה. מספיק לרוב הפלוגות והצוותים."
            />
            <FaqItem
              question="האם המידע שלי מאובטח?"
              answer="בהחלט. כל המידע מוצפן, סיסמאות מאובטחות ב-BCrypt, והתקשורת מוצפנת ב-HTTPS. יש בידוד מלא בין קבוצות."
            />
            <FaqItem
              question="האם אפשר לראות את הסידור בלי אינטרנט?"
              answer="כן! האפליקציה שומרת את הסידור האחרון במכשיר. גם בלי קליטה תוכל לראות את המשמרות שלך."
            />
            <FaqItem
              question="מה קורה אם מישהו לא יכול להגיע למשמרת?"
              answer="המנהל יכול לסמן 'לא יכול להגיע' ולהפעיל את האלגוריתם מחדש — הוא ימצא מחליף אוטומטית."
            />
            <FaqItem
              question="האם אפשר לייבא אנשים מאקסל?"
              answer="כן. אפשר לייבא רשימת אנשים ומשימות מקובץ CSV או Excel בלחיצה אחת."
            />
          </div>
        </div>
      </section>

      {/* CTA */}
      <section className="px-6 py-20 text-center">
        <div className="max-w-2xl mx-auto bg-gradient-to-br from-blue-600 to-blue-700 rounded-3xl p-10 sm:p-14 shadow-2xl shadow-blue-500/20">
          <h2 className="text-2xl sm:text-3xl font-bold mb-4">מוכן להתחיל?</h2>
          <p className="text-blue-100 mb-8">הרשמה חינם תוך 30 שניות. בלי כרטיס אשראי.</p>
          <div className="flex flex-col sm:flex-row items-center justify-center gap-4">
            <Link
              href="/register"
              className="w-full sm:w-auto inline-block bg-white text-blue-700 font-bold px-8 py-4 rounded-2xl text-base hover:bg-blue-50 transition-colors shadow-lg"
            >
              צור חשבון חינם
            </Link>
            <Link
              href="/login"
              className="w-full sm:w-auto inline-block text-blue-100 hover:text-white border border-blue-400/50 hover:border-white px-8 py-4 rounded-2xl text-base transition-colors"
            >
              כניסה לחשבון קיים
            </Link>
          </div>
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
            <a href="#about" className="hover:text-white transition-colors">אודות</a>
            <a href="#faq" className="hover:text-white transition-colors">שאלות נפוצות</a>
            <Link href="/terms" className="hover:text-white transition-colors">תנאי שימוש</Link>
            <Link href="/privacy" className="hover:text-white transition-colors">פרטיות</Link>
            <Link href="/login" className="hover:text-white transition-colors">כניסה</Link>
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
        <svg
          className="w-4 h-4 text-slate-400 group-open:rotate-180 transition-transform flex-shrink-0 mr-3"
          fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}
        >
          <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
        </svg>
      </summary>
      <div className="px-6 pb-4 text-sm text-slate-400 leading-relaxed">
        {answer}
      </div>
    </details>
  );
}
