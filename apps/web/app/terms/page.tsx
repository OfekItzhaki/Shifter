import Link from "next/link";
import ShifterLogo from "@/components/shell/ShifterLogo";

export default function TermsPage() {
  return (
    <div className="min-h-screen bg-white" dir="rtl">
      {/* Header */}
      <header className="border-b border-slate-100 px-6 py-4">
        <div className="max-w-3xl mx-auto flex items-center justify-between">
          <Link href="/" className="flex items-center gap-2 text-slate-900 hover:text-blue-600 transition-colors">
            <ShifterLogo size={24} />
            <span className="font-bold text-sm">Shifter</span>
          </Link>
          <Link href="/privacy" className="text-sm text-blue-600 hover:underline">
            מדיניות פרטיות
          </Link>
        </div>
      </header>

      {/* Content */}
      <main className="px-6 py-12 max-w-3xl mx-auto">
        <h1 className="text-2xl font-bold text-slate-900 mb-2">תנאי שימוש</h1>
        <p className="text-sm text-slate-400 mb-8">עדכון אחרון: {new Date().toLocaleDateString("he-IL", { month: "long", year: "numeric" })}</p>

        <div className="prose prose-slate prose-sm max-w-none space-y-6 text-slate-700 leading-relaxed">
          <Section title="1. הסכמה לתנאים">
            <p>
              בשימוש באפליקציית Shifter (להלן: &quot;השירות&quot;), אתה מסכים לתנאי שימוש אלה.
              אם אינך מסכים לתנאים, אנא הפסק להשתמש בשירות.
            </p>
          </Section>

          <Section title="2. תיאור השירות">
            <p>
              Shifter הוא כלי לניהול סידור משמרות ועבודה. השירות מאפשר יצירת סידורים אוטומטיים,
              ניהול קבוצות, הגדרת אילוצים וצפייה בלוח זמנים.
            </p>
          </Section>

          <Section title="3. חשבון משתמש">
            <ul className="list-disc pr-5 space-y-1">
              <li>אתה אחראי לשמירה על סודיות הסיסמה שלך</li>
              <li>אתה אחראי לכל הפעילות שמתבצעת תחת החשבון שלך</li>
              <li>עליך לספק מידע מדויק ועדכני בעת ההרשמה</li>
              <li>אנו שומרים את הזכות להשעות חשבונות שמפרים את התנאים</li>
            </ul>
          </Section>

          <Section title="4. שימוש מותר">
            <p>אתה מתחייב:</p>
            <ul className="list-disc pr-5 space-y-1">
              <li>להשתמש בשירות למטרות חוקיות בלבד</li>
              <li>לא לנסות לגשת למידע של משתמשים אחרים ללא הרשאה</li>
              <li>לא להעמיס על השרתים בצורה מכוונת</li>
              <li>לא להעתיק, לשנות או להפיץ את קוד המקור של השירות</li>
            </ul>
          </Section>

          <Section title="5. תוכן משתמש">
            <p>
              כל מידע שאתה מזין לשירות (שמות, מספרי טלפון, סידורים) נשאר בבעלותך.
              אנו לא נשתמש בתוכן שלך למטרות שיווקיות או נמכור אותו לצדדים שלישיים.
            </p>
          </Section>

          <Section title="6. זמינות השירות">
            <p>
              אנו שואפים לספק שירות זמין 24/7, אך אין אנו מתחייבים לזמינות מלאה.
              תחזוקה מתוכננת תתבצע בשעות שקטות ככל הניתן.
              אנו לא אחראים לנזקים שנגרמו כתוצאה מהפסקת שירות.
            </p>
          </Section>

          <Section title="7. הגבלת אחריות">
            <p>
              השירות מסופק &quot;כמות שהוא&quot; (AS IS). אנו לא מתחייבים שהסידורים שנוצרים
              יהיו מושלמים או יתאימו לכל מצב. האחריות הסופית על סידור העבודה היא של המנהל.
            </p>
          </Section>

          <Section title="8. שינויים בתנאים">
            <p>
              אנו שומרים את הזכות לעדכן תנאים אלה. שינויים מהותיים יפורסמו באפליקציה.
              המשך השימוש לאחר עדכון מהווה הסכמה לתנאים החדשים.
            </p>
          </Section>

          <Section title="9. ביטול חשבון">
            <p>
              תוכל לבקש מחיקת החשבון שלך בכל עת. לאחר מחיקה, כל המידע האישי שלך יימחק
              תוך 30 יום, למעט מידע שנדרש לשמירה על פי חוק.
            </p>
          </Section>

          <Section title="10. יצירת קשר">
            <p>
              לשאלות בנוגע לתנאי השימוש, ניתן לפנות אלינו בכתובת:{" "}
              <a href="mailto:support@shifter.app" className="text-blue-600 hover:underline">support@shifter.app</a>
            </p>
          </Section>
        </div>
      </main>
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div>
      <h2 className="text-base font-semibold text-slate-900 mb-2">{title}</h2>
      {children}
    </div>
  );
}
