import Link from "next/link";
import ShifterLogo from "@/components/shell/ShifterLogo";

export default function PrivacyPage() {
  return (
    <div className="min-h-screen bg-white" dir="rtl">
      {/* Header */}
      <header className="border-b border-slate-100 px-6 py-4">
        <div className="max-w-3xl mx-auto flex items-center justify-between">
          <Link href="/" className="flex items-center gap-2 text-slate-900 hover:text-blue-600 transition-colors">
            <ShifterLogo size={24} />
            <span className="font-bold text-sm">Shifter</span>
          </Link>
          <Link href="/terms" className="text-sm text-blue-600 hover:underline">
            תנאי שימוש
          </Link>
        </div>
      </header>

      {/* Content */}
      <main className="px-6 py-12 max-w-3xl mx-auto">
        <h1 className="text-2xl font-bold text-slate-900 mb-2">מדיניות פרטיות</h1>
        <p className="text-sm text-slate-400 mb-8">עדכון אחרון: {new Date().toLocaleDateString("he-IL", { month: "long", year: "numeric" })}</p>

        <div className="prose prose-slate prose-sm max-w-none space-y-6 text-slate-700 leading-relaxed">
          <Section title="1. מידע שאנו אוספים">
            <p>אנו אוספים את המידע הבא:</p>
            <ul className="list-disc pr-5 space-y-1">
              <li><strong>מידע הרשמה:</strong> שם, כתובת אימייל, מספר טלפון (אופציונלי), תאריך לידה (אופציונלי)</li>
              <li><strong>מידע שימוש:</strong> סידורי עבודה, חברויות בקבוצות, אילוצים שהוגדרו</li>
              <li><strong>מידע טכני:</strong> כתובת IP, סוג דפדפן, זמני גישה (לצורכי אבטחה בלבד)</li>
            </ul>
          </Section>

          <Section title="2. כיצד אנו משתמשים במידע">
            <ul className="list-disc pr-5 space-y-1">
              <li>יצירת סידורי עבודה ומשמרות</li>
              <li>שליחת התראות על שינויים בסידור</li>
              <li>אימות זהות ואבטחת החשבון</li>
              <li>שיפור השירות ותיקון באגים</li>
            </ul>
            <p className="mt-2">
              <strong>אנו לא:</strong> מוכרים מידע לצדדים שלישיים, משתמשים במידע לפרסום,
              או חולקים מידע אישי עם גורמים חיצוניים ללא הסכמתך.
            </p>
          </Section>

          <Section title="3. אחסון ואבטחת מידע">
            <ul className="list-disc pr-5 space-y-1">
              <li>כל המידע מאוחסן בשרתים מאובטחים עם הצפנה</li>
              <li>סיסמאות מוצפנות באמצעות BCrypt ולא נשמרות בטקסט גלוי</li>
              <li>תקשורת מוצפנת באמצעות TLS/HTTPS</li>
              <li>גישה למסד הנתונים מוגבלת לצוות הפיתוח בלבד</li>
              <li>בידוד מלא בין מרחבי עבודה (multi-tenancy)</li>
            </ul>
          </Section>

          <Section title="4. שיתוף מידע">
            <p>מידע אישי משותף רק במקרים הבאים:</p>
            <ul className="list-disc pr-5 space-y-1">
              <li><strong>בתוך הקבוצה:</strong> שמך ומשמרותיך גלויים לחברי הקבוצה שלך</li>
              <li><strong>למנהל הקבוצה:</strong> מנהלים רואים מידע נוסף כמו מספר טלפון (אם סופק)</li>
              <li><strong>דרישה חוקית:</strong> אם נדרש על פי חוק או צו בית משפט</li>
            </ul>
          </Section>

          <Section title="5. עוגיות ואחסון מקומי">
            <p>
              אנו משתמשים ב-localStorage בדפדפן לשמירת:
            </p>
            <ul className="list-disc pr-5 space-y-1">
              <li>טוקן גישה (לאימות)</li>
              <li>העדפות שפה ותצוגה</li>
              <li>מטמון סידור עבודה (לצפייה אופליין)</li>
            </ul>
            <p className="mt-2">אנו לא משתמשים בעוגיות מעקב או כלי אנליטיקה של צדדים שלישיים.</p>
          </Section>

          <Section title="6. זכויותיך">
            <p>יש לך את הזכות:</p>
            <ul className="list-disc pr-5 space-y-1">
              <li><strong>לצפות</strong> במידע האישי שלך (דרך עמוד הפרופיל)</li>
              <li><strong>לתקן</strong> מידע שגוי</li>
              <li><strong>למחוק</strong> את החשבון שלך ואת כל המידע הקשור</li>
              <li><strong>לייצא</strong> את הנתונים שלך (סידורים בפורמט CSV)</li>
            </ul>
          </Section>

          <Section title="7. שמירת מידע">
            <p>
              מידע אישי נשמר כל עוד החשבון שלך פעיל. לאחר מחיקת חשבון:
            </p>
            <ul className="list-disc pr-5 space-y-1">
              <li>מידע אישי נמחק תוך 30 יום</li>
              <li>לוגים אנונימיים נשמרים עד 90 יום לצורכי אבטחה</li>
              <li>גיבויים מוצפנים נמחקים תוך 60 יום</li>
            </ul>
          </Section>

          <Section title="8. ילדים">
            <p>
              השירות אינו מיועד לילדים מתחת לגיל 16. אנו לא אוספים ביודעין מידע מילדים.
              אם נודע לנו שנאסף מידע של קטין, נמחק אותו מיידית.
            </p>
          </Section>

          <Section title="9. שינויים במדיניות">
            <p>
              אנו עשויים לעדכן מדיניות זו מעת לעת. שינויים מהותיים יפורסמו באפליקציה
              ובאימייל. המשך השימוש לאחר עדכון מהווה הסכמה למדיניות החדשה.
            </p>
          </Section>

          <Section title="10. יצירת קשר">
            <p>
              לשאלות בנוגע לפרטיות, ניתן לפנות אלינו:{" "}
              <a href="mailto:privacy@shifter.app" className="text-blue-600 hover:underline">privacy@shifter.app</a>
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
