import Link from "next/link";
import ShifterLogo from "@/components/shell/ShifterLogo";

const CONTACT_EMAIL = process.env.NEXT_PUBLIC_LEGAL_EMAIL ?? "support@ofeklabs.com";

export default function PrivacyPage() {
  return (
    <div className="min-h-screen bg-white dark:bg-slate-900">
      {/* Header */}
      <header className="border-b border-slate-100 dark:border-slate-800 px-6 py-4">
        <div className="max-w-3xl mx-auto flex items-center justify-between">
          <Link href="/" className="flex items-center gap-2 text-slate-900 dark:text-white hover:text-sky-600 transition-colors">
            <ShifterLogo size={24} />
            <span className="font-bold text-sm">Shifter</span>
          </Link>
          <Link href="/terms" className="text-sm text-sky-600 hover:underline">
            תנאי שימוש / Terms
          </Link>
        </div>
      </header>

      {/* Hebrew Section */}
      <main className="px-6 py-12 max-w-3xl mx-auto">
        <div dir="rtl">
          <h1 className="text-2xl font-bold text-slate-900 dark:text-white mb-2">מדיניות פרטיות</h1>
          <p className="text-sm text-slate-400 mb-8">עדכון אחרון: מאי 2026</p>

          <div className="space-y-6 text-slate-700 dark:text-slate-300 leading-relaxed text-sm">
            <Section title="1. מידע שאנו אוספים">
              <p>אנו אוספים את המידע הבא:</p>
              <ul className="list-disc pr-5 space-y-1">
                <li><strong>מידע הרשמה:</strong> שם, כתובת אימייל, מספר טלפון (אופציונלי)</li>
                <li><strong>נתוני סידור:</strong> משמרות, אילוצים, זמינות, חברויות בקבוצות</li>
                <li><strong>מידע טכני:</strong> כתובת IP, סוג דפדפן, זמני גישה (לצורכי אבטחה בלבד)</li>
              </ul>
            </Section>

            <Section title="2. כיצד אנו משתמשים במידע">
              <ul className="list-disc pr-5 space-y-1">
                <li>אספקת השירות — יצירת סידורי עבודה ומשמרות</li>
                <li>שליחת התראות על שינויים בסידור</li>
                <li>אימות זהות ואבטחת החשבון</li>
                <li>שיפור השירות ותיקון באגים</li>
              </ul>
              <p className="mt-2">
                <strong>אנו לא:</strong> מוכרים מידע לצדדים שלישיים, משתמשים במידע לפרסום,
                או חולקים מידע אישי עם גורמים חיצוניים ללא הסכמתך.
              </p>
            </Section>

            <Section title="3. אחסון מידע">
              <ul className="list-disc pr-5 space-y-1">
                <li>המידע מאוחסן בשרת VPS מאובטח עם מסד נתונים PostgreSQL</li>
                <li>סיסמאות מוצפנות באמצעות BCrypt ולא נשמרות בטקסט גלוי</li>
                <li>תקשורת מוצפנת באמצעות TLS/HTTPS</li>
                <li>בידוד מלא בין מרחבי עבודה (multi-tenancy)</li>
                <li>גישה למסד הנתונים מוגבלת לצוות הפיתוח בלבד</li>
              </ul>
            </Section>

            <Section title="4. צדדים שלישיים">
              <p>אנו משתמשים בשירותים הבאים:</p>
              <ul className="list-disc pr-5 space-y-1">
                <li><strong>LemonSqueezy:</strong> עיבוד תשלומים וניהול מנויים</li>
                <li><strong>SendGrid:</strong> שליחת אימיילים (אימות, התראות, איפוס סיסמה)</li>
              </ul>
              <p className="mt-2">
                שירותים אלה מקבלים רק את המידע המינימלי הנדרש לתפקודם.
                אנו לא משתמשים בכלי אנליטיקה או מעקב של צדדים שלישיים.
              </p>
            </Section>

            <Section title="5. שמירת מידע">
              <p>מידע אישי נשמר כל עוד החשבון שלך פעיל. לאחר מחיקת חשבון:</p>
              <ul className="list-disc pr-5 space-y-1">
                <li>מידע אישי נמחק תוך 30 יום</li>
                <li>לוגים אנונימיים נשמרים עד 90 יום לצורכי אבטחה</li>
                <li>גיבויים מוצפנים נמחקים תוך 60 יום</li>
              </ul>
            </Section>

            <Section title="6. זכויותיך">
              <p>יש לך את הזכות:</p>
              <ul className="list-disc pr-5 space-y-1">
                <li><strong>לצפות</strong> במידע האישי שלך (דרך עמוד הפרופיל)</li>
                <li><strong>לייצא</strong> את הנתונים שלך (סידורים בפורמט CSV/PDF)</li>
                <li><strong>למחוק</strong> את החשבון שלך ואת כל המידע הקשור</li>
              </ul>
              <p className="mt-2">כל הזכויות הללו כבר מיושמות בממשק האפליקציה.</p>
            </Section>

            <Section title="7. עוגיות ואחסון מקומי">
              <p>אנו משתמשים ב-localStorage ובעוגיות לצורך:</p>
              <ul className="list-disc pr-5 space-y-1">
                <li>טוקן גישה (לאימות)</li>
                <li>העדפות שפה</li>
                <li>מטמון סידור עבודה (לצפייה אופליין)</li>
              </ul>
              <p className="mt-2">
                <strong>אנו לא משתמשים בעוגיות מעקב.</strong> אין אנליטיקה של צדדים שלישיים.
              </p>
            </Section>

            <Section title="8. ילדים">
              <p>
                השירות אינו מיועד לילדים מתחת לגיל 16. אנו לא אוספים ביודעין מידע מילדים.
                אם נודע לנו שנאסף מידע של קטין, נמחק אותו מיידית.
              </p>
            </Section>

            <Section title="9. עמידה בחוק הגנת הפרטיות הישראלי">
              <p>
                אנו פועלים בהתאם לחוק הגנת הפרטיות, התשמ&quot;א-1981 ותקנותיו.
                מאגר המידע רשום כנדרש. זכויותיך לעיון, תיקון ומחיקה מובטחות.
              </p>
            </Section>

            <Section title="10. עמידה ב-GDPR (למשתמשים מהאיחוד האירופי)">
              <p>למשתמשים מהאיחוד האירופי, אנו מבטיחים:</p>
              <ul className="list-disc pr-5 space-y-1">
                <li>הבסיס החוקי לעיבוד: הסכמה וביצוע חוזה</li>
                <li>זכות לניידות מידע (ייצוא נתונים)</li>
                <li>זכות למחיקה (&quot;הזכות להישכח&quot;)</li>
                <li>זכות להגבלת עיבוד</li>
                <li>זכות להתנגד לעיבוד</li>
              </ul>
            </Section>

            <Section title="11. שינויים במדיניות">
              <p>
                אנו עשויים לעדכן מדיניות זו מעת לעת. שינויים מהותיים יפורסמו באפליקציה
                ובאימייל. המשך השימוש לאחר עדכון מהווה הסכמה למדיניות החדשה.
              </p>
            </Section>

            <Section title="12. יצירת קשר">
              <p>
                לשאלות בנוגע לפרטיות, ניתן לפנות אלינו:{" "}
                <a href={`mailto:${CONTACT_EMAIL}`} className="text-sky-600 hover:underline">{CONTACT_EMAIL}</a>
              </p>
            </Section>
          </div>
        </div>

        {/* Divider */}
        <hr className="my-12 border-slate-200 dark:border-slate-700" />

        {/* English Section */}
        <div dir="ltr">
          <h1 className="text-2xl font-bold text-slate-900 dark:text-white mb-2">Privacy Policy</h1>
          <p className="text-sm text-slate-400 mb-8">Last updated: May 2026</p>

          <div className="space-y-6 text-slate-700 dark:text-slate-300 leading-relaxed text-sm">
            <Section title="1. What Data We Collect">
              <p>We collect the following information:</p>
              <ul className="list-disc pl-5 space-y-1">
                <li><strong>Registration data:</strong> Name, email address, phone number (optional)</li>
                <li><strong>Scheduling data:</strong> Shifts, constraints, availability, group memberships</li>
                <li><strong>Technical data:</strong> IP address, browser type, access times (for security only)</li>
              </ul>
            </Section>

            <Section title="2. How We Use It">
              <ul className="list-disc pl-5 space-y-1">
                <li>Provide the service — generate schedules and manage shifts</li>
                <li>Send notifications about schedule changes</li>
                <li>Authenticate identity and secure accounts</li>
                <li>Improve the service and fix bugs</li>
              </ul>
              <p className="mt-2">
                <strong>We do not:</strong> sell data to third parties, use data for advertising,
                or share personal information with external parties without your consent.
              </p>
            </Section>

            <Section title="3. Data Storage">
              <ul className="list-disc pl-5 space-y-1">
                <li>Data is stored on a secured VPS with a PostgreSQL database</li>
                <li>Passwords are hashed with BCrypt and never stored in plain text</li>
                <li>All communication is encrypted via TLS/HTTPS</li>
                <li>Full isolation between workspaces (multi-tenancy)</li>
                <li>Database access is restricted to the development team only</li>
              </ul>
            </Section>

            <Section title="4. Third Parties">
              <p>We use the following services:</p>
              <ul className="list-disc pl-5 space-y-1">
                <li><strong>LemonSqueezy:</strong> Payment processing and subscription management</li>
                <li><strong>SendGrid:</strong> Email delivery (verification, notifications, password reset)</li>
              </ul>
              <p className="mt-2">
                These services receive only the minimum data required for their function.
                We do not use third-party analytics or tracking tools.
              </p>
            </Section>

            <Section title="5. Data Retention">
              <p>Personal data is retained as long as your account is active. After account deletion:</p>
              <ul className="list-disc pl-5 space-y-1">
                <li>Personal data is deleted within 30 days</li>
                <li>Anonymous logs are retained up to 90 days for security</li>
                <li>Encrypted backups are deleted within 60 days</li>
              </ul>
            </Section>

            <Section title="6. User Rights">
              <p>You have the right to:</p>
              <ul className="list-disc pl-5 space-y-1">
                <li><strong>Access</strong> your personal data (via the profile page)</li>
                <li><strong>Export</strong> your data (schedules in CSV/PDF format)</li>
                <li><strong>Delete</strong> your account and all associated data</li>
              </ul>
              <p className="mt-2">All of these rights are already implemented in the application interface.</p>
            </Section>

            <Section title="7. Cookies and Local Storage">
              <p>We use localStorage and cookies for:</p>
              <ul className="list-disc pl-5 space-y-1">
                <li>Access tokens (authentication)</li>
                <li>Language preferences</li>
                <li>Schedule cache (offline viewing)</li>
              </ul>
              <p className="mt-2">
                <strong>We do not use tracking cookies.</strong> No third-party analytics.
              </p>
            </Section>

            <Section title="8. Children&apos;s Privacy">
              <p>
                The Service is not intended for children under 16. We do not knowingly collect data from children.
                If we learn that data from a minor has been collected, we will delete it immediately.
              </p>
            </Section>

            <Section title="9. Israeli Privacy Protection Law Compliance">
              <p>
                We operate in accordance with the Israeli Privacy Protection Law, 5741-1981 and its regulations.
                The database is registered as required. Your rights to access, correction, and deletion are guaranteed.
              </p>
            </Section>

            <Section title="10. GDPR Compliance (for EU Users)">
              <p>For users in the European Union, we ensure:</p>
              <ul className="list-disc pl-5 space-y-1">
                <li>Legal basis for processing: consent and contract performance</li>
                <li>Right to data portability (data export)</li>
                <li>Right to erasure (&quot;right to be forgotten&quot;)</li>
                <li>Right to restriction of processing</li>
                <li>Right to object to processing</li>
              </ul>
            </Section>

            <Section title="11. Changes to Policy">
              <p>
                We may update this policy from time to time. Material changes will be published in the application
                and via email. Continued use after an update constitutes acceptance of the new policy.
              </p>
            </Section>

            <Section title="12. Contact">
              <p>
                For privacy-related questions, contact us at:{" "}
                <a href={`mailto:${CONTACT_EMAIL}`} className="text-sky-600 hover:underline">{CONTACT_EMAIL}</a>
              </p>
            </Section>
          </div>
        </div>

        {/* Footer */}
        <footer className="mt-12 pt-8 border-t border-slate-200 dark:border-slate-700 text-center text-xs text-slate-400 space-y-2">
          <div className="flex items-center justify-center gap-4">
            <Link href="/terms" className="text-sky-600 hover:underline">תנאי שימוש</Link>
            <span>·</span>
            <Link href="/privacy" className="text-sky-600 hover:underline">מדיניות פרטיות</Link>
            <span>·</span>
            <Link href="/" className="text-sky-600 hover:underline">חזרה לדף הבית</Link>
          </div>
          <p>© {new Date().getFullYear()} Ofek Labs. All rights reserved.</p>
        </footer>
      </main>
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div>
      <h2 className="text-base font-semibold text-slate-900 dark:text-white mb-2">{title}</h2>
      {children}
    </div>
  );
}
