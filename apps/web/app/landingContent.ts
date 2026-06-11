import type { Locale } from "@/lib/i18n/locales";

export type LandingLang = Locale;

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
  };
  hero: {
    eyebrow: string;
    title: string;
    subtitle: string;
    primary: string;
    secondary: string;
    install: string;
  };
  finder?: {
    title: string;
    placeholder: string;
    empty: string;
    items: Array<{ label: string; desc: string; href: string; keywords: string[] }>;
  };
  proof: Array<{ value: string; label: string }>;
  preview: {
    workspace: string;
    scheduleTitle: string;
    scheduleSubtitle: string;
    importLabel: string;
    aiLabel: string;
    phoneTitle: string;
    phoneSubtitle: string;
    shifts: Array<{ time: string; name: string; status: string }>;
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
    },
    hero: {
      eyebrow: "Smart shift scheduling for real teams",
      title: "Build fair schedules without living in spreadsheets.",
      subtitle:
        "Shifter turns people, roles, constraints, imports, and last-minute changes into a clear schedule your team can open from any phone.",
      primary: "Start free",
      secondary: "Sign in",
      install: "Installable on iPhone and Android as a PWA",
    },
    finder: {
      title: "Find the workflow you care about",
      placeholder: "Search imports, swaps, accessibility, PWA, support...",
      empty: "No exact match yet. Try schedule, import, swap, mobile, security, or support.",
      items: [
        { label: "Automatic scheduling", desc: "Generate fair drafts from roles, rules, rest, availability, and coverage needs.", href: "#features", keywords: ["schedule", "automatic", "solver", "constraints", "fair", "roster"] },
        { label: "Imports and scan flow", desc: "Bring spreadsheet data in and prepare messy files for AI-assisted cleanup.", href: "#features", keywords: ["import", "excel", "csv", "scan", "ai", "files"] },
        { label: "Swaps and absences", desc: "Let members report absence, request changes, and handle swaps with admin visibility.", href: "#features", keywords: ["swap", "absence", "change", "cannot attend", "self service"] },
        { label: "Installable mobile app", desc: "Use Shifter as a PWA on phones and supported desktop browsers.", href: "#faq", keywords: ["pwa", "install", "iphone", "android", "desktop", "offline"] },
        { label: "Accessibility", desc: "Read the accessibility statement and supported access practices.", href: "/accessibility", keywords: ["accessibility", "a11y", "contrast", "keyboard", "screen reader"] },
        { label: "Security and customer hosting", desc: "Review security posture, privacy controls, and deployment options.", href: "/security", keywords: ["security", "privacy", "hosting", "server", "cloudflare", "data"] },
        { label: "Talk to support", desc: "Ask about rollout, pricing, hosting, or how to model your scheduling rules.", href: "#contact", keywords: ["support", "contact", "demo", "help", "whatsapp", "sales"] },
      ],
    },
    proof: [
      { value: "90%", label: "less scheduling work" },
      { value: "24/7", label: "mobile schedule access" },
      { value: "0", label: "spreadsheet handoffs" },
      { value: "AI", label: "import and support ready" },
    ],
    preview: {
      workspace: "North Team",
      scheduleTitle: "June weekly schedule",
      scheduleSubtitle: "Balanced and ready to publish",
      importLabel: "Excel import mapped",
      aiLabel: "Assistant found 2 conflicts",
      phoneTitle: "My shifts",
      phoneSubtitle: "Available offline after install",
      shifts: [
        { time: "06:00", name: "Gate watch", status: "Confirmed" },
        { time: "14:00", name: "Control room", status: "Needs approval" },
        { time: "22:00", name: "Night patrol", status: "Covered" },
      ],
      alerts: ["Rest rule protected", "One swap pending", "3 qualified people available"],
    },
    features: {
      title: "Everything the scheduler and the team both need",
      subtitle: "A focused operating system for shift work, not another spreadsheet wrapper.",
      items: [
        {
          title: "Automatic scheduling",
          desc: "Generate balanced rosters while respecting rest, qualifications, preferences, and availability.",
          detail: "CP-SAT optimization",
        },
        {
          title: "Mobile self-service",
          desc: "Members see their shifts, updates, notifications, and offline schedule from the same web app.",
          detail: "PWA ready",
        },
        {
          title: "Imports and scan flow",
          desc: "Bring people and shift data from files, then use AI assistance to clean and map messy inputs.",
          detail: "CSV, Excel, AI",
        },
        {
          title: "Swaps, absences, and fixes",
          desc: "Handle sick days, vacations, replacements, and republishing without rebuilding the week by hand.",
          detail: "Change control",
        },
      ],
    },
    teams: {
      title: "Built for teams that cannot miss a shift",
      subtitle: "Security, operations, healthcare, restaurants, field teams, and command-style teams all need the same thing: coverage without chaos.",
      items: ["Security teams", "Restaurants", "Healthcare", "Factories", "Military-style units", "Field operations"],
    },
    workflow: {
      title: "From messy inputs to published schedule",
      steps: [
        { title: "Collect", desc: "Add members, roles, constraints, availability, and imported files." },
        { title: "Generate", desc: "Let Shifter create a fair draft and explain conflicts before publishing." },
        { title: "Operate", desc: "Publish, notify, swap, and adjust from desktop or mobile." },
      ],
    },
    trust: [
      { title: "Installable web app", desc: "Users can add Shifter to their home screen without waiting for app stores." },
      { title: "Secure by design", desc: "HTTPS, isolated workspaces, hashed passwords, and controlled access.", href: "/security" },
      { title: "Accessibility minded", desc: "Keyboard, contrast, localization, and assistive technology support are first-class concerns.", href: "/accessibility" },
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
        { q: "Do users need to download an app?", a: "No. Shifter works in the browser and can be installed as a PWA on iPhone and Android." },
        { q: "Will the PWA keep users logged in?", a: "On Chromium browsers, same-domain installs use the same origin storage. On iPhone, Apple may isolate the installed app, so the user may need to sign in once inside the PWA." },
        { q: "Can managers still edit the generated schedule?", a: "Yes. The generated schedule is a draft until you review and publish it." },
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
    },
    hero: {
      eyebrow: "סידור משמרות חכם לצוותים אמיתיים",
      title: "בונים סידור הוגן בלי לחיות בתוך אקסלים.",
      subtitle:
        "Shifter מחבר אנשים, תפקידים, אילוצים, יבוא קבצים ושינויים של הרגע האחרון לסידור ברור שכל הצוות פותח מכל טלפון.",
      primary: "להתחיל בחינם",
      secondary: "כניסה",
      install: "ניתן להתקנה באייפון ובאנדרואיד כ-PWA",
    },
    proof: [
      { value: "90%", label: "פחות עבודת סידור" },
      { value: "24/7", label: "גישה מהנייד" },
      { value: "0", label: "העברות באקסל" },
      { value: "AI", label: "מוכן ליבוא ותמיכה" },
    ],
    preview: {
      workspace: "צוות צפון",
      scheduleTitle: "סידור שבועי ליוני",
      scheduleSubtitle: "מאוזן ומוכן לפרסום",
      importLabel: "יבוא אקסל מופה",
      aiLabel: "העוזר מצא 2 התנגשויות",
      phoneTitle: "המשמרות שלי",
      phoneSubtitle: "זמין גם אחרי התקנה למסך הבית",
      shifts: [
        { time: "06:00", name: "שמירת שער", status: "מאושר" },
        { time: "14:00", name: "חדר בקרה", status: "דורש אישור" },
        { time: "22:00", name: "סיור לילה", status: "מכוסה" },
      ],
      alerts: ["מנוחת מינימום נשמרה", "החלפה אחת ממתינה", "3 אנשים מוסמכים זמינים"],
    },
    features: {
      title: "כל מה שמנהל הסידור והצוות צריכים",
      subtitle: "מערכת עבודה ממוקדת למשמרות, לא עוד עטיפה יפה לאקסל.",
      items: [
        {
          title: "סידור אוטומטי",
          desc: "יצירת סידור מאוזן לפי מנוחה, הכשרות, העדפות וזמינות.",
          detail: "אופטימיזציית CP-SAT",
        },
        {
          title: "שירות עצמי בנייד",
          desc: "חברי הצוות רואים משמרות, עדכונים, התראות וסידור אופליין מאותה אפליקציית ווב.",
          detail: "מוכן כ-PWA",
        },
        {
          title: "יבוא וסריקה",
          desc: "מכניסים אנשים ומשמרות מקבצים, ובהמשך AI יעזור לנקות ולמפות קלטים מבולגנים.",
          detail: "CSV, Excel, AI",
        },
        {
          title: "החלפות והיעדרויות",
          desc: "מטפלים במחלה, חופש, החלפות ופרסום מחדש בלי לבנות את כל השבוע מחדש.",
          detail: "ניהול שינויים",
        },
      ],
    },
    teams: {
      title: "נבנה לצוותים שלא יכולים לפספס משמרת",
      subtitle: "אבטחה, תפעול, רפואה, מסעדות, צוותי שטח וצוותים פיקודיים צריכים אותו דבר: כיסוי בלי כאוס.",
      items: ["צוותי אבטחה", "מסעדות", "רפואה", "מפעלים", "יחידות בסגנון צבאי", "תפעול שטח"],
    },
    workflow: {
      title: "מקלטים מבולגנים לסידור מפורסם",
      steps: [
        { title: "אוספים", desc: "מוסיפים חברים, תפקידים, אילוצים, זמינות וקבצי יבוא." },
        { title: "מייצרים", desc: "Shifter יוצר טיוטה הוגנת ומסביר התנגשויות לפני פרסום." },
        { title: "מתפעלים", desc: "מפרסמים, מעדכנים, מחליפים ומתקנים מהמחשב או מהנייד." },
      ],
    },
    trust: [
      { title: "אפליקציית ווב להתקנה", desc: "משתמשים יכולים להוסיף את Shifter למסך הבית בלי לחכות לחנויות אפליקציות." },
      { title: "אבטחה כברירת מחדל", desc: "HTTPS, סביבת עבודה מבודדת, סיסמאות מגובבות והרשאות גישה.", href: "/security" },
      { title: "נגישות כחלק מהמוצר", desc: "מקלדת, ניגודיות, שפות ותמיכה בטכנולוגיות מסייעות נמצאים בתכנון.", href: "/accessibility" },
    ],
    contact: {
      title: "צריך עזרה לבחור את ההקמה הנכונה?",
      subtitle: "ספרו לנו על גודל הצוות, האילוצים והכאב הנוכחי בסידור. נעזור לבנות התחלה נכונה.",
      email: "שליחת מייל",
      demo: "תיאום שיחה",
    },
    faq: {
      title: "שאלות לפני שעוברים",
      subtitle: "הפרטים המעשיים שצוותים שואלים לפני שמחליפים אקסלים.",
      items: [
        { q: "צריך להוריד אפליקציה?", a: "לא. Shifter עובד בדפדפן וניתן להתקנה כ-PWA באייפון ובאנדרואיד." },
        { q: "ה-PWA ישאיר משתמשים מחוברים?", a: "בדפדפני Chromium התקנה מאותו דומיין משתמשת באותו אחסון מקור. באייפון Apple יכולה לבודד את האפליקציה המותקנת, ולכן ייתכן שצריך להתחבר פעם אחת בתוך ה-PWA." },
        { q: "מנהל יכול לערוך את הסידור שנוצר?", a: "כן. הסידור הוא טיוטה עד שמאשרים ומפרסמים אותו." },
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
      product: "Product",
      features: "Features",
      teams: "Teams",
      faq: "FAQ",
      contact: "Contact",
      signIn: "Sign in",
      getStarted: "Start free",
    },
    hero: {
      eyebrow: "Smart shift scheduling for real teams",
      title: "Build fair schedules without living in spreadsheets.",
      subtitle:
        "Shifter turns people, roles, constraints, imports, and last-minute changes into a clear schedule your team can open from any phone.",
      primary: "Start free",
      secondary: "Sign in",
      install: "Installable on iPhone and Android as a PWA",
    },
    proof: [
      { value: "90%", label: "less scheduling work" },
      { value: "24/7", label: "mobile schedule access" },
      { value: "0", label: "spreadsheet handoffs" },
      { value: "AI", label: "import and support ready" },
    ],
    preview: {
      workspace: "North Team",
      scheduleTitle: "June weekly schedule",
      scheduleSubtitle: "Balanced and ready to publish",
      importLabel: "Excel import mapped",
      aiLabel: "Assistant found 2 conflicts",
      phoneTitle: "My shifts",
      phoneSubtitle: "Available offline after install",
      shifts: [
        { time: "06:00", name: "Gate watch", status: "Confirmed" },
        { time: "14:00", name: "Control room", status: "Needs approval" },
        { time: "22:00", name: "Night patrol", status: "Covered" },
      ],
      alerts: ["Rest rule protected", "One swap pending", "3 qualified people available"],
    },
    features: {
      title: "Everything the scheduler and the team both need",
      subtitle: "A focused operating system for shift work, not another spreadsheet wrapper.",
      items: [
        { title: "Automatic scheduling", desc: "Generate balanced rosters while respecting rest, qualifications, preferences, and availability.", detail: "CP-SAT optimization" },
        { title: "Mobile self-service", desc: "Members see their shifts, updates, notifications, and offline schedule from the same web app.", detail: "PWA ready" },
        { title: "Imports and scan flow", desc: "Bring people and shift data from files, then use AI assistance to clean and map messy inputs.", detail: "CSV, Excel, AI" },
        { title: "Swaps, absences, and fixes", desc: "Handle sick days, vacations, replacements, and republishing without rebuilding the week by hand.", detail: "Change control" },
      ],
    },
    teams: {
      title: "Built for teams that cannot miss a shift",
      subtitle: "Security, operations, healthcare, restaurants, field teams, and command-style teams all need the same thing: coverage without chaos.",
      items: ["Security teams", "Restaurants", "Healthcare", "Factories", "Military-style units", "Field operations"],
    },
    workflow: {
      title: "From messy inputs to published schedule",
      steps: [
        { title: "Collect", desc: "Add members, roles, constraints, availability, and imported files." },
        { title: "Generate", desc: "Let Shifter create a fair draft and explain conflicts before publishing." },
        { title: "Operate", desc: "Publish, notify, swap, and adjust from desktop or mobile." },
      ],
    },
    trust: [
      { title: "Installable web app", desc: "Users can add Shifter to their home screen without waiting for app stores." },
      { title: "Secure by design", desc: "HTTPS, isolated workspaces, hashed passwords, and controlled access.", href: "/security" },
      { title: "Accessibility minded", desc: "Keyboard, contrast, localization, and assistive technology support are first-class concerns.", href: "/accessibility" },
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
        { q: "Do users need to download an app?", a: "No. Shifter works in the browser and can be installed as a PWA on iPhone and Android." },
        { q: "Will the PWA keep users logged in?", a: "On Chromium browsers, same-domain installs use the same origin storage. On iPhone, Apple may isolate the installed app, so the user may need to sign in once inside the PWA." },
        { q: "Can managers still edit the generated schedule?", a: "Yes. The generated schedule is a draft until you review and publish it." },
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
    { href: "/terms", label: "Terms" },
    { href: "/privacy", label: "Privacy" },
    { href: "/security", label: "Security" },
    { href: "/accessibility", label: "Accessibility" },
    { href: "/subprocessors", label: "Subprocessors" },
  ],
};
