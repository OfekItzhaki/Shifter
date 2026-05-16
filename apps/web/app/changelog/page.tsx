"use client";

import AppShell from "@/components/shell/AppShell";
import { useTranslations } from "next-intl";

type ChangeType = "new" | "fix" | "improved";

interface Change {
  type: ChangeType;
  text: string;
}

interface ChangelogEntry {
  version: string;
  date: string;
  changes: Change[];
}

const CHANGELOG: ChangelogEntry[] = [
  {
    version: "1.9.0",
    date: "2026-06-01",
    changes: [
      { type: "new", text: "מערכת תבניות — צבא, מסעדה, בית חולים, אבטחה, מותאם אישית" },
      { type: "new", text: "תצוגת תכנון חופשות — טבלה עם סטטוס לכל חבר" },
      { type: "new", text: "צפייה בסידורים ישנים — היסטוריית שיבוצים מלאה" },
      { type: "new", text: "הרשאת צפייה בהיסטוריה — הגדרה לכל קבוצה" },
      { type: "new", text: "מינימום אנשים בבסיס — הגדרה ישירה במקום מכסת יוצאים" },
      { type: "new", text: "מנוחה מינימלית בין משמרות — הגדרת קבוצה אוטומטית" },
      { type: "new", text: "אילוץ גנרי — מקסימום משימה בתקופה (לא רק מטבח)" },
      { type: "improved", text: "הסרת קוד מת — המערכת נקייה וגנרית" },
      { type: "improved", text: "תוויות דינמיות — בסיס סגור / לינה במקום לפי תבנית" },
      { type: "fix", text: "תיקון כפילויות חופשות בית" },
      { type: "fix", text: "ניווט אוטומטי ליום הראשון עם שיבוצים" },
    ],
  },
  {
    version: "1.8.0",
    date: "2026-06-01",
    changes: [
      { type: "new", text: "מערכת חופשות בית חדשה — מצב אוטומטי, ידני, וחירום" },
      { type: "new", text: "מחוון חכם — מרכז על היחס האופטימלי בסיס/בית" },
      { type: "new", text: "מצב ידני — הגדרת ימים מדויקת עם משוב ישיר" },
      { type: "new", text: "הקפאת חירום — עצירת כל החופשות מיידית" },
      { type: "new", text: "בדיקת היתכנות בזמן אמת לכל שינוי הגדרות" },
      { type: "improved", text: "תמיכה מלאה ב-RTL/LTR למחוון ולכל הרכיבים" },
      { type: "fix", text: "תיקון בדיקות — הודעות שגיאה בעברית" },
    ],
  },
  {
    version: "1.7.0",
    date: "2026-06-01",
    changes: [
      { type: "new", text: "מעקב מצטבר — זיכרון בין הרצות סולבר" },
      { type: "new", text: "צפייה בהיסטוריית שיבוצים לשבועות קודמים" },
      { type: "new", text: "סטטיסטיקות מצטברות עם בחירת טווח זמן" },
      { type: "new", text: "תקופות מנוי — חלוקת נתונים לפי מחזורי חיוב" },
      { type: "improved", text: "זכאות חופשת בית מבוססת שעות מצטברות (לא רק אופק נוכחי)" },
      { type: "improved", text: "הוגנות שיבוצים מתחשבת בהיסטוריה מצטברת" },
      { type: "fix", text: "תיקון TypeScript — groupId חסר ב-ScheduleTab" },
    ],
  },
  {
    version: "1.6.0",
    date: "2026-05-26",
    changes: [
      { type: "new", text: "תזמון חופשות בית לקבוצות בסיס סגור" },
      { type: "new", text: "התחברות ביומטרית (טביעת אצבע / זיהוי פנים)" },
      { type: "new", text: "גרפים וסטטיסטיקות מתקדמות בתוך הקבוצה" },
      { type: "new", text: "תפקידים צבעוניים — כל תפקיד מקבל צבע ייחודי" },
      { type: "new", text: "תבניות הכשרות — הגדרה מהירה של הכשרות נפוצות" },
      { type: "new", text: "סיבות אי-זמינות — מחלה, חופשה, קורס ועוד" },
      { type: "new", text: "פישוט רמות עומס — קשה / רגיל / קל" },
      { type: "improved", text: "קישור הזמנה — הצטרפות אוטומטית למשתמשים מחוברים" },
      { type: "fix", text: "הודעות שגיאה מוצגות בעברית בכל המערכת" },
    ],
  },
  {
    version: "1.5.0",
    date: "2026-05-12",
    changes: [
      { type: "new", text: "Onboarding wizard for new users" },
      { type: "new", text: "24h/AM-PM time format toggle in profile" },
      { type: "new", text: "Push notifications for schedule updates" },
      { type: "new", text: "Email verification flow" },
      { type: "improved", text: "Smooth page transitions" },
      { type: "improved", text: "Deep health checks for monitoring" },
      { type: "fix", text: "Docker build compatibility with Alpine Linux" },
    ],
  },
  {
    version: "1.4.0",
    date: "2026-04-28",
    changes: [
      { type: "new", text: "Manual import fallback (CSV/Excel)" },
      { type: "new", text: "Custom error pages (404, 403, 500)" },
      { type: "new", text: "Group ownership transfer" },
      { type: "improved", text: "Schedule solver performance" },
      { type: "fix", text: "Schedule publish notification timing" },
    ],
  },
  {
    version: "1.3.0",
    date: "2026-04-15",
    changes: [
      { type: "new", text: "Dark mode support" },
      { type: "new", text: "Group join codes" },
      { type: "new", text: "Statistics dashboard with burden tracking" },
      { type: "improved", text: "Mobile responsive layout" },
      { type: "fix", text: "RTL layout issues in Hebrew" },
    ],
  },
  {
    version: "1.2.0",
    date: "2026-04-01",
    changes: [
      { type: "new", text: "AI-powered schedule import" },
      { type: "new", text: "Group alerts and messaging" },
      { type: "new", text: "Member qualifications system" },
      { type: "improved", text: "Constraint management UI" },
    ],
  },
  {
    version: "1.1.0",
    date: "2026-03-15",
    changes: [
      { type: "new", text: "Multi-language support (English, Hebrew, Russian)" },
      { type: "new", text: "Profile image upload" },
      { type: "new", text: "Schedule export (CSV/PDF)" },
      { type: "fix", text: "Token refresh race condition" },
    ],
  },
  {
    version: "1.0.0",
    date: "2026-03-01",
    changes: [
      { type: "new", text: "Initial release" },
      { type: "new", text: "Automatic shift scheduling with OR-Tools solver" },
      { type: "new", text: "Group and member management" },
      { type: "new", text: "Constraint-based scheduling" },
      { type: "new", text: "Schedule versioning and rollback" },
    ],
  },
];

