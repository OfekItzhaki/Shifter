import Link from "next/link";
import ShifterLogo from "@/components/shell/ShifterLogo";

const CONTACT_EMAIL = process.env.NEXT_PUBLIC_LEGAL_EMAIL ?? "support@ofeklabs.com";

export default function TermsPage() {
  return (
    <div className="min-h-screen bg-white dark:bg-slate-900">
      {/* Header */}
      <header className="border-b border-slate-100 dark:border-slate-800 px-6 py-4">
        <div className="max-w-3xl mx-auto flex items-center justify-between">
          <Link href="/" className="flex items-center gap-2 text-slate-900 dark:text-white hover:text-blue-600 transition-colors">
            <ShifterLogo size={24} />
            <span className="font-bold text-sm">Shifter</span>
          </Link>
          <Link href="/privacy" className="text-sm text-blue-600 hover:underline">
            מדיניות פרטיות / Privacy
          </Link>
        </div>
      </header>

      {/* Hebrew Section */}
      <main className="px-6 py-12 max-w-3xl mx-auto">
        <div dir="rtl">
          <h1 className="text-2xl font-bold text-slate-900 dark:text-white mb-2">תנאי שימוש</h1>
          <p className="text-sm text-slate-400 mb-8">עדכון אחרון: מאי 2026</p>

          <div className="space-y-6 text-slate-700 dark:text-slate-300 leading-relaxed text-sm">
            <Section title="1. הסכמה לתנאים">
              <p>
                בשימוש באפליקציית Shifter של Ofek Labs (להלן: &quot;השירות&quot;), אתה מסכים לתנאי שימוש אלה.
                אם אינך מסכים לתנאים, אנא הפסק להשתמש בשירות.
              </p>
            </Section>

            <Section title="2. תיאור השירות">
              <p>
                Shifter הוא כלי לניהול סידור משמרות ועבודה. השירות מאפשר יצירת סידורים אוטומטיים,
                ניהול קבוצות, הגדרת אילוצים, צפייה בלוח זמנים, ושליחת התראות לחברי צוות.
                השירות מיועד לצוותים צבאיים, ביטחוניים, רפואיים, מסעדות ועוד.
              </p>
            </Section>

            <Section title="3. אחריות חשבון">
              <ul className="list-disc pr-5 space-y-1">
                <li>אתה אחראי לשמירה על סודיות הסיסמה שלך</li>
                <li>אתה אחראי לכל הפעילות שמתבצעת תחת החשבון שלך</li>
                <li>עליך לספק מידע מדויק ועדכני בעת ההרשמה</li>
                <li>עליך להודיע לנו מיידית על כל שימוש לא מורשה בחשבונך</li>
                <li>אנו שומרים את הזכות להשעות חשבונות שמפרים את התנאים</li>
              </ul>
            </Section>

            <Section title="4. מנוי וחיוב">
              <p>
                השירות מציע תוכניות מנוי בתשלום דרך LemonSqueezy. פרטי התמחור מפורטים בעמוד התמחור.
              </p>
              <ul className="list-disc pr-5 space-y-1 mt-2">
                <li>חיובים מתבצעים מראש לתקופת המנוי</li>
                <li>ביטול מנוי ייכנס לתוקף בסוף תקופת החיוב הנוכחית</li>
                <li>אין החזרים עבור תקופות חלקיות</li>
                <li>אנו שומרים את הזכות לשנות מחירים עם הודעה מראש של 30 יום</li>
              </ul>
            </Section>

            <Section title="5. הגבלת אחריות">
              <div className="bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800 rounded-lg p-4 my-3">
                <p className="font-semibold text-amber-900 dark:text-amber-200 mb-1">⚠️ חשוב:</p>
                <p className="text-amber-800 dark:text-amber-300">
                  הסידור הוא כלי עזר — לא ערובה. המשתמשים אחראים לאמת את הסידורים שנוצרו.
                  Shifter אינו אחראי לנזקים שנגרמו כתוצאה מסידור שגוי, חוסר כיסוי, או כל תקלה בשירות.
                </p>
              </div>
              <p>
                השירות מסופק &quot;כמות שהוא&quot; (AS IS). אנו לא מתחייבים לזמינות מלאה, דיוק מושלם של הסידורים,
                או התאמה לכל מצב. האחריות הסופית על סידור העבודה היא של המנהל.
              </p>
            </Section>

            <Section title="6. קניין רוחני">
              <p>
                כל הזכויות בשירות, כולל קוד המקור, העיצוב, הלוגו והאלגוריתמים, שייכות ל-Ofek Labs.
                תוכן שאתה מזין לשירות (שמות, סידורים, אילוצים) נשאר בבעלותך.
                אנו לא נשתמש בתוכן שלך למטרות שיווקיות.
              </p>
            </Section>

            <Section title="7. סיום">
              <ul className="list-disc pr-5 space-y-1">
                <li>תוכל לבקש מחיקת החשבון שלך בכל עת</li>
                <li>לאחר מחיקה, כל המידע האישי שלך יימחק תוך 30 יום</li>
                <li>אנו שומרים את הזכות לסיים את השירות עם הודעה מראש של 30 יום</li>
                <li>במקרה של הפרה חמורה, אנו רשאים להשעות את החשבון מיידית</li>
              </ul>
            </Section>

            <Section title="8. דין חל">
              <p>
                תנאים אלה כפופים לחוקי מדינת ישראל. כל סכסוך יידון בבתי המשפט המוסמכים בישראל.
              </p>
            </Section>

            <Section title="9. שינויים בתנאים">
              <p>
                אנו שומרים את הזכות לעדכן תנאים אלה. שינויים מהותיים יפורסמו באפליקציה ובאימייל
                לפחות 14 יום לפני כניסתם לתוקף. המשך השימוש לאחר עדכון מהווה הסכמה לתנאים החדשים.
              </p>
            </Section>

            <Section title="10. יצירת קשר">
              <p>
                לשאלות בנוגע לתנאי השימוש, ניתן לפנות אלינו:{" "}
                <a href={`mailto:${CONTACT_EMAIL}`} className="text-blue-600 hover:underline">{CONTACT_EMAIL}</a>
              </p>
            </Section>
          </div>
        </div>

        {/* Divider */}
        <hr className="my-12 border-slate-200 dark:border-slate-700" />

        {/* English Section */}
        <div dir="ltr">
          <h1 className="text-2xl font-bold text-slate-900 dark:text-white mb-2">Terms of Service</h1>
          <p className="text-sm text-slate-400 mb-8">Last updated: May 2026</p>

          <div className="space-y-6 text-slate-700 dark:text-slate-300 leading-relaxed text-sm">
            <Section title="1. Acceptance of Terms">
              <p>
                By using the Shifter application by Ofek Labs (the &quot;Service&quot;), you agree to these Terms of Service.
                If you do not agree, please discontinue use of the Service.
              </p>
            </Section>

            <Section title="2. Service Description">
              <p>
                Shifter is a scheduling and shift management tool. The Service enables automatic schedule generation,
                team management, constraint definition, schedule viewing, and team notifications.
                It is designed for military, security, medical, restaurant, and other shift-based teams.
              </p>
            </Section>

            <Section title="3. Account Responsibilities">
              <ul className="list-disc pl-5 space-y-1">
                <li>You are responsible for maintaining the confidentiality of your password</li>
                <li>You are responsible for all activity under your account</li>
                <li>You must provide accurate and current information during registration</li>
                <li>You must notify us immediately of any unauthorized use of your account</li>
                <li>We reserve the right to suspend accounts that violate these terms</li>
              </ul>
            </Section>

            <Section title="4. Subscription and Billing">
              <p>
                The Service offers paid subscription plans through LemonSqueezy. Pricing details are available on the pricing page.
              </p>
              <ul className="list-disc pl-5 space-y-1 mt-2">
                <li>Charges are billed in advance for the subscription period</li>
                <li>Cancellation takes effect at the end of the current billing period</li>
                <li>No refunds for partial periods</li>
                <li>We reserve the right to change prices with 30 days advance notice</li>
              </ul>
            </Section>

            <Section title="5. Limitation of Liability">
              <div className="bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800 rounded-lg p-4 my-3">
                <p className="font-semibold text-amber-900 dark:text-amber-200 mb-1">⚠️ Important:</p>
                <p className="text-amber-800 dark:text-amber-300">
                  The scheduling tool is a tool — not a guarantee. Users are responsible for verifying generated schedules.
                  Shifter is not liable for damages resulting from incorrect schedules, coverage gaps, or any service disruption.
                </p>
              </div>
              <p>
                The Service is provided &quot;AS IS&quot;. We do not guarantee full availability, perfect schedule accuracy,
                or suitability for every situation. Final responsibility for work schedules lies with the manager.
              </p>
            </Section>

            <Section title="6. Intellectual Property">
              <p>
                All rights in the Service, including source code, design, logo, and algorithms, belong to Ofek Labs.
                Content you enter into the Service (names, schedules, constraints) remains your property.
                We will not use your content for marketing purposes.
              </p>
            </Section>

            <Section title="7. Termination">
              <ul className="list-disc pl-5 space-y-1">
                <li>You may request account deletion at any time</li>
                <li>After deletion, all personal data will be removed within 30 days</li>
                <li>We reserve the right to terminate the Service with 30 days advance notice</li>
                <li>In case of serious violation, we may suspend the account immediately</li>
              </ul>
            </Section>

            <Section title="8. Governing Law">
              <p>
                These terms are governed by the laws of the State of Israel. Any dispute shall be adjudicated
                in the competent courts of Israel.
              </p>
            </Section>

            <Section title="9. Changes to Terms">
              <p>
                We reserve the right to update these terms. Material changes will be published in the application
                and via email at least 14 days before taking effect. Continued use after an update constitutes
                acceptance of the new terms.
              </p>
            </Section>

            <Section title="10. Contact">
              <p>
                For questions regarding these Terms of Service, contact us at:{" "}
                <a href={`mailto:${CONTACT_EMAIL}`} className="text-blue-600 hover:underline">{CONTACT_EMAIL}</a>
              </p>
            </Section>
          </div>
        </div>

        {/* Footer */}
        <footer className="mt-12 pt-8 border-t border-slate-200 dark:border-slate-700 text-center text-xs text-slate-400 space-y-2">
          <div className="flex items-center justify-center gap-4">
            <Link href="/terms" className="text-blue-600 hover:underline">תנאי שימוש</Link>
            <span>·</span>
            <Link href="/privacy" className="text-blue-600 hover:underline">מדיניות פרטיות</Link>
            <span>·</span>
            <Link href="/" className="text-blue-600 hover:underline">חזרה לדף הבית</Link>
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
