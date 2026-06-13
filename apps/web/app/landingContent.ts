import type { Locale } from "@/lib/i18n/locales";

export type LandingLang = Locale;

type FinderItem = {
  label: string;
  desc: string;
  href: string;
  keywords: string[];
};

type LandingContent = {
  dir: "ltr" | "rtl";
  nav: {
    product: string;
    features: string;
    teams: string;
    faq: string;
    contact: string;
    signIn: string;
    getStarted: string;
    tagline: string;
    menu: string;
  };
  hero: {
    eyebrow: string;
    title: string;
    subtitle: string;
    primary: string;
    secondary: string;
    install: string;
  };
  finder: {
    title: string;
    placeholder: string;
    empty: string;
    items: FinderItem[];
  };
  proof: Array<{ value: string; label: string }>;
  preview: {
    workspace: string;
    productSubtitle: string;
    publishLabel: string;
    desktopTitle: string;
    desktopSubtitle: string;
    scheduleView: string;
    pickView: string;
    operationsView: string;
    slotsLabel: string;
    requestsLabel: string;
    coverageLabel: string;
    openWindowLabel: string;
    adminReviewLabel: string;
    mobileHeader: string;
    mobileSubtitle: string;
    mobileTabs: string[];
    opsSnapshot: string;
    live: string;
    metrics: Array<{ value: string; label: string }>;
    shifts: Array<{ time: string; name: string; status: string; assignee: string }>;
    alerts: string[];
  };
  features: {
    title: string;
    subtitle: string;
    items: Array<{ title: string; desc: string; detail: string }>;
  };
  teams: {
    title: string;
    subtitle: string;
    items: string[];
  };
  workflow: {
    title: string;
    steps: Array<{ title: string; desc: string }>;
  };
  trust: Array<{ title: string; desc: string; href?: string }>;
  contact: {
    title: string;
    subtitle: string;
    email: string;
    demo: string;
  };
  faq: {
    title: string;
    subtitle: string;
    items: Array<{ q: string; a: string }>;
  };
  cta: {
    title: string;
    subtitle: string;
    primary: string;
    secondary: string;
  };
  footer: {
    product: string;
    faq: string;
    signIn: string;
  };
};

const enFinder: FinderItem[] = [
  { label: "Manual self-service", desc: "Members pick shifts, join waitlists, request changes, report absence, and propose swaps.", href: "#features", keywords: ["manual", "self service", "pick", "waitlist", "swap", "absence"] },
  { label: "Automatic scheduling", desc: "Generate fair drafts from roles, rules, rest, availability, and coverage needs.", href: "#features", keywords: ["schedule", "automatic", "solver", "constraints", "fair", "roster"] },
  { label: "Imports and scan flow", desc: "Bring spreadsheet data in and prepare messy files for AI-assisted cleanup.", href: "#features", keywords: ["import", "excel", "csv", "scan", "ai", "files"] },
  { label: "Installable web app", desc: "Use Shifter as a PWA on phones and supported desktop browsers.", href: "#faq", keywords: ["pwa", "install", "iphone", "android", "desktop", "offline"] },
  { label: "Accessibility", desc: "Read the accessibility statement and supported access practices.", href: "/accessibility", keywords: ["accessibility", "a11y", "contrast", "keyboard", "screen reader"] },
  { label: "Security and customer hosting", desc: "Review security posture, privacy controls, and deployment options.", href: "/security", keywords: ["security", "privacy", "hosting", "server", "cloudflare", "data"] },
  { label: "Talk to support", desc: "Ask about rollout, pricing, hosting, or how to model your scheduling rules.", href: "#contact", keywords: ["support", "contact", "demo", "help", "sales"] },
];