const BADGE_STYLES: Record<ChangeType, string> = {
  new: "bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300",
  fix: "bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300",
  improved: "bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-300",
};

export default function ChangelogPage() {
  const t = useTranslations();

  return (
    <AppShell>
      <div className="max-w-3xl mx-auto">
        <h1 className="text-2xl font-bold text-slate-900 dark:text-white mb-6">
          {t("changelog.title")}
        </h1>

        <div className="flex flex-col gap-6">
          {CHANGELOG.map((entry) => (
            <div
              key={entry.version}
              className="bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 p-6"
            >
              <div className="flex items-baseline gap-3 mb-1">
                <h2 className="text-lg font-semibold text-slate-900 dark:text-white">
                  v{entry.version}
                </h2>
              </div>
              <p className="text-sm text-slate-500 dark:text-slate-400 mb-4">
                {entry.date}
              </p>

              <ul className="flex flex-col gap-2">
                {entry.changes.map((change, idx) => (
                  <li key={idx} className="flex items-start gap-2">
                    <span
                      className={`inline-block px-2 py-0.5 rounded text-xs font-medium shrink-0 mt-0.5 ${BADGE_STYLES[change.type]}`}
                    >
                      {t(`changelog.${change.type}`)}
                    </span>
                    <span className="text-sm text-slate-700 dark:text-slate-300">
                      {change.text}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      </div>
    </AppShell>
  );
}
