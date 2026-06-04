import type { Locale } from "@/lib/i18n/locales";

export type LandingLang = Locale;

export const LANDING_CONTENT: Record<LandingLang, {
  nav: { features: string; howItWorks: string; about: string; faq: string; signIn: string; getStarted: string };
  hero: { title1: string; title2: string; subtitle: string; cta: string; signIn: string };
  features: { title: string; subtitle: string; items: Array<{ icon: string; title: string; desc: string }> };
  howItWorks: { title: string; steps: Array<{ title: string; desc: string }> };
  about: { title: string; subtitle: string; paragraphs: string[]; stats: Array<{ value: string; label: string }> };
  faq: { title: string; subtitle: string; items: Array<{ q: string; a: string }> };
  cta: { title: string; subtitle: string; primary: string; secondary: string };
  footer: { about: string; faq: string; terms: string; privacy: string; signIn: string };
  dir: "ltr" | "rtl";
}> = {
  en: {
    dir: "ltr",
    nav: { features: "Features", howItWorks: "How it Works", about: "About", faq: "FAQ", signIn: "Sign In", getStarted: "Get Started Free" },
    hero: { title1: "Smart Shift", title2: "Scheduling", subtitle: "Shifter generates fair, balanced shift schedules at the click of a button. No spreadsheets. No headaches. No arguments.", cta: "Get Started - It's Free", signIn: "I have an account - Sign In" },
    features: {
      title: "Why Shifter?",
      subtitle: "Everything you need to manage shift schedules - in one place",
      items: [
        { icon: "⚡", title: "Auto Scheduling", desc: "Smart algorithm distributes shifts fairly while respecting all constraints" },
        { icon: "📱", title: "Mobile First", desc: "Everyone sees their shifts on mobile - even offline with no internet" },
        { icon: "⚖️", title: "Fair Distribution", desc: "The system balances workload across all people and prevents overloading" },
        { icon: "🔒", title: "Flexible Constraints", desc: "Set minimum rest, personal restrictions, required qualifications and more" },
        { icon: "📊", title: "Statistics", desc: "See who did how many shifts, who's overloaded, who's available - in real time" },
        { icon: "🔔", title: "Notifications", desc: "Every schedule change is pushed directly to team members' phones" },
      ],
    },
    howItWorks: {
      title: "How it Works",
      steps: [
        { title: "Define", desc: "Add people, tasks, and constraints to your group" },
        { title: "Generate", desc: "Click 'Create Schedule' - the algorithm does the rest" },
        { title: "Publish", desc: "Review the draft and publish - everyone gets notified" },
      ],
    },
    about: {
      title: "About",
      subtitle: "The story behind Shifter",
      paragraphs: [
        "Shifter was born from the real need of team leaders who spend hours building shift schedules in Excel - and someone always ends up unhappy.",
        "The system uses a constraint-optimization algorithm (CP-SAT) that distributes shifts fairly and balanced, while respecting all constraints: minimum rest, required qualifications, personal preferences, and more.",
        "The platform is built mobile-first - because team members need to check their schedule in the field, even without internet. Every change is pushed directly to their phone.",
        "Shifter works for military teams, security, factories, hospitals, restaurants - anywhere that needs smart, fair shift distribution.",
      ],
      stats: [
        { value: "90%", label: "Time Saved" },
        { value: "0", label: "Spreadsheets" },
        { value: "24/7", label: "Mobile Access" },
      ],
    },
    faq: {
      title: "FAQ",
      subtitle: "Answers to common questions",
      items: [
        { q: "Is it really free?", a: "You get a 14-day free trial with full access to all features. After that, you choose a paid plan that fits your team size. No credit card required to start." },
        { q: "How many people can I add?", a: "During the trial there's no member limit. On paid plans, limits depend on your tier: Starter (15), Growth (30), Team (60), Org (90), or Unlimited." },
        { q: "Is my data secure?", a: "Yes. All data is encrypted, passwords are hashed with BCrypt, and all communication uses HTTPS. There's full isolation between groups. You can also enable biometric login (fingerprint/face) for extra security." },
        { q: "Can I view the schedule offline?", a: "Yes! The app caches your latest schedule on your device. Even without signal, you can see your shifts." },
        { q: "What if someone can't make their shift?", a: "The admin can mark them as unavailable with a reason (vacation, sick, personal) and re-run the algorithm. It automatically finds a replacement." },
        { q: "Can I import people from Excel?", a: "Yes. You can import people and tasks from CSV or Excel files with one click." },
        { q: "Does it work for different industries?", a: "Yes! Shifter comes with templates for military, restaurants, hospitals, and security. Each template includes pre-configured tasks, qualifications, and constraints." },
        { q: "Can I see statistics and graphs?", a: "Yes. The stats tab shows bar charts, burden breakdown, trend lines, and fairness comparisons. You can filter by time range (7d, 14d, 30d, 90d)." },
        { q: "Can I login with fingerprint?", a: "Yes! After your first login, you can enable biometric authentication (fingerprint or face) for instant passwordless login on subsequent visits." },
      ],
    },
    cta: { title: "Ready to get started?", subtitle: "Sign up free in 30 seconds. No credit card needed.", primary: "Create Free Account", secondary: "Sign In to Existing Account" },
    footer: { about: "About", faq: "FAQ", terms: "Terms", privacy: "Privacy", signIn: "Sign In" },
  },
  he: {
    dir: "rtl",
    nav: { features: "יתרונות", howItWorks: "איך זה עובד", about: "אודות", faq: "שאלות נפוצות", signIn: "כניסה", getStarted: "הרשמה חינם" },
    hero: { title1: "סידור משמרות", title2: "חכם ואוטומטי", subtitle: "Shifter מייצר סידור עבודה הוגן ומאוזן בלחיצת כפתור. בלי אקסלים, בלי כאב ראש, בלי ויכוחים.", cta: "התחל עכשיו - חינם", signIn: "יש לי חשבון - כניסה" },
    features: {
      title: "למה Shifter?",
      subtitle: "כל מה שצריך כדי לנהל סידור עבודה - ממקום אחד",
      items: [
        { icon: "⚡", title: "סידור אוטומטי", desc: "אלגוריתם חכם שמחלק משמרות בצורה הוגנת תוך שמירה על כל האילוצים" },
        { icon: "📱", title: "עובד בנייד", desc: "כל חייל רואה את המשמרות שלו בנייד - גם בלי אינטרנט" },
        { icon: "⚖️", title: "חלוקה הוגנת", desc: "המערכת מאזנת עומס בין כל האנשים ומונעת העמסה על אחד" },
        { icon: "🔒", title: "אילוצים גמישים", desc: "הגדר מנוחה מינימלית, הגבלות אישיות, כישורים נדרשים ועוד" },
        { icon: "📊", title: "סטטיסטיקות", desc: "ראה מי עשה כמה משמרות, מי עמוס ומי פנוי - בזמן אמת" },
        { icon: "🔔", title: "התראות", desc: "כל שינוי בסידור מגיע ישירות לנייד של החיילים" },
      ],
    },
    howItWorks: {
      title: "איך זה עובד?",
      steps: [
        { title: "הגדר", desc: "הוסף אנשים, משימות ואילוצים לקבוצה שלך" },
        { title: "הפעל", desc: "לחץ על 'צור סידור' - האלגוריתם עושה את השאר" },
        { title: "פרסם", desc: "בדוק את הטיוטה ופרסם - כולם מקבלים התראה בנייד" },
      ],
    },
    about: {
      title: "אודות",
      subtitle: "הסיפור מאחורי Shifter",
      paragraphs: [
        "Shifter נולד מתוך הצורך האמיתי של מפקדים ומנהלי צוותים שמבלים שעות על סידור משמרות באקסל - ובסוף תמיד מישהו לא מרוצה.",
        "המערכת משתמשת באלגוריתם אופטימיזציה (CP-SAT) שמחלק משמרות בצורה הוגנת ומאוזנת, תוך שמירה על כל האילוצים: מנוחה מינימלית, כישורים נדרשים, העדפות אישיות ועוד.",
        "הפלטפורמה בנויה עם דגש על חוויית משתמש בנייד - כי חיילים צריכים לראות את הסידור שלהם בשטח, גם בלי אינטרנט. כל שינוי בסידור מגיע ישירות לנייד.",
        "Shifter מתאים לצוותים צבאיים, שמירה, מפעלים, בתי חולים, מסעדות - כל מקום שצריך לחלק משמרות בצורה חכמה והוגנת.",
      ],
      stats: [
        { value: "90%", label: "חיסכון בזמן" },
        { value: "0", label: "אקסלים" },
        { value: "24/7", label: "גישה מהנייד" },
      ],
    },
    faq: {
      title: "שאלות נפוצות",
      subtitle: "תשובות לשאלות הכי נפוצות",
      items: [
        { q: "האם זה באמת חינם?", a: "יש תקופת ניסיון חינם של 14 ימים עם גישה מלאה לכל התכונות. אחרי זה בוחרים תוכנית בתשלום שמתאימה לגודל הצוות. לא צריך כרטיס אשראי כדי להתחיל." },
        { q: "כמה אנשים אפשר להוסיף?", a: "בתקופת הניסיון אין הגבלה על מספר החברים. בתוכניות בתשלום, ההגבלה תלויה בתוכנית: Starter (15), Growth (30), Team (60), Org (90), או ללא הגבלה." },
        { q: "האם המידע שלי מאובטח?", a: "כן. כל המידע מוצפן, סיסמאות מאובטחות ב-BCrypt, והתקשורת מוצפנת ב-HTTPS. יש בידוד מלא בין קבוצות. ניתן גם להפעיל כניסה ביומטרית (טביעת אצבע/זיהוי פנים) לאבטחה נוספת." },
        { q: "אפשר לראות את הסידור בלי אינטרנט?", a: "כן! האפליקציה שומרת את הסידור האחרון במכשיר. גם בלי קליטה תוכל לראות את המשמרות שלך." },
        { q: "מה קורה אם מישהו לא יכול להגיע?", a: "המנהל יכול לסמן אותו כלא זמין עם סיבה (חופשה, מחלה, אישי) ולהפעיל את האלגוריתם מחדש. הוא ימצא מחליף אוטומטית." },
        { q: "אפשר לייבא אנשים מאקסל?", a: "כן. אפשר לייבא רשימת אנשים ומשימות מקובץ CSV או Excel בלחיצה אחת." },
        { q: "האם זה מתאים לתעשיות שונות?", a: "כן! Shifter מגיע עם תבניות מוכנות לצבא, מסעדות, בתי חולים ואבטחה. כל תבנית כוללת משימות, הכשרות ואילוצים מוגדרים מראש." },
        { q: "אפשר לראות סטטיסטיקות וגרפים?", a: "כן. לשונית הסטטיסטיקות מציגה גרפי עמודות, פילוח לפי קושי, מגמות לאורך זמן והשוואת הוגנות. ניתן לסנן לפי טווח זמן (7, 14, 30, 90 ימים)." },
        { q: "אפשר להתחבר עם טביעת אצבע?", a: "כן! אחרי ההתחברות הראשונה, ניתן להפעיל כניסה ביומטרית (טביעת אצבע או זיהוי פנים) לכניסה מהירה ללא סיסמה בביקורים הבאים." },
      ],
    },
    cta: { title: "מוכן להתחיל?", subtitle: "הרשמה חינם תוך 30 שניות. בלי כרטיס אשראי.", primary: "צור חשבון חינם", secondary: "כניסה לחשבון קיים" },
    footer: { about: "אודות", faq: "שאלות נפוצות", terms: "תנאי שימוש", privacy: "פרטיות", signIn: "כניסה" },
  },
  ru: {
    dir: "ltr",
    nav: { features: "Возможности", howItWorks: "Как это работает", about: "О нас", faq: "FAQ", signIn: "Войти", getStarted: "Начать бесплатно" },
    hero: { title1: "Умное", title2: "Расписание смен", subtitle: "Shifter создаёт справедливое и сбалансированное расписание одним нажатием. Без таблиц. Без головной боли. Без споров.", cta: "Начать - бесплатно", signIn: "У меня есть аккаунт - Войти" },
    features: {
      title: "Почему Shifter?",
      subtitle: "Всё для управления сменами - в одном месте",
      items: [
        { icon: "⚡", title: "Автоматическое расписание", desc: "Умный алгоритм распределяет смены справедливо с учётом всех ограничений" },
        { icon: "📱", title: "Мобильная версия", desc: "Каждый видит свои смены на телефоне - даже без интернета" },
        { icon: "⚖️", title: "Справедливое распределение", desc: "Система балансирует нагрузку и предотвращает перегрузку" },
        { icon: "🔒", title: "Гибкие ограничения", desc: "Минимальный отдых, личные ограничения, квалификации и многое другое" },
        { icon: "📊", title: "Статистика", desc: "Кто сколько отработал, кто перегружен, кто свободен - в реальном времени" },
        { icon: "🔔", title: "Уведомления", desc: "Каждое изменение расписания приходит прямо на телефон" },
      ],
    },
    howItWorks: {
      title: "Как это работает",
      steps: [
        { title: "Настройте", desc: "Добавьте людей, задачи и ограничения в группу" },
        { title: "Запустите", desc: "Нажмите 'Создать расписание' - алгоритм сделает остальное" },
        { title: "Опубликуйте", desc: "Проверьте черновик и опубликуйте - все получат уведомление" },
      ],
    },
    about: {
      title: "О нас",
      subtitle: "История Shifter",
      paragraphs: [
        "Shifter родился из реальной потребности руководителей, которые тратят часы на составление расписания в Excel - и кто-то всегда остаётся недоволен.",
        "Система использует алгоритм оптимизации (CP-SAT), который распределяет смены справедливо и сбалансированно, соблюдая все ограничения.",
        "Платформа создана для мобильных устройств - потому что сотрудники должны видеть расписание в поле, даже без интернета.",
        "Shifter подходит для военных, охраны, заводов, больниц, ресторанов - везде, где нужно умное распределение смен.",
      ],
      stats: [
        { value: "90%", label: "Экономия времени" },
        { value: "0", label: "Таблиц" },
        { value: "24/7", label: "Мобильный доступ" },
      ],
    },
    faq: {
      title: "Частые вопросы",
      subtitle: "Ответы на популярные вопросы",
      items: [
        { q: "Это действительно бесплатно?", a: "Вы получаете 14-дневный бесплатный пробный период с полным доступом ко всем функциям. После этого выбираете платный план под размер команды. Кредитная карта не нужна для начала." },
        { q: "Сколько людей можно добавить?", a: "Во время пробного периода ограничений нет. На платных планах лимиты зависят от тарифа: Starter (15), Growth (30), Team (60), Org (90) или без ограничений." },
        { q: "Мои данные в безопасности?", a: "Да. Все данные зашифрованы, пароли хешируются BCrypt, связь через HTTPS. Полная изоляция между группами. Также можно включить биометрический вход (отпечаток/лицо)." },
        { q: "Можно смотреть расписание офлайн?", a: "Да! Приложение сохраняет последнее расписание на устройстве." },
        { q: "Что если кто-то не может выйти на смену?", a: "Админ отмечает его как недоступного с причиной (отпуск, болезнь, личное) и перезапускает алгоритм. Замена находится автоматически." },
        { q: "Можно импортировать из Excel?", a: "Да. Импорт людей и задач из CSV или Excel одним кликом." },
        { q: "Подходит для разных отраслей?", a: "Да! Shifter включает шаблоны для армии, ресторанов, больниц и охраны с предустановленными задачами и ограничениями." },
        { q: "Есть статистика и графики?", a: "Да. Вкладка статистики показывает графики, разбивку по сложности, тренды и сравнение справедливости." },
        { q: "Можно входить по отпечатку?", a: "Да! После первого входа можно включить биометрическую аутентификацию для мгновенного входа без пароля." },
      ],
    },
    cta: { title: "Готовы начать?", subtitle: "Регистрация бесплатна за 30 секунд. Без кредитной карты.", primary: "Создать бесплатный аккаунт", secondary: "Войти в существующий аккаунт" },
    footer: { about: "О нас", faq: "FAQ", terms: "Условия", privacy: "Конфиденциальность", signIn: "Войти" },
  },
};

export const LANDING_LEGAL_LINKS: Record<LandingLang, Array<{ href: string; label: string }>> = {
  en: [
    { href: "/terms", label: "Terms" },
    { href: "/privacy", label: "Privacy" },
    { href: "/security", label: "Security" },
    { href: "/subprocessors", label: "Subprocessors" },
  ],
  he: [
    { href: "/terms", label: "תנאי שימוש" },
    { href: "/privacy", label: "פרטיות" },
    { href: "/security", label: "אבטחה" },
    { href: "/subprocessors", label: "מעבדי משנה" },
  ],
  ru: [
    { href: "/terms", label: "Условия" },
    { href: "/privacy", label: "Конфиденциальность" },
    { href: "/security", label: "Безопасность" },
    { href: "/subprocessors", label: "Субпроцессоры" },
  ],
};