const heFinder: FinderItem[] = [
  { label: "שירות עצמי ידני", desc: "חברי צוות בוחרים משמרות, מצטרפים לרשימת המתנה, מבקשים שינוי, מדווחים היעדרות ומציעים החלפות.", href: "#features", keywords: ["ידני", "שירות עצמי", "בחירה", "המתנה", "החלפה", "היעדרות"] },
  { label: "סידור אוטומטי", desc: "יוצרים טיוטות הוגנות לפי תפקידים, חוקים, מנוחה, זמינות וצרכי כיסוי.", href: "#features", keywords: ["סידור", "אוטומטי", "אילוצים", "הוגן", "משמרות"] },
  { label: "ייבוא וסריקה", desc: "מכניסים נתוני אקסל וקבצים מבולגנים ומכינים אותם לניקוי בעזרת AI.", href: "#features", keywords: ["ייבוא", "אקסל", "CSV", "סריקה", "AI", "קבצים"] },
  { label: "אפליקציית ווב להתקנה", desc: "משתמשים ב-Shifter כ-PWA בטלפונים ובדפדפני דסקטופ נתמכים.", href: "#faq", keywords: ["PWA", "התקנה", "אייפון", "אנדרואיד", "דסקטופ", "אופליין"] },
  { label: "נגישות", desc: "קוראים את הצהרת הנגישות ואת שיטות העבודה הנתמכות.", href: "/accessibility", keywords: ["נגישות", "מקלדת", "ניגודיות", "קורא מסך"] },
  { label: "אבטחה ואירוח לקוח", desc: "בודקים פרטיות, אבטחה ואפשרויות התקנה על שרת לקוח.", href: "/security", keywords: ["אבטחה", "פרטיות", "אירוח", "שרת", "מידע"] },
  { label: "שיחה עם תמיכה", desc: "שאלו על הטמעה, מחירים, אירוח או איך למדל את חוקי הסידור שלכם.", href: "#contact", keywords: ["תמיכה", "צור קשר", "דמו", "עזרה", "מכירות"] },
];

const ruFinder: FinderItem[] = [
  { label: "Ручное самообслуживание", desc: "Участники выбирают смены, встают в лист ожидания, запрашивают изменения, сообщают об отсутствии и предлагают обмены.", href: "#features", keywords: ["ручной", "самообслуживание", "смены", "лист ожидания", "обмен", "отсутствие"] },
  { label: "Автоматическое расписание", desc: "Создавайте справедливые черновики с учетом ролей, правил, отдыха, доступности и покрытия.", href: "#features", keywords: ["расписание", "автоматически", "solver", "ограничения", "справедливо"] },
  { label: "Импорт и сканирование", desc: "Загружайте таблицы и готовьте сложные файлы к очистке с помощью AI.", href: "#features", keywords: ["импорт", "excel", "csv", "скан", "ai", "файлы"] },
  { label: "Устанавливаемое веб-приложение", desc: "Используйте Shifter как PWA на телефонах и поддерживаемых desktop-браузерах.", href: "#faq", keywords: ["pwa", "установка", "iphone", "android", "desktop", "offline"] },
  { label: "Доступность", desc: "Ознакомьтесь с заявлением о доступности и поддерживаемыми практиками.", href: "/accessibility", keywords: ["доступность", "контраст", "клавиатура", "screen reader"] },
  { label: "Безопасность и клиентский хостинг", desc: "Проверьте безопасность, приватность и варианты развертывания.", href: "/security", keywords: ["безопасность", "приватность", "хостинг", "сервер", "данные"] },
  { label: "Связаться с поддержкой", desc: "Спросите о внедрении, ценах, хостинге или моделировании правил смен.", href: "#contact", keywords: ["поддержка", "контакт", "демо", "помощь", "продажи"] },
];

