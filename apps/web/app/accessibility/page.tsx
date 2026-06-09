import Link from "next/link";
import { getLocale } from "next-intl/server";
import ShifterLogo from "@/components/shell/ShifterLogo";
import { legalDir } from "@/lib/legal/legalContent";

const CONTENT = {
  en: {
    eyebrow: "Accessibility",
    title: "Accessibility Statement",
    updated: "Last updated",
    calloutTitle: "Need accessibility help?",
    calloutBody: "Email support@shifter.app with the page, device, browser, and the barrier you encountered.",
    sections: [
      {
        title: "Our approach",
        body: "Shifter is built for teams who need to check and manage schedules quickly from desktop and mobile. Accessibility is part of that product work, including keyboard use, readable contrast, localization, and assistive technology support.",
      },
      {
        title: "What we support",
        bullets: [
          "Responsive layouts for mobile, tablet, and desktop.",
          "Semantic controls where possible for forms, navigation, buttons, and dialogs.",
          "Visible focus states and keyboard-operable workflows.",
          "Light, dark, and automatic appearance modes.",
          "English, Hebrew, and Russian localization, including right-to-left layout for Hebrew.",
        ],
      },
      {
        title: "Known work in progress",
        body: "Some advanced workflows, charts, imported file previews, and older pages may still need additional accessibility review. We prioritize issues that block account access, scheduling, billing, privacy, or safety-critical workflows.",
      },
      {
        title: "Feedback",
        body: "If something prevents you from using Shifter, contact us and we will review it. Helpful details include the URL, browser, operating system, assistive technology, and a short description of the problem.",
      },
    ],
  },
  he: {
    eyebrow: "נגישות",
    title: "הצהרת נגישות",
    updated: "עודכן לאחרונה",
    calloutTitle: "צריך עזרה בנושא נגישות?",
    calloutBody: "שלחו מייל ל-support@shifter.app עם העמוד, המכשיר, הדפדפן והחסם שנתקלתם בו.",
    sections: [
      {
        title: "הגישה שלנו",
        body: "Shifter נבנה עבור צוותים שצריכים לבדוק ולנהל סידורים במהירות מהמחשב ומהנייד. נגישות היא חלק מעבודת המוצר, כולל שימוש במקלדת, ניגודיות קריאה, שפות ותמיכה בטכנולוגיות מסייעות.",
      },
      {
        title: "מה נתמך",
        bullets: [
          "פריסה רספונסיבית לנייד, טאבלט ומחשב.",
          "שימוש ברכיבים סמנטיים ככל האפשר בטפסים, ניווט, כפתורים ודיאלוגים.",
          "מצבי פוקוס גלויים ותהליכים שניתן להפעיל במקלדת.",
          "מצב בהיר, מצב כהה ומצב אוטומטי.",
          "עברית, אנגלית ורוסית, כולל תמיכה בימין לשמאל בעברית.",
        ],
      },
      {
        title: "עבודה בתהליך",
        body: "חלק מהתהליכים המתקדמים, הגרפים, תצוגות יבוא קבצים ועמודים ישנים עדיין צריכים בדיקת נגישות נוספת. אנחנו נותנים עדיפות לבעיות שחוסמות גישה לחשבון, סידור משמרות, חיוב, פרטיות או תהליכים קריטיים.",
      },
      {
        title: "משוב",
        body: "אם משהו מונע מכם להשתמש ב-Shifter, צרו קשר ונבדוק את זה. פרטים שעוזרים: כתובת העמוד, דפדפן, מערכת הפעלה, טכנולוגיה מסייעת ותיאור קצר של הבעיה.",
      },
    ],
  },
  ru: {
    eyebrow: "Accessibility",
    title: "Accessibility Statement",
    updated: "Last updated",
    calloutTitle: "Need accessibility help?",
    calloutBody: "Email support@shifter.app with the page, device, browser, and the barrier you encountered.",
    sections: [
      {
        title: "Our approach",
        body: "Shifter is built for teams who need to check and manage schedules quickly from desktop and mobile. Accessibility is part of that product work, including keyboard use, readable contrast, localization, and assistive technology support.",
      },
      {
        title: "What we support",
        bullets: [
          "Responsive layouts for mobile, tablet, and desktop.",
          "Semantic controls where possible for forms, navigation, buttons, and dialogs.",
          "Visible focus states and keyboard-operable workflows.",
          "Light, dark, and automatic appearance modes.",
          "English, Hebrew, and Russian localization, including right-to-left layout for Hebrew.",
        ],
      },
      {
        title: "Known work in progress",
        body: "Some advanced workflows, charts, imported file previews, and older pages may still need additional accessibility review.",
      },
      {
        title: "Feedback",
        body: "If something prevents you from using Shifter, contact us and we will review it.",
      },
    ],
  },
};

export default async function AccessibilityPage() {
  const locale = await getLocale();
  const lang = locale === "he" || locale === "ru" ? locale : "en";
  const content = CONTENT[lang];
  const dir = legalDir(locale);

  return (
    <div className="min-h-screen bg-white dark:bg-slate-900">
      <header className="border-b border-slate-100 px-6 py-4 dark:border-slate-800">
        <div className="mx-auto flex max-w-3xl items-center justify-between gap-4">
          <Link href="/" className="flex items-center gap-2 text-slate-900 transition-colors hover:text-sky-600 dark:text-white">
            <ShifterLogo size={24} />
            <span className="text-sm font-bold">Shifter</span>
          </Link>
          <Link href="/privacy" className="text-sm text-sky-600 hover:underline">
            Privacy
          </Link>
        </div>
      </header>

      <main className="mx-auto max-w-3xl px-6 py-12" dir={dir}>
        <p className="mb-3 text-xs font-semibold uppercase tracking-[0.18em] text-sky-600">{content.eyebrow}</p>
        <h1 className="mb-2 text-3xl font-bold text-slate-900 dark:text-white">{content.title}</h1>
        <p className="mb-8 text-sm text-slate-500 dark:text-slate-400">{content.updated}: 2026-06-09</p>

        <div className="mb-8 rounded-lg border border-sky-200 bg-sky-50 p-4 text-sm leading-relaxed text-sky-900 dark:border-sky-800 dark:bg-sky-950/30 dark:text-sky-200">
          <p className="font-semibold">{content.calloutTitle}</p>
          <p className="mt-1">{content.calloutBody}</p>
        </div>

        <div className="space-y-7 text-sm leading-relaxed text-slate-700 dark:text-slate-300">
          {content.sections.map(section => (
            <section key={section.title}>
              <h2 className="mb-2 text-base font-semibold text-slate-900 dark:text-white">{section.title}</h2>
              {"body" in section && <p>{section.body}</p>}
              {section.bullets && (
                <ul className={`${dir === "rtl" ? "pr-5" : "pl-5"} list-disc space-y-1`}>
                  {section.bullets.map(bullet => (
                    <li key={bullet}>{bullet}</li>
                  ))}
                </ul>
              )}
            </section>
          ))}
        </div>
      </main>
    </div>
  );
}