export const LANDING_CONTENT: Record<LandingLang, LandingContent> = {
  en: {
    dir: "ltr",
    nav: {
      product: "Product",
      features: "Features",
      teams: "Teams",
      faq: "FAQ",
      contact: "Contact",
      signIn: "Sign in",
      getStarted: "Start free",
      tagline: "Smart Shift Scheduling",
      menu: "Menu",
    },
    hero: {
      eyebrow: "Smart shift scheduling for real teams",
      title: "Build fair schedules without living in spreadsheets.",
      subtitle: "Shifter connects automatic scheduling, manual self-service, swaps, absences, imports, and mobile access in one web service your team can use from any phone.",
      primary: "Start free",
      secondary: "Sign in",
      install: "Installable on phones and supported desktop browsers as a PWA",
    },
    finder: { title: "Find the workflow you care about", placeholder: "Search imports, swaps, self-service, PWA, support...", empty: "No exact match yet. Try schedule, import, swap, mobile, security, or support.", items: enFinder },
    proof: [
      { value: "Manual", label: "self-service mode" },
      { value: "Auto", label: "scheduler mode" },
      { value: "24/7", label: "mobile access" },
      { value: "PWA", label: "installable web app" },
    ],
    preview: {
      workspace: "North Team",
      productSubtitle: "Admin workspace",
      publishLabel: "Publish",
      desktopTitle: "Self-service operations",
      desktopSubtitle: "Run the current cycle, watch coverage, and review queues.",
      scheduleView: "Schedule",
      pickView: "Pick Shifts",
      operationsView: "Operations",
      slotsLabel: "Slots",
      requestsLabel: "Requests",
      coverageLabel: "Coverage",
      openWindowLabel: "Window open",
      adminReviewLabel: "Admin review",
      mobileHeader: "My status",
      mobileSubtitle: "2/4 shifts selected. Minimum: 3.",
      mobileTabs: ["Status", "Available Shifts", "Waitlist"],
      opsSnapshot: "Ops snapshot",
      live: "Live",
      metrics: [
        { value: "18", label: "shifts" },
        { value: "7", label: "roles" },
        { value: "3", label: "alerts" },
      ],
      shifts: [
        { time: "06:00", name: "Morning prep", status: "Open", assignee: "2 open seats" },
        { time: "14:00", name: "Lunch service", status: "Waitlist", assignee: "Offer expires soon" },
        { time: "22:00", name: "Close shift", status: "Approved", assignee: "Dana Levi" },
      ],
      alerts: ["Under-filled slots need coverage", "2 absence reports waiting", "One swap proposal is pending"],
    },
    features: {
      title: "Two scheduling modes, one operating flow",
      subtitle: "Use the automatic scheduler when rules are complex, or open manual self-service when the team should choose from available shifts.",
      items: [
        { title: "Manual self-service", desc: "Members pick open shifts, join waitlists, cancel inside policy, report absence, request changes, propose swaps, and request time off.", detail: "New mode" },
        { title: "Automatic scheduling", desc: "Generate balanced rosters while respecting rest, qualifications, preferences, availability, and coverage rules.", detail: "CP-SAT" },
        { title: "Imports and scan flow", desc: "Bring people and shift data from files, then use AI assistance to clean and map messy inputs.", detail: "CSV, Excel, AI" },
        { title: "Admin review queues", desc: "Keep absences, shift changes, waitlists, swaps, special leave, and coverage gaps visible before closing a cycle.", detail: "Control room" },
      ],
    },
    teams: {
      title: "Built for teams that cannot miss a shift",
      subtitle: "Restaurants, security, operations, healthcare, factories, field teams, and command-style teams all need the same thing: coverage without chaos.",
      items: ["Restaurants", "Security teams", "Healthcare", "Factories", "Military-style units", "Field operations"],
    },
    workflow: {
      title: "From rules to a working schedule",
      steps: [
        { title: "Prepare", desc: "Add members, roles, shift templates, constraints, availability, and imported files." },
        { title: "Open", desc: "Choose automatic generation or manual self-service, then let members and admins work from the same truth." },
        { title: "Operate", desc: "Review exceptions, publish, notify, export closeout evidence, and improve the next cycle." },
      ],
    },
    trust: [
      { title: "Installable web app", desc: "Users can add Shifter to their home screen without waiting for app stores." },
      { title: "Secure by design", desc: "HTTPS, isolated workspaces, hashed passwords, controlled access, and customer-hosted options.", href: "/security" },
      { title: "Accessibility minded", desc: "Keyboard, contrast, localization, and assistive technology support are treated as product requirements.", href: "/accessibility" },
    ],
    contact: {
      title: "Need help choosing the right setup?",
      subtitle: "Tell us about your team size, constraints, and current scheduling pain. We will guide the rollout.",
      email: "Email support",
      demo: "Book a walkthrough",
    },
    faq: {
      title: "Questions before you switch",
      subtitle: "The practical details teams usually ask before replacing spreadsheets.",
      items: [
        { q: "Is manual self-service separate from automatic scheduling?", a: "Yes. Manual self-service is a second scheduling mode. Teams can let members pick from available shifts, while the automatic scheduler remains available for optimized rosters." },
        { q: "Do users need to download an app?", a: "No. Shifter works in the browser and can be installed as a PWA on phones and supported desktop browsers." },
        { q: "Can managers still edit the generated schedule?", a: "Yes. Generated schedules stay as drafts until admins review and publish them." },
        { q: "Can we import existing files?", a: "Yes. Shifter supports CSV and Excel imports, with AI-assisted cleanup planned for messy scan/import cases." },
        { q: "Is this only for one industry?", a: "No. The model fits any team with recurring shifts, coverage rules, qualifications, and fairness concerns." },
      ],
    },
    cta: {
      title: "Give the next schedule a cleaner starting point.",
      subtitle: "Create a workspace, invite the team, and publish from one place.",
      primary: "Create account",
      secondary: "I already have an account",
    },
    footer: { product: "Product", faq: "FAQ", signIn: "Sign in" },
  },
  he: {
    dir: "rtl",
    nav: {
      product: "מוצר",
      features: "יכולות",
      teams: "צוותים",
      faq: "שאלות",
      contact: "יצירת קשר",
      signIn: "כניסה",
      getStarted: "להתחיל בחינם",
      tagline: "סידור משמרות חכם",
      menu: "תפריט",
    },
    hero: {
      eyebrow: "סידור משמרות חכם לצוותים אמיתיים",
      title: "בונים סידור הוגן בלי לחיות בתוך אקסלים.",
      subtitle: "Shifter מחבר סידור אוטומטי, שירות עצמי ידני, החלפות, היעדרויות, ייבוא וגישה מהנייד בתוך שירות ווב אחד שכל הצוות יכול לפתוח מכל טלפון.",
      primary: "להתחיל בחינם",
      secondary: "כניסה",
      install: "ניתן להתקנה כ-PWA בטלפון ובדפדפני דסקטופ נתמכים",
    },
    finder: { title: "מצאו את התהליך שחשוב לכם", placeholder: "חיפוש ייבוא, החלפות, שירות עצמי, PWA, תמיכה...", empty: "אין התאמה מדויקת. נסו סידור, ייבוא, החלפה, נייד, אבטחה או תמיכה.", items: heFinder },
    proof: [
      { value: "ידני", label: "מצב שירות עצמי" },
      { value: "אוטומטי", label: "מצב סידור חכם" },
      { value: "24/7", label: "גישה מהנייד" },
      { value: "PWA", label: "אפליקציית ווב להתקנה" },
    ],
    preview: {
      workspace: "צוות צפון",
      productSubtitle: "סביבת ניהול",
      publishLabel: "פרסום",
      desktopTitle: "תפעול שירות עצמי",
      desktopSubtitle: "מנהלים את הסבב, עוקבים אחרי כיסוי ובודקים תורי החלטה.",
      scheduleView: "סידור",
      pickView: "בחירת משמרות",
      operationsView: "תפעול",
      slotsLabel: "משבצות",
      requestsLabel: "בקשות",
      coverageLabel: "כיסוי",
      openWindowLabel: "חלון פתוח",
      adminReviewLabel: "בדיקת מנהל",
      mobileHeader: "הסטטוס שלי",
      mobileSubtitle: "נבחרו 2/4 משמרות. מינימום: 3.",
      mobileTabs: ["סטטוס", "משמרות פנויות", "רשימת המתנה"],
      opsSnapshot: "תמונת מצב",
      live: "חי",
      metrics: [
        { value: "18", label: "משמרות" },
        { value: "7", label: "תפקידים" },
        { value: "3", label: "התראות" },
      ],
      shifts: [
        { time: "06:00", name: "הכנת בוקר", status: "פתוח", assignee: "2 מקומות פתוחים" },
        { time: "14:00", name: "שירות צהריים", status: "המתנה", assignee: "הצעה עומדת לפוג" },
        { time: "22:00", name: "סגירת ערב", status: "מאושר", assignee: "דנה לוי" },
      ],
      alerts: ["משמרות חסרות צריכות כיסוי", "2 דיווחי היעדרות ממתינים", "הצעת החלפה אחת ממתינה"],
    },
    features: {
      title: "שני מצבי סידור, תהליך עבודה אחד",
      subtitle: "משתמשים בסידור האוטומטי כשהחוקים מורכבים, או פותחים שירות עצמי ידני כשהצוות צריך לבחור מתוך משמרות זמינות.",
      items: [
        { title: "שירות עצמי ידני", desc: "חברי צוות בוחרים משמרות פתוחות, מצטרפים לרשימות המתנה, מבטלים לפי מדיניות, מדווחים היעדרות, מבקשים שינוי, מציעים החלפות ומבקשים חופש.", detail: "מצב חדש" },
        { title: "סידור אוטומטי", desc: "יוצרים סידור מאוזן לפי מנוחה, הכשרות, העדפות, זמינות וחוקי כיסוי.", detail: "CP-SAT" },
        { title: "ייבוא וסריקה", desc: "מכניסים אנשים ומשמרות מקבצים, ואז משתמשים בעזרת AI לניקוי ומיפוי קלטים מבולגנים.", detail: "CSV, Excel, AI" },
        { title: "תורי בדיקה למנהל", desc: "היעדרויות, בקשות שינוי, רשימות המתנה, החלפות, חופשות ופערי כיסוי נשארים גלויים לפני סגירת הסבב.", detail: "חדר בקרה" },
      ],
    },
    teams: {
      title: "נבנה לצוותים שלא יכולים לפספס משמרת",
      subtitle: "מסעדות, אבטחה, תפעול, רפואה, מפעלים, צוותי שטח וצוותים פיקודיים צריכים אותו דבר: כיסוי בלי כאוס.",
      items: ["מסעדות", "צוותי אבטחה", "רפואה", "מפעלים", "יחידות בסגנון צבאי", "תפעול שטח"],
    },
    workflow: {
      title: "מחוקים לסידור שעובד",
      steps: [
        { title: "מכינים", desc: "מוסיפים חברים, תפקידים, תבניות משמרת, אילוצים, זמינות וקבצי ייבוא." },
        { title: "פותחים", desc: "בוחרים יצירה אוטומטית או שירות עצמי ידני, ואז כולם עובדים מאותה אמת." },
        { title: "מתפעלים", desc: "בודקים חריגים, מפרסמים, שולחים עדכונים, מייצאים סיכום ומשפרים את הסבב הבא." },
      ],
    },
    trust: [
      { title: "אפליקציית ווב להתקנה", desc: "משתמשים יכולים להוסיף את Shifter למסך הבית בלי לחכות לחנויות אפליקציות." },
      { title: "אבטחה כברירת מחדל", desc: "HTTPS, סביבות עבודה מבודדות, סיסמאות מגובבות, הרשאות גישה ואפשרות לאירוח לקוח.", href: "/security" },
      { title: "נגישות כחלק מהמוצר", desc: "מקלדת, ניגודיות, לוקליזציה ותמיכה בטכנולוגיות מסייעות נחשבות דרישות מוצר.", href: "/accessibility" },
    ],
    contact: {
      title: "צריך עזרה לבחור את ההקמה הנכונה?",
      subtitle: "ספרו לנו על גודל הצוות, האילוצים וכאב הסידור הנוכחי. נעזור לבנות התחלה נכונה.",
      email: "שליחת מייל",
      demo: "תיאום שיחה",
    },
    faq: {
      title: "שאלות לפני שעוברים",
      subtitle: "הפרטים המעשיים שצוותים שואלים לפני שמחליפים אקסלים.",
      items: [
        { q: "השירות העצמי הידני נפרד מהסידור האוטומטי?", a: "כן. שירות עצמי ידני הוא מצב סידור שני. אפשר לתת לחברי צוות לבחור משמרות זמינות, והסידור האוטומטי עדיין נשאר זמין לרוסטרים אופטימליים." },
        { q: "צריך להוריד אפליקציה?", a: "לא. Shifter עובד בדפדפן וניתן להתקנה כ-PWA בטלפונים ובדפדפני דסקטופ נתמכים." },
        { q: "מנהל יכול לערוך את הסידור שנוצר?", a: "כן. סידור שנוצר נשאר טיוטה עד שמנהל בודק ומפרסם אותו." },
        { q: "אפשר לייבא קבצים קיימים?", a: "כן. Shifter תומך ב-CSV וב-Excel, ובהמשך AI יעזור לנקות קלטים מורכבים יותר." },
        { q: "זה מתאים רק לתחום אחד?", a: "לא. כל צוות עם משמרות, כיסוי, הכשרות והוגנות יכול להשתמש במודל." },
      ],
    },
    cta: {
      title: "תנו לסידור הבא נקודת פתיחה נקייה יותר.",
      subtitle: "יוצרים סביבת עבודה, מזמינים צוות ומפרסמים ממקום אחד.",
      primary: "יצירת חשבון",
      secondary: "כבר יש לי חשבון",
    },
    footer: { product: "מוצר", faq: "שאלות", signIn: "כניסה" },
  },
  ru: {
    dir: "ltr",
    nav: {
      product: "Продукт",
      features: "Возможности",
      teams: "Команды",
      faq: "FAQ",
      contact: "Контакты",
      signIn: "Войти",
      getStarted: "Начать бесплатно",
      tagline: "Умное планирование смен",
      menu: "Меню",
    },
    hero: {
      eyebrow: "Умное расписание смен для реальных команд",
      title: "Создавайте справедливые графики без жизни в таблицах.",
      subtitle: "Shifter объединяет автоматическое планирование, ручное самообслуживание, обмены, отсутствия, импорт и мобильный доступ в одном веб-сервисе.",
      primary: "Начать бесплатно",
      secondary: "Войти",
      install: "Можно установить как PWA на телефонах и поддерживаемых desktop-браузерах",
    },
    finder: { title: "Найдите нужный процесс", placeholder: "Поиск: импорт, обмены, самообслуживание, PWA, поддержка...", empty: "Точного совпадения нет. Попробуйте расписание, импорт, обмен, мобильный доступ, безопасность или поддержку.", items: ruFinder },
    proof: [
      { value: "Ручной", label: "режим самообслуживания" },
      { value: "Авто", label: "автоматический режим" },
      { value: "24/7", label: "мобильный доступ" },
      { value: "PWA", label: "устанавливаемое веб-приложение" },
    ],
    preview: {
      workspace: "Северная команда",
      productSubtitle: "Рабочее место администратора",
      publishLabel: "Опубликовать",
      desktopTitle: "Операции самообслуживания",
      desktopSubtitle: "Управляйте циклом, следите за покрытием и проверяйте очереди решений.",
      scheduleView: "Расписание",
      pickView: "Выбор смен",
      operationsView: "Операции",
      slotsLabel: "Слоты",
      requestsLabel: "Запросы",
      coverageLabel: "Покрытие",
      openWindowLabel: "Окно открыто",
      adminReviewLabel: "Проверка админом",
      mobileHeader: "Мой статус",
      mobileSubtitle: "Выбрано 2/4 смены. Минимум: 3.",
      mobileTabs: ["Статус", "Доступные смены", "Лист ожидания"],
      opsSnapshot: "Сводка операций",
      live: "Онлайн",
      metrics: [
        { value: "18", label: "смен" },
        { value: "7", label: "ролей" },
        { value: "3", label: "сигнала" },
      ],
      shifts: [
        { time: "06:00", name: "Утренняя подготовка", status: "Открыто", assignee: "2 свободных места" },
        { time: "14:00", name: "Обеденный сервис", status: "Лист ожидания", assignee: "Предложение скоро истечет" },
        { time: "22:00", name: "Закрывающая смена", status: "Одобрено", assignee: "Дана Леви" },
      ],
      alerts: ["Сменам не хватает покрытия", "2 отчета об отсутствии ждут решения", "Одно предложение обмена ожидает ответа"],
    },
    features: {
      title: "Два режима планирования, один рабочий процесс",
      subtitle: "Используйте автоматический планировщик для сложных правил или ручное самообслуживание, когда команда должна выбрать доступные смены.",
      items: [
        { title: "Ручное самообслуживание", desc: "Участники выбирают открытые смены, встают в лист ожидания, отменяют по правилам, сообщают об отсутствии, запрашивают изменения, предлагают обмены и отпуск.", detail: "Новый режим" },
        { title: "Автоматическое расписание", desc: "Создавайте сбалансированные графики с учетом отдыха, квалификаций, предпочтений, доступности и покрытия.", detail: "CP-SAT" },
        { title: "Импорт и сканирование", desc: "Загружайте людей и смены из файлов, затем используйте AI для очистки и сопоставления сложных данных.", detail: "CSV, Excel, AI" },
        { title: "Очереди проверки", desc: "Отсутствия, изменения, листы ожидания, обмены, отпуска и пробелы покрытия видны до закрытия цикла.", detail: "Центр контроля" },
      ],
    },
    teams: {
      title: "Для команд, которые не могут пропустить смену",
      subtitle: "Рестораны, охрана, операции, медицина, производство, полевые и командные группы нуждаются в одном: покрытии без хаоса.",
      items: ["Рестораны", "Охрана", "Медицина", "Производство", "Командные подразделения", "Полевые операции"],
    },
    workflow: {
      title: "От правил к рабочему расписанию",
      steps: [
        { title: "Подготовить", desc: "Добавьте участников, роли, шаблоны смен, ограничения, доступность и импортированные файлы." },
        { title: "Открыть", desc: "Выберите автоматическую генерацию или ручное самообслуживание, чтобы все работали с одной версией правды." },
        { title: "Управлять", desc: "Проверяйте исключения, публикуйте, уведомляйте, экспортируйте итоги и улучшайте следующий цикл." },
      ],
    },
    trust: [
      { title: "Устанавливаемое веб-приложение", desc: "Пользователи могут добавить Shifter на главный экран без ожидания app stores." },
      { title: "Безопасность по умолчанию", desc: "HTTPS, изолированные рабочие пространства, хешированные пароли, контроль доступа и клиентский хостинг.", href: "/security" },
      { title: "Доступность в продукте", desc: "Клавиатура, контраст, локализация и assistive technology учитываются как требования продукта.", href: "/accessibility" },
    ],
    contact: {
      title: "Нужна помощь с выбором настройки?",
      subtitle: "Расскажите о размере команды, ограничениях и текущей боли расписания. Мы поможем с внедрением.",
      email: "Написать в поддержку",
      demo: "Запланировать обзор",
    },
    faq: {
      title: "Вопросы перед переходом",
      subtitle: "Практические детали, о которых команды спрашивают перед заменой таблиц.",
      items: [
        { q: "Ручное самообслуживание отдельно от автоматического планирования?", a: "Да. Это второй режим планирования. Команда может выбирать доступные смены сама, а автоматический планировщик остается для оптимизированных графиков." },
        { q: "Нужно скачивать приложение?", a: "Нет. Shifter работает в браузере и устанавливается как PWA на телефонах и поддерживаемых desktop-браузерах." },
        { q: "Могут ли менеджеры редактировать созданное расписание?", a: "Да. Сгенерированное расписание остается черновиком, пока администратор его не проверит и не опубликует." },
        { q: "Можно импортировать существующие файлы?", a: "Да. Shifter поддерживает CSV и Excel, а AI-помощь для сложных импортов запланирована." },
        { q: "Это только для одной отрасли?", a: "Нет. Модель подходит любой команде со сменами, покрытием, квалификациями и требованиями справедливости." },
      ],
    },
    cta: {
      title: "Дайте следующему расписанию более чистый старт.",
      subtitle: "Создайте рабочее пространство, пригласите команду и публикуйте из одного места.",
      primary: "Создать аккаунт",
      secondary: "У меня уже есть аккаунт",
    },
    footer: { product: "Продукт", faq: "FAQ", signIn: "Войти" },
  },
};

export const LANDING_LEGAL_LINKS: Record<LandingLang, Array<{ href: string; label: string }>> = {
  en: [
    { href: "/terms", label: "Terms" },
    { href: "/privacy", label: "Privacy" },
    { href: "/security", label: "Security" },
    { href: "/accessibility", label: "Accessibility" },
    { href: "/subprocessors", label: "Subprocessors" },
  ],
  he: [
    { href: "/terms", label: "תנאי שימוש" },
    { href: "/privacy", label: "פרטיות" },
    { href: "/security", label: "אבטחה" },
    { href: "/accessibility", label: "נגישות" },
    { href: "/subprocessors", label: "מעבדי משנה" },
  ],
  ru: [
    { href: "/terms", label: "Условия" },
    { href: "/privacy", label: "Конфиденциальность" },
    { href: "/security", label: "Безопасность" },
    { href: "/accessibility", label: "Доступность" },
    { href: "/subprocessors", label: "Субпроцессоры" },
  ],
};
