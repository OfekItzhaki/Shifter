import type { Locale } from "@/i18n/request";

export type LegalPageKey = "terms" | "privacy" | "subprocessors" | "dpa";

export interface LegalSection {
  title: string;
  paragraphs?: string[];
  bullets?: string[];
}

export interface LegalPageContent {
  eyebrow: string;
  title: string;
  lastUpdatedLabel: string;
  calloutTitle: string;
  calloutBody: string;
  sections: LegalSection[];
  relatedLinkLabel: string;
}

export const LEGAL_LAST_UPDATED = "June 4, 2026";
export const LEGAL_CONTACT_EMAIL = process.env.NEXT_PUBLIC_LEGAL_EMAIL ?? "support@ofeklabs.com";

export function legalDir(locale: string): "rtl" | "ltr" {
  return locale === "he" ? "rtl" : "ltr";
}

export function getLegalContent(page: LegalPageKey, locale: string): LegalPageContent {
  const safeLocale = (locale in legalContent ? locale : "en") as Locale;
  return legalContent[safeLocale][page];
}

export const legalContent: Record<Locale, Record<LegalPageKey, LegalPageContent>> = {
  en: {
    terms: {
      eyebrow: "Legal",
      title: "Terms of Service",
      lastUpdatedLabel: "Last updated",
      calloutTitle: "Important scheduling notice",
      calloutBody:
        "Shifter helps teams create and manage schedules, but it does not replace human review. You are responsible for verifying staffing, legal requirements, safety requirements, rest rules, emergency coverage, and operational fitness before using or publishing a schedule.",
      relatedLinkLabel: "Privacy Policy",
      sections: [
        {
          title: "1. Who we are and acceptance of these Terms",
          paragraphs: [
            "These Terms of Service govern your access to and use of Shifter, a shift scheduling and workforce coordination service operated by Ofek Labs. By accessing Shifter, creating an account, joining a workspace, or purchasing a subscription, you agree to these Terms.",
            "If you use Shifter on behalf of an organization, you represent that you have authority to bind that organization to these Terms.",
          ],
        },
        {
          title: "2. The service",
          paragraphs: [
            "Shifter provides tools for workspace management, team and group management, shift planning, automated scheduling, availability and constraint collection, self-service scheduling, notifications, exports, billing management, analytics for operators, and related support features.",
            "We may add, change, suspend, or remove features from time to time.",
          ],
        },
        {
          title: "3. Accounts, administrators, and users",
          bullets: [
            "You must provide accurate account information and keep it current.",
            "You are responsible for credentials, passkeys, devices, and activity under your account.",
            "Workspace administrators control workspace settings, members, roles, schedules, permissions, and billing.",
            "Administrators are responsible for obtaining any notices or permissions required before adding team-member data to Shifter.",
            "The service is intended for users who are at least 16 years old.",
          ],
        },
        {
          title: "4. Acceptable use",
          paragraphs: ["You may not use Shifter to:"],
          bullets: [
            "break applicable laws, employment rules, privacy rules, safety rules, or third-party rights;",
            "upload unlawful, harmful, discriminatory, abusive, or infringing content;",
            "access another user, workspace, tenant, system, or database without permission;",
            "interfere with, reverse engineer, scrape, overload, or disrupt the service;",
            "remove security controls, rate limits, audit trails, or access controls;",
            "make fully automated operational decisions without appropriate human review.",
          ],
        },
        {
          title: "5. Scheduling results and AI-assisted features",
          paragraphs: [
            "Shifter may generate recommendations, drafts, alerts, statistics, exports, imports, summaries, AI-assisted parsing, and automatic schedules. These outputs can be incomplete, inaccurate, delayed, or unsuitable for a specific operational environment.",
            "You must review all generated or AI-assisted output before relying on it. We are not responsible for missed shifts, understaffing, overstaffing, labor-law violations, fatigue issues, safety incidents, loss of revenue, discipline issues, or other consequences arising from schedules or recommendations.",
          ],
        },
        {
          title: "6. Customer data",
          paragraphs: [
            "You retain ownership of names, schedules, availability, constraints, files, notes, feedback, and other content submitted to Shifter. You grant us a limited license to host, process, transmit, display, back up, and use that data as needed to provide, secure, support, and improve the service.",
            "You represent that you have the rights and permissions needed to submit data to Shifter.",
          ],
        },
        {
          title: "7. Subscriptions, billing, taxes, and refunds",
          paragraphs: [
            "Paid plans, trials, renewals, upgrades, downgrades, cancellations, invoices, taxes, and payment processing may be managed through LemonSqueezy or another payment processor. Prices, plan limits, trial periods, and included features are shown in the product or checkout flow.",
            "Unless stated otherwise, subscriptions renew automatically until canceled. Cancellation generally takes effect at the end of the current billing period. Except where required by law or expressly stated in checkout, payments are non-refundable and we do not provide credits for partial periods or unused features.",
          ],
        },
        {
          title: "8. Third-party services",
          paragraphs: [
            "Shifter may use third-party providers for payments, email, WhatsApp or SMS delivery, error monitoring, analytics, file storage, hosting, databases, backups, AI assistance, and infrastructure. Third-party services are governed by their own terms and policies.",
            "We are not responsible for third-party outages, policy changes, or processing outside our control.",
          ],
        },
        {
          title: "9. Intellectual property",
          paragraphs: [
            "Shifter, including its software, design, workflows, branding, logos, documentation, and algorithms, is owned by Ofek Labs or its licensors. These Terms do not transfer any intellectual-property rights to you.",
            "You may not copy, modify, distribute, sell, lease, or create derivative works from the service except as expressly permitted by us in writing.",
          ],
        },
        {
          title: "10. Privacy, security, and availability",
          paragraphs: [
            "Our Privacy Policy explains how we collect, use, store, and share personal information. We use reasonable safeguards, but no service can be guaranteed to be perfectly secure or continuously available.",
            "The service may be unavailable because of maintenance, deployments, security events, third-party outages, internet issues, force majeure events, or other causes.",
          ],
        },
        {
          title: "11. Suspension and termination",
          paragraphs: [
            "You may stop using Shifter at any time. We may suspend or terminate access if you violate these Terms, create security or legal risk, fail to pay amounts due, misuse the service, or if we are required to do so by law.",
            "After termination, we may retain data as described in the Privacy Policy and as required for legal, security, backup, billing, and audit purposes.",
          ],
        },
        {
          title: "12. Disclaimers and limitation of liability",
          paragraphs: [
            "To the fullest extent permitted by law, Shifter is provided \"as is\" and \"as available\" without warranties of any kind. We disclaim warranties of merchantability, fitness for a particular purpose, non-infringement, accuracy, availability, and error-free operation.",
            "To the fullest extent permitted by law, Ofek Labs will not be liable for indirect, incidental, special, consequential, exemplary, or punitive damages, lost profits, lost revenue, lost data, business interruption, staffing failures, schedule errors, or operational losses. Our total liability for any claim is limited to the amount you paid for Shifter during the three months before the event giving rise to the claim, or USD 100 if no amount was paid.",
          ],
        },
        {
          title: "13. Indemnity",
          paragraphs: [
            "You agree to defend, indemnify, and hold harmless Ofek Labs from claims, damages, liabilities, losses, and expenses arising from your use of Shifter, your data, your violation of these Terms, your violation of law, or your infringement of third-party rights.",
          ],
        },
        {
          title: "14. Governing law, changes, and contact",
          paragraphs: [
            "These Terms are governed by the laws of the State of Israel, without regard to conflict-of-law rules. The competent courts in Israel will have exclusive jurisdiction unless applicable law requires otherwise.",
            "We may update these Terms from time to time. If changes are material, we will provide reasonable notice in the application, by email, or by another appropriate method. Continued use after changes take effect means you accept the updated Terms.",
            `For questions about these Terms, contact ${LEGAL_CONTACT_EMAIL}.`,
          ],
        },
      ],
    },
    privacy: {
      eyebrow: "Legal",
      title: "Privacy Policy",
      lastUpdatedLabel: "Last updated",
      calloutTitle: "Short version",
      calloutBody:
        "Shifter uses your data to operate scheduling workspaces, secure accounts, send notifications, process billing, support the product, and improve reliability. We do not sell personal information.",
      relatedLinkLabel: "Terms of Service",
      sections: [
        {
          title: "1. Who controls your data and scope",
          paragraphs: [
            `Ofek Labs operates Shifter and is responsible for the personal information described in this Privacy Policy. For privacy questions or requests, contact ${LEGAL_CONTACT_EMAIL}.`,
            "This Policy applies to Shifter websites, web applications, APIs, scheduling tools, billing flows, support channels, feedback tools, and related services.",
          ],
        },
        {
          title: "2. Personal information we collect",
          bullets: [
            "Account data: name, display name, email, phone, password hash, preferred language, timezone, and authentication state.",
            "Profile data: profile image, birthday if provided, role information, workspace membership, and permission level.",
            "Scheduling data: groups, tasks, shifts, assignments, availability, constraints, qualifications, home-leave settings, waitlists, swaps, requests, alerts, and schedule history.",
            "Workspace data: workspace settings, members, invitations, ownership, billing permissions, audit activity, and configuration choices.",
            "Billing data: plan, subscription status, checkout metadata, renewal/cancellation state, LemonSqueezy identifiers, and transaction-related webhook data. We do not store full card numbers.",
            "Communications: support requests, feedback, bug reports, notification preferences, email delivery events, and WhatsApp/SMS delivery metadata where configured.",
            "Files and exports: profile images, imported scheduling data, generated PDFs/CSVs, and uploaded or generated files.",
            "Technical and analytics data: IP address, browser data, logs, cookies, local storage identifiers, session data, API usage, errors, diagnostics, product events, and session replay when enabled.",
          ],
        },
        {
          title: "3. How we collect information",
          bullets: [
            "directly from you when you register, update your profile, submit forms, upload files, or contact us;",
            "from workspace administrators who add or invite users and configure schedules;",
            "automatically through the application, API, logs, cookies, local storage, analytics, and error-monitoring tools;",
            "from payment, email, messaging, hosting, storage, and AI providers that support the service.",
          ],
        },
        {
          title: "4. How we use information",
          bullets: [
            "create accounts, authenticate users, manage sessions, and secure access;",
            "operate workspaces, groups, schedules, assignments, self-service shifts, notifications, and exports;",
            "process subscriptions, trials, billing changes, renewals, cancellations, and invoices;",
            "send transactional messages such as verification, password reset, invitations, schedule updates, and recall notices;",
            "provide support, investigate bugs, monitor reliability, detect abuse, prevent fraud, and maintain audit logs;",
            "analyze and improve usability, performance, feature quality, and security;",
            "comply with legal, tax, accounting, billing, security, and regulatory obligations.",
          ],
        },
        {
          title: "5. Legal bases for processing",
          paragraphs: [
            "Where a legal basis is required, we process personal information based on contract performance, legitimate interests, consent, legal obligations, and protection of rights and security. You may withdraw consent where processing depends on consent, but this will not affect processing that happened before withdrawal.",
          ],
        },
        {
          title: "6. Third-party processors and providers",
          bullets: [
            "LemonSqueezy: checkout, payment processing, invoices, tax handling, and subscription management.",
            "SendGrid: transactional email delivery.",
            "Twilio: WhatsApp or SMS notification delivery where configured.",
            "PostHog: product analytics, page events, usage events, and session replay when enabled in production.",
            "Sentry: error monitoring, performance traces, diagnostic context, and limited session replay for debugging.",
            "Hosting, database, and storage providers: hosting, PostgreSQL databases, backups, local or S3-compatible file storage, and infrastructure operations.",
            "AI providers: AI-assisted parsing, import, summary, or recommendation features where enabled.",
          ],
        },
        {
          title: "7. No sale of personal information",
          paragraphs: [
            "We do not sell personal information. We do not share personal information for third-party advertising networks. If that changes, we will update this Policy and provide any required choices or notices.",
          ],
        },
        {
          title: "8. Cookies, local storage, and similar technologies",
          paragraphs: ["Shifter uses cookies, local storage, and similar technologies for:"],
          bullets: [
            "authentication, token state, session continuity, and auth guards;",
            "language, theme, timezone, offline cache, and app preferences;",
            "security, abuse prevention, diagnostics, reliability, and product analytics when configured.",
          ],
        },
        {
          title: "9. Security and retention",
          paragraphs: [
            "We use safeguards such as HTTPS/TLS, password hashing, role and permission checks, workspace isolation, audit logs, production error handling, restricted administrative access, and backups. No security system is perfect.",
            "We keep personal information as long as needed to provide Shifter, comply with legal obligations, resolve disputes, maintain security, prevent abuse, and enforce agreements. Deleted account personal data is generally deleted or anonymized within 30 days where technically and legally feasible. Billing records, security logs, audit records, and backups may be retained longer where needed.",
          ],
        },
        {
          title: "10. Your choices and rights",
          paragraphs: [
            "Depending on your location and relationship to a workspace, you may have rights to access, correct, export, delete, restrict, or object to processing of personal information. Some requests may need to be handled by your workspace administrator if they control the workspace.",
            `To exercise rights, use in-app controls where available or contact ${LEGAL_CONTACT_EMAIL}.`,
          ],
        },
        {
          title: "11. Workspace administrators",
          paragraphs: [
            "Workspace administrators may access and manage data submitted by members of their workspace, including schedule data, availability, constraints, assignments, profile details, and activity needed to operate the workspace. If your organization uses Shifter, its internal policies may also apply.",
          ],
        },
        {
          title: "12. International users",
          paragraphs: [
            "Shifter may process and store information in Israel, the United States, the European Economic Area, or other locations where we or our providers operate. By using Shifter, you understand that information may be transferred to countries with privacy laws different from those in your jurisdiction.",
          ],
        },
        {
          title: "13. Children, Israeli privacy law, GDPR, and changes",
          paragraphs: [
            "Shifter is not intended for children under 16, and we do not knowingly collect personal information from children under 16.",
            "We aim to handle personal information consistently with applicable Israeli privacy requirements and, where applicable, GDPR principles for users in the European Economic Area or United Kingdom.",
            "We may update this Privacy Policy from time to time. If changes are material, we will provide reasonable notice in the application, by email, or by another appropriate method.",
          ],
        },
      ],
    },
    subprocessors: {
      eyebrow: "Legal",
      title: "Subprocessors",
      lastUpdatedLabel: "Last updated",
      calloutTitle: "What this page covers",
      calloutBody:
        "This page lists third-party providers that may process personal information or customer data when Shifter is configured to use them. Availability may vary by environment, plan, and feature configuration.",
      relatedLinkLabel: "Privacy Policy",
      sections: [
        {
          title: "1. Payment and subscription processing",
          bullets: [
            "LemonSqueezy: checkout, payment processing, tax handling, invoices, subscription status, customer identifiers, and billing webhooks.",
          ],
        },
        {
          title: "2. Communications",
          bullets: [
            "SendGrid: transactional email delivery such as verification, password reset, invitations, schedule updates, and service notices.",
            "Twilio: WhatsApp or SMS delivery for operational notifications where messaging is configured.",
          ],
        },
        {
          title: "3. Analytics and reliability",
          bullets: [
            "PostHog: product analytics, page events, usage events, and session diagnostics when analytics consent is granted and production analytics is configured.",
            "Sentry: error monitoring, performance diagnostics, and limited replay data when consent and production configuration allow it.",
          ],
        },
        {
          title: "4. Infrastructure and storage",
          bullets: [
            "Hosting and database providers: application hosting, API hosting, PostgreSQL databases, network services, backups, and operational logs.",
            "Local or S3-compatible storage providers: profile images, uploads, exports, generated files, and related backups.",
          ],
        },
        {
          title: "5. AI-assisted features",
          bullets: [
            "AI model providers: parsing, importing, summarizing, recommending, or assisting with scheduling-related content where AI features are enabled.",
          ],
        },
        {
          title: "6. Updates and questions",
          paragraphs: [
            "We may update this page when providers are added, removed, replaced, or materially changed.",
            `For questions about subprocessors, contact ${LEGAL_CONTACT_EMAIL}.`,
          ],
        },
      ],
    },
    dpa: {
      eyebrow: "Legal",
      title: "Data Processing Addendum",
      lastUpdatedLabel: "Last updated",
      calloutTitle: "How to use this DPA",
      calloutBody:
        "This Data Processing Addendum is intended for organizations that use Shifter to manage team-member or workforce data. It explains controller/processor roles and key data-processing commitments. For signed enterprise terms, contact Ofek Labs.",
      relatedLinkLabel: "Privacy Policy",
      sections: [
        {
          title: "1. Parties and scope",
          paragraphs: [
            "This Data Processing Addendum (DPA) supplements the Terms of Service and Privacy Policy when a customer uses Shifter to process personal information on behalf of a workspace, employer, team, unit, or organization.",
            "The customer is generally the controller of Customer Data, and Ofek Labs is generally the processor or service provider that processes Customer Data to provide Shifter, unless applicable law or a specific written agreement states otherwise.",
          ],
        },
        {
          title: "2. Processing instructions",
          paragraphs: [
            "Ofek Labs will process Customer Data only to provide, secure, support, maintain, and improve Shifter; comply with documented customer instructions; comply with law; and protect rights, safety, and security.",
            "The Terms, Privacy Policy, product settings, administrator actions, support requests, and this DPA are documented instructions for processing.",
          ],
        },
        {
          title: "3. Categories of data subjects and personal data",
          bullets: [
            "Data subjects may include workspace owners, administrators, employees, volunteers, contractors, team members, invitees, support contacts, and end users.",
            "Personal data may include account data, contact details, profile data, roles, permissions, scheduling data, availability, constraints, assignments, messages, files, billing metadata, technical logs, and support communications.",
            "Customers should not submit sensitive personal data unless necessary for their scheduling use case and legally permitted.",
          ],
        },
        {
          title: "4. Customer responsibilities",
          bullets: [
            "Customers are responsible for having a lawful basis to collect and process team-member data in Shifter.",
            "Customers are responsible for providing required notices to users and team members.",
            "Customers must configure roles and permissions appropriately and remove access when no longer needed.",
            "Customers must avoid submitting unnecessary sensitive information.",
          ],
        },
        {
          title: "5. Confidentiality and personnel",
          paragraphs: [
            "Ofek Labs restricts access to Customer Data to personnel and service providers who need access to operate, secure, support, or improve Shifter. Personnel with access to Customer Data are expected to protect it and use it only for authorized purposes.",
          ],
        },
        {
          title: "6. Security measures",
          bullets: [
            "HTTPS/TLS for data in transit.",
            "Password hashing and authentication controls.",
            "Role and permission checks.",
            "Workspace isolation and tenant-aware data access controls.",
            "Production error handling and security-conscious logging.",
            "Backups, monitoring, and operational controls appropriate to the service.",
          ],
        },
        {
          title: "7. Subprocessors",
          paragraphs: [
            "Ofek Labs may use subprocessors to provide payments, email, messaging, analytics, error monitoring, hosting, storage, AI assistance, and infrastructure. The current public list is available on the Subprocessors page.",
            "We remain responsible for subprocessors we engage to process Customer Data on our behalf, subject to the limits in the Terms.",
          ],
        },
        {
          title: "8. Assistance with rights requests",
          paragraphs: [
            "Where legally required and reasonably possible, Ofek Labs will assist customers with data subject requests through product functionality, exports, deletion workflows, or support. Customers remain responsible for responding to requests where they are the controller.",
          ],
        },
        {
          title: "9. Security incidents",
          paragraphs: [
            "If Ofek Labs becomes aware of a confirmed security incident involving Customer Data, we will notify affected customers without undue delay, provide information reasonably available to us, and take reasonable steps to contain, investigate, and remediate the incident.",
          ],
        },
        {
          title: "10. Return, deletion, and audit",
          paragraphs: [
            "Upon termination or deletion, Customer Data will be deleted or anonymized according to the Privacy Policy, product behavior, backup cycles, and legal retention obligations.",
            "Customers may request reasonable information about our security and privacy practices. Any audit right must be exercised in a way that protects other customers, security, confidential information, and service reliability.",
          ],
        },
        {
          title: "11. International transfers",
          paragraphs: [
            "Customer Data may be processed in Israel, the United States, the European Economic Area, and other locations where Ofek Labs or subprocessors operate. Where required, customers and Ofek Labs will rely on appropriate transfer mechanisms.",
          ],
        },
        {
          title: "12. Contact",
          paragraphs: [
            `For DPA questions or signed enterprise paperwork, contact ${LEGAL_CONTACT_EMAIL}.`,
          ],
        },
      ],
    },
  },
  he: {
    terms: {
      eyebrow: "משפטי",
      title: "תנאי שימוש",
      lastUpdatedLabel: "עדכון אחרון",
      calloutTitle: "הודעה חשובה לגבי סידורים",
      calloutBody:
        "Shifter מסייע ביצירה וניהול של סידורי עבודה, אך אינו מחליף בדיקה אנושית. באחריותך לוודא כוח אדם, דרישות חוק, בטיחות, מנוחה, כיסוי חירום והתאמה תפעולית לפני שימוש או פרסום סידור.",
      relatedLinkLabel: "מדיניות פרטיות",
      sections: [
        {
          title: "1. מי אנחנו והסכמה לתנאים",
          paragraphs: [
            "תנאי שימוש אלה מסדירים את הגישה והשימוש ב-Shifter, שירות לניהול משמרות ותיאום כוח אדם המופעל על ידי Ofek Labs. שימוש בשירות, יצירת חשבון, הצטרפות למרחב עבודה או רכישת מנוי מהווים הסכמה לתנאים אלה.",
            "אם אתה משתמש בשירות בשם ארגון, אתה מצהיר שיש לך סמכות לחייב את הארגון לתנאים אלה.",
          ],
        },
        {
          title: "2. השירות",
          paragraphs: [
            "Shifter מספק כלים לניהול מרחבי עבודה, קבוצות, תכנון משמרות, סידור אוטומטי, איסוף זמינות ואילוצים, בחירת משמרות עצמאית, התראות, יצוא נתונים, חיוב, נתוני שימוש למנהלים ותמיכה.",
            "אנו עשויים להוסיף, לשנות, להשעות או להסיר תכונות מעת לעת.",
          ],
        },
        {
          title: "3. חשבונות, מנהלים ומשתמשים",
          bullets: [
            "עליך למסור פרטים מדויקים ולעדכן אותם בעת הצורך.",
            "אתה אחראי לסיסמאות, מפתחות גישה, מכשירים וכל פעילות בחשבונך.",
            "מנהלי מרחב שולטים בהגדרות, חברים, תפקידים, סידורים, הרשאות וחיוב.",
            "מנהלים אחראים לקבל כל הודעה או הסכמה נדרשת לפני הוספת נתוני חברי צוות.",
            "השירות מיועד למשתמשים בני 16 ומעלה.",
          ],
        },
        {
          title: "4. שימוש מותר",
          paragraphs: ["אין להשתמש ב-Shifter כדי:"],
          bullets: [
            "להפר חוק, דיני עבודה, כללי פרטיות, בטיחות או זכויות צד שלישי;",
            "להעלות תוכן בלתי חוקי, מזיק, מפלה, פוגעני או מפר זכויות;",
            "לגשת ללא הרשאה למשתמש, מרחב, מערכת או מסד נתונים;",
            "לשבש, להעמיס, לגרד, להנדס לאחור או לפגוע בשירות;",
            "לעקוף בקרות אבטחה, מגבלות קצב, יומני ביקורת או הרשאות;",
            "לקבל החלטות תפעוליות אוטומטיות לחלוטין ללא בדיקה אנושית מתאימה.",
          ],
        },
        {
          title: "5. תוצרי סידור ותכונות AI",
          paragraphs: [
            "Shifter עשוי ליצור המלצות, טיוטות, התראות, סטטיסטיקות, יצוא, יבוא, סיכומים, ניתוח בעזרת AI וסידורים אוטומטיים. תוצרים אלה עלולים להיות חלקיים, שגויים, מאוחרים או לא מתאימים לסביבה תפעולית מסוימת.",
            "עליך לבדוק כל תוצר אוטומטי או מבוסס AI לפני הסתמכות עליו. איננו אחראים למשמרות שהוחמצו, חוסר או עודף כוח אדם, הפרות חוקי עבודה, עייפות, אירועי בטיחות, אובדן הכנסות או נזקים תפעוליים אחרים.",
          ],
        },
        {
          title: "6. נתוני לקוח",
          paragraphs: [
            "הבעלות בשמות, סידורים, זמינות, אילוצים, קבצים, הערות, משוב ותוכן אחר שהוזן ל-Shifter נשארת שלך. אתה מעניק לנו רישיון מוגבל לאחסן, לעבד, להעביר, להציג, לגבות ולהשתמש בנתונים ככל שנדרש להפעלה, אבטחה, תמיכה ושיפור השירות.",
            "אתה מצהיר שיש לך את הזכויות וההרשאות הדרושות למסירת הנתונים לשירות.",
          ],
        },
        {
          title: "7. מנויים, חיוב, מסים והחזרים",
          paragraphs: [
            "תוכניות בתשלום, ניסיונות, חידושים, שדרוגים, ביטולים, חשבוניות, מסים ותשלומים עשויים להתבצע דרך LemonSqueezy או ספק תשלום אחר. מחירים, מגבלות תוכנית, תקופת ניסיון ותכונות כלולות מוצגים במוצר או בתהליך התשלום.",
            "אלא אם צוין אחרת, מנויים מתחדשים אוטומטית עד ביטול. ביטול נכנס בדרך כלל לתוקף בסוף תקופת החיוב. למעט כנדרש בחוק או כפי שצוין בתשלום, תשלומים אינם ניתנים להחזר ואין זיכוי עבור תקופות חלקיות או תכונות שלא נוצלו.",
          ],
        },
        {
          title: "8. שירותי צד שלישי",
          paragraphs: [
            "Shifter עשוי להשתמש בספקים חיצוניים לתשלומים, אימייל, WhatsApp או SMS, ניטור שגיאות, אנליטיקה, אחסון קבצים, אירוח, מסדי נתונים, גיבויים, AI ותשתית. שירותים אלה כפופים לתנאים ולמדיניות שלהם.",
            "איננו אחראים להשבתות, שינויי מדיניות או עיבוד שאינם בשליטתנו.",
          ],
        },
        {
          title: "9. קניין רוחני",
          paragraphs: [
            "Shifter, לרבות התוכנה, העיצוב, התהליכים, המותג, הלוגואים, התיעוד והאלגוריתמים, שייכים ל-Ofek Labs או למעניקי הרישיונות שלה. תנאים אלה אינם מעבירים אליך זכויות קניין רוחני.",
            "אין להעתיק, לשנות, להפיץ, למכור, להשכיר או ליצור יצירות נגזרות מהשירות ללא אישור מפורש בכתב.",
          ],
        },
        {
          title: "10. פרטיות, אבטחה וזמינות",
          paragraphs: [
            "מדיניות הפרטיות שלנו מסבירה כיצד אנו אוספים, משתמשים, שומרים ומשתפים מידע אישי. אנו משתמשים באמצעי הגנה סבירים, אך אין שירות שמובטח להיות מאובטח או זמין ללא הפסקה.",
            "השירות עשוי להיות לא זמין עקב תחזוקה, פריסות, אירועי אבטחה, תקלות צד שלישי, בעיות אינטרנט, כוח עליון או סיבות אחרות.",
          ],
        },
        {
          title: "11. השעיה וסיום",
          paragraphs: [
            "ניתן להפסיק להשתמש בשירות בכל עת. אנו רשאים להשעות או לסיים גישה אם הופרו התנאים, נוצר סיכון אבטחה או משפטי, לא שולם חוב, נעשה שימוש לרעה או אם נדרש לפי חוק.",
            "לאחר סיום, אנו עשויים לשמור נתונים כפי שמתואר במדיניות הפרטיות ולפי הצורך לצרכים משפטיים, אבטחה, גיבוי, חיוב וביקורת.",
          ],
        },
        {
          title: "12. כתב ויתור והגבלת אחריות",
          paragraphs: [
            "במידה המרבית המותרת בחוק, Shifter מסופק כפי שהוא וכפי שהוא זמין, ללא אחריות מכל סוג. אנו מסירים אחריות לסחירות, התאמה למטרה מסוימת, אי-הפרה, דיוק, זמינות או פעולה ללא שגיאות.",
            "במידה המרבית המותרת בחוק, Ofek Labs לא תהיה אחראית לנזקים עקיפים, מיוחדים, תוצאתיים, עונשיים, אובדן רווחים, אובדן נתונים, השבתת עסק, כשלים בכוח אדם, שגיאות סידור או הפסדים תפעוליים. סך אחריותנו מוגבל לסכום ששולם עבור Shifter בשלושת החודשים שקדמו לאירוע, או 100 דולר אם לא שולם סכום.",
          ],
        },
        {
          title: "13. שיפוי",
          paragraphs: [
            "אתה מסכים להגן, לשפות ולפטור את Ofek Labs מתביעות, נזקים, התחייבויות, הפסדים והוצאות הנובעים מהשימוש שלך ב-Shifter, מהנתונים שלך, מהפרת תנאים אלה, מהפרת חוק או מפגיעה בזכויות צד שלישי.",
          ],
        },
        {
          title: "14. דין חל, שינויים ויצירת קשר",
          paragraphs: [
            "תנאים אלה כפופים לחוקי מדינת ישראל, ללא כללי ברירת דין. לבתי המשפט המוסמכים בישראל תהיה סמכות ייחודית, אלא אם חוק מחייב אחרת.",
            "אנו עשויים לעדכן תנאים אלה מעת לעת. אם השינויים מהותיים, נמסור הודעה סבירה באפליקציה, באימייל או בדרך מתאימה אחרת. המשך שימוש לאחר כניסת השינויים לתוקף מהווה הסכמה לתנאים המעודכנים.",
            `לשאלות לגבי תנאים אלה ניתן לפנות אל ${LEGAL_CONTACT_EMAIL}.`,
          ],
        },
      ],
    },
    privacy: {
      eyebrow: "משפטי",
      title: "מדיניות פרטיות",
      lastUpdatedLabel: "עדכון אחרון",
      calloutTitle: "בקצרה",
      calloutBody:
        "Shifter משתמש במידע שלך כדי להפעיל מרחבי סידור, לאבטח חשבונות, לשלוח התראות, לעבד חיובים, לתמוך במוצר ולשפר אמינות. איננו מוכרים מידע אישי.",
      relatedLinkLabel: "תנאי שימוש",
      sections: [
        {
          title: "1. מי אחראי למידע והיקף המדיניות",
          paragraphs: [
            `Ofek Labs מפעילה את Shifter ואחראית למידע האישי המתואר במדיניות זו. לשאלות או בקשות פרטיות ניתן לפנות אל ${LEGAL_CONTACT_EMAIL}.`,
            "מדיניות זו חלה על אתרי Shifter, אפליקציות web, APIs, כלי סידור, תהליכי חיוב, ערוצי תמיכה, משוב ושירותים קשורים.",
          ],
        },
        {
          title: "2. מידע אישי שאנו אוספים",
          bullets: [
            "נתוני חשבון: שם, שם תצוגה, אימייל, טלפון, גיבוב סיסמה, שפה מועדפת, אזור זמן ומצב אימות.",
            "נתוני פרופיל: תמונת פרופיל, תאריך לידה אם נמסר, תפקידים, חברות במרחבים ורמת הרשאה.",
            "נתוני סידור: קבוצות, משימות, משמרות, שיבוצים, זמינות, אילוצים, כשירויות, זמן בית, רשימות המתנה, החלפות, בקשות, התראות והיסטוריית סידור.",
            "נתוני מרחב: הגדרות, חברים, הזמנות, בעלות, הרשאות חיוב, פעילות ביקורת והגדרות תצורה.",
            "נתוני חיוב: תוכנית, סטטוס מנוי, מטא-דאטה של checkout, חידוש או ביטול, מזהי LemonSqueezy ונתוני webhook. איננו שומרים מספרי כרטיס מלאים.",
            "תקשורת: תמיכה, משוב, דיווחי באגים, העדפות התראה, אירועי אימייל ומטא-דאטה של WhatsApp/SMS היכן שמוגדר.",
            "קבצים ויצוא: תמונות פרופיל, נתוני יבוא, PDF/CSV וקבצים שהועלו או נוצרו.",
            "מידע טכני ואנליטי: IP, דפדפן, לוגים, cookies, local storage, נתוני סשן, שימוש API, שגיאות, דיאגנוסטיקה, אירועי מוצר והקלטת סשן כאשר מופעלת.",
          ],
        },
        {
          title: "3. כיצד אנו אוספים מידע",
          bullets: [
            "ישירות ממך בעת הרשמה, עדכון פרופיל, מילוי טפסים, העלאת קבצים או פנייה אלינו;",
            "ממנהלי מרחב שמוסיפים או מזמינים משתמשים ומגדירים סידורים;",
            "אוטומטית דרך האפליקציה, API, לוגים, cookies, local storage, אנליטיקה וניטור שגיאות;",
            "מספקי תשלום, אימייל, הודעות, אירוח, אחסון ו-AI שתומכים בשירות.",
          ],
        },
        {
          title: "4. כיצד אנו משתמשים במידע",
          bullets: [
            "יצירת חשבונות, אימות משתמשים, ניהול סשנים ואבטחת גישה;",
            "הפעלת מרחבים, קבוצות, סידורים, שיבוצים, בחירת משמרות, התראות ויצוא;",
            "עיבוד מנויים, ניסיונות, שינויי חיוב, חידושים, ביטולים וחשבוניות;",
            "שליחת הודעות תפעוליות כגון אימות, איפוס סיסמה, הזמנות, עדכוני סידור והודעות חזרה;",
            "תמיכה, חקירת באגים, ניטור אמינות, זיהוי שימוש לרעה, מניעת הונאה ויומני ביקורת;",
            "שיפור שימושיות, ביצועים, איכות תכונות ואבטחה;",
            "עמידה בחובות משפטיות, מס, חשבונאות, חיוב, אבטחה ורגולציה.",
          ],
        },
        {
          title: "5. בסיסים חוקיים לעיבוד",
          paragraphs: [
            "כאשר נדרש בסיס חוקי, אנו מעבדים מידע אישי לצורך ביצוע חוזה, אינטרסים לגיטימיים, הסכמה, חובה חוקית והגנה על זכויות ואבטחה. ניתן למשוך הסכמה כאשר העיבוד מבוסס על הסכמה, אך הדבר לא ישפיע על עיבוד שבוצע לפני המשיכה.",
          ],
        },
        {
          title: "6. ספקים ומעבדי מידע",
          bullets: [
            "LemonSqueezy: תשלום, חשבוניות, מסים וניהול מנויים.",
            "SendGrid: שליחת אימיילים תפעוליים.",
            "Twilio: משלוח WhatsApp או SMS היכן שמוגדר.",
            "PostHog: אנליטיקת מוצר, צפיות עמוד, אירועי שימוש והקלטת סשן כאשר מופעל בפרודקשן.",
            "Sentry: ניטור שגיאות, ביצועים, הקשר דיאגנוסטי והקלטת סשן מוגבלת לצורך דיבוג.",
            "ספקי אירוח, מסדי נתונים ואחסון: אירוח, PostgreSQL, גיבויים, אחסון מקומי או S3 ותפעול תשתיות.",
            "ספקי AI: ניתוח, יבוא, סיכום או המלצות מבוססי AI היכן שמופעל.",
          ],
        },
        {
          title: "7. איננו מוכרים מידע אישי",
          paragraphs: [
            "איננו מוכרים מידע אישי ואיננו משתפים מידע אישי לרשתות פרסום של צד שלישי. אם הדבר ישתנה, נעדכן מדיניות זו ונספק בחירות או הודעות כנדרש.",
          ],
        },
        {
          title: "8. Cookies, אחסון מקומי וטכנולוגיות דומות",
          paragraphs: ["Shifter משתמש ב-cookies, local storage וטכנולוגיות דומות עבור:"],
          bullets: [
            "אימות, מצב טוקנים, המשכיות סשן ושומרי גישה;",
            "שפה, ערכת נושא, אזור זמן, מטמון לא מקוון והעדפות אפליקציה;",
            "אבטחה, מניעת שימוש לרעה, דיאגנוסטיקה, אמינות ואנליטיקה כאשר מוגדרת.",
          ],
        },
        {
          title: "9. אבטחה ושמירת מידע",
          paragraphs: [
            "אנו משתמשים באמצעי הגנה כגון HTTPS/TLS, גיבוב סיסמאות, בדיקות תפקידים והרשאות, בידוד מרחבים, יומני ביקורת, טיפול שגיאות בפרודקשן, גישה מנהלית מוגבלת וגיבויים. אין מערכת אבטחה מושלמת.",
            "אנו שומרים מידע כל עוד נדרש להפעלת Shifter, עמידה בחובות חוקיות, פתרון מחלוקות, אבטחה, מניעת שימוש לרעה ואכיפת הסכמים. מידע אישי של חשבון שנמחק נמחק או עובר אנונימיזציה בדרך כלל תוך 30 יום כאשר הדבר אפשרי טכנית וחוקית. רשומות חיוב, אבטחה, ביקורת וגיבויים עשויים להישמר יותר זמן.",
          ],
        },
        {
          title: "10. הבחירות והזכויות שלך",
          paragraphs: [
            "בהתאם למיקום שלך וליחס שלך למרחב עבודה, ייתכן שיש לך זכויות לעיין, לתקן, לייצא, למחוק, להגביל או להתנגד לעיבוד מידע אישי. חלק מהבקשות עשויות להצריך טיפול של מנהל המרחב אם הוא שולט במרחב.",
            `למימוש זכויות ניתן להשתמש בכלים באפליקציה או לפנות אל ${LEGAL_CONTACT_EMAIL}.`,
          ],
        },
        {
          title: "11. מנהלי מרחב",
          paragraphs: [
            "מנהלי מרחב עשויים לגשת ולנהל מידע שהוזן על ידי חברי המרחב, לרבות סידורים, זמינות, אילוצים, שיבוצים, פרטי פרופיל ופעילות הדרושה להפעלת המרחב. אם הארגון שלך משתמש ב-Shifter, ייתכן שגם המדיניות הפנימית שלו תחול.",
          ],
        },
        {
          title: "12. משתמשים בינלאומיים",
          paragraphs: [
            "Shifter עשוי לעבד ולאחסן מידע בישראל, בארצות הברית, באזור הכלכלי האירופי או במקומות אחרים שבהם אנו או ספקינו פועלים. בשימוש בשירות אתה מבין שמידע עשוי לעבור למדינות עם חוקי פרטיות שונים.",
          ],
        },
        {
          title: "13. ילדים, חוקי פרטיות ושינויים",
          paragraphs: [
            "Shifter אינו מיועד לילדים מתחת לגיל 16 ואיננו אוספים ביודעין מידע אישי מילדים מתחת לגיל 16.",
            "אנו שואפים לטפל במידע אישי בהתאם לדרישות הפרטיות החלות בישראל, וכאשר רלוונטי גם לפי עקרונות GDPR למשתמשים באזור הכלכלי האירופי או בבריטניה.",
            "אנו עשויים לעדכן מדיניות זו מעת לעת. אם השינויים מהותיים, נמסור הודעה סבירה באפליקציה, באימייל או בדרך מתאימה אחרת.",
          ],
        },
      ],
    },
    subprocessors: {
      eyebrow: "משפטי",
      title: "ספקי משנה",
      lastUpdatedLabel: "עדכון אחרון",
      calloutTitle: "מה מופיע בעמוד זה",
      calloutBody:
        "עמוד זה מפרט ספקי צד שלישי שעשויים לעבד מידע אישי או נתוני לקוח כאשר Shifter מוגדר להשתמש בהם. הזמינות עשויה להשתנות לפי סביבה, תוכנית ותצורת תכונות.",
      relatedLinkLabel: "מדיניות פרטיות",
      sections: [
        {
          title: "1. תשלומים ומנויים",
          bullets: [
            "LemonSqueezy: תהליך checkout, עיבוד תשלומים, מסים, חשבוניות, סטטוס מנוי, מזהי לקוח ו-webhooks של חיוב.",
          ],
        },
        {
          title: "2. תקשורת",
          bullets: [
            "SendGrid: שליחת אימיילים תפעוליים כגון אימות, איפוס סיסמה, הזמנות, עדכוני סידור והודעות שירות.",
            "Twilio: משלוח WhatsApp או SMS להתראות תפעוליות כאשר הודעות מוגדרות.",
          ],
        },
        {
          title: "3. אנליטיקה ואמינות",
          bullets: [
            "PostHog: אנליטיקת מוצר, אירועי עמוד, אירועי שימוש ודיאגנוסטיקת סשן כאשר ניתנה הסכמה ואנליטיקה בפרודקשן מוגדרת.",
            "Sentry: ניטור שגיאות, דיאגנוסטיקת ביצועים ונתוני replay מוגבלים כאשר הסכמה ותצורת פרודקשן מאפשרות זאת.",
          ],
        },
        {
          title: "4. תשתית ואחסון",
          bullets: [
            "ספקי אירוח ומסדי נתונים: אירוח אפליקציה ו-API, PostgreSQL, שירותי רשת, גיבויים ולוגים תפעוליים.",
            "אחסון מקומי או S3-compatible: תמונות פרופיל, העלאות, יצוא, קבצים שנוצרו וגיבויים קשורים.",
          ],
        },
        {
          title: "5. תכונות AI",
          bullets: [
            "ספקי מודלי AI: ניתוח, יבוא, סיכום, המלצה או סיוע בתוכן הקשור לסידורים כאשר תכונות AI מופעלות.",
          ],
        },
        {
          title: "6. עדכונים ושאלות",
          paragraphs: [
            "אנו עשויים לעדכן עמוד זה כאשר ספקים מתווספים, מוסרים, מוחלפים או משתנים באופן מהותי.",
            `לשאלות לגבי ספקי משנה ניתן לפנות אל ${LEGAL_CONTACT_EMAIL}.`,
          ],
        },
      ],
    },
    dpa: {
      eyebrow: "משפטי",
      title: "נספח עיבוד מידע",
      lastUpdatedLabel: "עדכון אחרון",
      calloutTitle: "כיצד להשתמש בנספח זה",
      calloutBody:
        "נספח עיבוד מידע זה מיועד לארגונים שמשתמשים ב-Shifter לניהול נתוני חברי צוות או כוח אדם. הוא מסביר את תפקידי בעל השליטה והמעבד ואת התחייבויות העיבוד המרכזיות. לתנאים חתומים לארגונים, פנו אל Ofek Labs.",
      relatedLinkLabel: "מדיניות פרטיות",
      sections: [
        {
          title: "1. צדדים והיקף",
          paragraphs: [
            "נספח עיבוד מידע זה משלים את תנאי השימוש ומדיניות הפרטיות כאשר לקוח משתמש ב-Shifter לעיבוד מידע אישי בשם מרחב עבודה, מעסיק, צוות, יחידה או ארגון.",
            "הלקוח הוא בדרך כלל בעל השליטה בנתוני הלקוח, ו-Ofek Labs היא בדרך כלל המעבדת או ספק השירות שמעבד את הנתונים לצורך אספקת Shifter, אלא אם חוק חל או הסכם כתוב קובע אחרת.",
          ],
        },
        {
          title: "2. הוראות עיבוד",
          paragraphs: [
            "Ofek Labs תעבד נתוני לקוח רק כדי לספק, לאבטח, לתמוך, לתחזק ולשפר את Shifter; לפעול לפי הוראות מתועדות של הלקוח; לעמוד בחוק; ולהגן על זכויות, בטיחות ואבטחה.",
            "תנאי השימוש, מדיניות הפרטיות, הגדרות המוצר, פעולות מנהל, בקשות תמיכה ונספח זה הם הוראות מתועדות לעיבוד.",
          ],
        },
        {
          title: "3. קטגוריות נושאי מידע ומידע אישי",
          bullets: [
            "נושאי מידע עשויים לכלול בעלי מרחב, מנהלים, עובדים, מתנדבים, קבלנים, חברי צוות, מוזמנים, אנשי קשר לתמיכה ומשתמשי קצה.",
            "מידע אישי עשוי לכלול נתוני חשבון, פרטי קשר, פרופיל, תפקידים, הרשאות, נתוני סידור, זמינות, אילוצים, שיבוצים, הודעות, קבצים, מטא-דאטה של חיוב, לוגים טכניים ותקשורת תמיכה.",
            "אין למסור מידע אישי רגיש אלא אם הוא נחוץ לשימוש בסידור ומותר לפי חוק.",
          ],
        },
        {
          title: "4. אחריות הלקוח",
          bullets: [
            "הלקוח אחראי לבסיס חוקי לאיסוף ולעיבוד נתוני חברי צוות ב-Shifter.",
            "הלקוח אחראי למסירת הודעות נדרשות למשתמשים ולחברי צוות.",
            "הלקוח חייב להגדיר תפקידים והרשאות כראוי ולהסיר גישה כשאין בה עוד צורך.",
            "הלקוח חייב להימנע מהזנת מידע רגיש שאינו נחוץ.",
          ],
        },
        {
          title: "5. סודיות ואנשי צוות",
          paragraphs: [
            "Ofek Labs מגבילה גישה לנתוני לקוח לאנשי צוות ולספקים שזקוקים לגישה לצורך תפעול, אבטחה, תמיכה או שיפור Shifter. מי שנחשף לנתוני לקוח מצופה להגן עליהם ולהשתמש בהם רק למטרות מורשות.",
          ],
        },
        {
          title: "6. אמצעי אבטחה",
          bullets: [
            "HTTPS/TLS למידע בתעבורה.",
            "גיבוב סיסמאות ובקרות אימות.",
            "בדיקות תפקידים והרשאות.",
            "בידוד מרחבים ובקרות גישה מודעות-דייר.",
            "טיפול שגיאות בפרודקשן ולוגים מודעי אבטחה.",
            "גיבויים, ניטור ובקרות תפעוליות המתאימות לשירות.",
          ],
        },
        {
          title: "7. ספקי משנה",
          paragraphs: [
            "Ofek Labs עשויה להשתמש בספקי משנה לתשלומים, אימייל, הודעות, אנליטיקה, ניטור שגיאות, אירוח, אחסון, AI ותשתית. הרשימה הציבורית הנוכחית זמינה בעמוד ספקי המשנה.",
            "אנו נשארים אחראים לספקי משנה שאנו מפעילים לעיבוד נתוני לקוח מטעמנו, בכפוף למגבלות בתנאי השימוש.",
          ],
        },
        {
          title: "8. סיוע בבקשות זכויות",
          paragraphs: [
            "כאשר נדרש לפי חוק וככל שסביר, Ofek Labs תסייע ללקוחות בבקשות נושאי מידע באמצעות יכולות מוצר, יצוא, מחיקה או תמיכה. הלקוח נשאר אחראי למענה לבקשות כאשר הוא בעל השליטה.",
          ],
        },
        {
          title: "9. אירועי אבטחה",
          paragraphs: [
            "אם Ofek Labs תגלה אירוע אבטחה מאומת הכולל נתוני לקוח, נודיע ללקוחות המושפעים ללא עיכוב בלתי סביר, נספק מידע שזמין לנו באופן סביר, וננקוט צעדים סבירים לבלימה, חקירה ותיקון.",
          ],
        },
        {
          title: "10. החזרה, מחיקה וביקורת",
          paragraphs: [
            "בעת סיום או מחיקה, נתוני לקוח יימחקו או יעברו אנונימיזציה לפי מדיניות הפרטיות, התנהגות המוצר, מחזורי גיבוי וחובות שמירה חוקיות.",
            "לקוחות רשאים לבקש מידע סביר לגבי נוהלי האבטחה והפרטיות שלנו. כל זכות ביקורת תמומש באופן שמגן על לקוחות אחרים, אבטחה, מידע סודי ואמינות השירות.",
          ],
        },
        {
          title: "11. העברות בינלאומיות",
          paragraphs: [
            "נתוני לקוח עשויים להיות מעובדים בישראל, בארצות הברית, באזור הכלכלי האירופי ובמקומות אחרים שבהם Ofek Labs או ספקי משנה פועלים. כאשר נדרש, הלקוח ו-Ofek Labs יסתמכו על מנגנוני העברה מתאימים.",
          ],
        },
        {
          title: "12. יצירת קשר",
          paragraphs: [
            `לשאלות DPA או מסמכים ארגוניים חתומים ניתן לפנות אל ${LEGAL_CONTACT_EMAIL}.`,
          ],
        },
      ],
    },
  },
  ru: {
    terms: {
      eyebrow: "Юридическая информация",
      title: "Условия использования",
      lastUpdatedLabel: "Последнее обновление",
      calloutTitle: "Важное уведомление о расписаниях",
      calloutBody:
        "Shifter помогает создавать и управлять расписаниями, но не заменяет проверку человеком. Вы отвечаете за проверку укомплектованности, правовых требований, требований безопасности, правил отдыха, аварийного покрытия и операционной пригодности перед использованием или публикацией расписания.",
      relatedLinkLabel: "Политика конфиденциальности",
      sections: [
        {
          title: "1. Кто мы и принятие условий",
          paragraphs: [
            "Настоящие Условия регулируют доступ к Shifter и его использование. Shifter — сервис планирования смен и координации персонала, управляемый Ofek Labs. Используя сервис, создавая аккаунт, присоединяясь к рабочему пространству или покупая подписку, вы соглашаетесь с этими Условиями.",
            "Если вы используете Shifter от имени организации, вы подтверждаете, что имеете полномочия принять эти Условия от ее имени.",
          ],
        },
        {
          title: "2. Сервис",
          paragraphs: [
            "Shifter предоставляет инструменты для управления рабочими пространствами, командами и группами, планирования смен, автоматического составления расписаний, сбора доступности и ограничений, самостоятельного выбора смен, уведомлений, экспорта, биллинга, аналитики для операторов и поддержки.",
            "Мы можем добавлять, изменять, приостанавливать или удалять функции время от времени.",
          ],
        },
        {
          title: "3. Аккаунты, администраторы и пользователи",
          bullets: [
            "Вы должны предоставлять точные данные аккаунта и поддерживать их актуальность.",
            "Вы отвечаете за учетные данные, passkey, устройства и действия в своем аккаунте.",
            "Администраторы рабочего пространства управляют настройками, участниками, ролями, расписаниями, разрешениями и биллингом.",
            "Администраторы отвечают за получение необходимых уведомлений или согласий перед добавлением данных участников команды.",
            "Сервис предназначен для пользователей от 16 лет.",
          ],
        },
        {
          title: "4. Допустимое использование",
          paragraphs: ["Запрещено использовать Shifter для:"],
          bullets: [
            "нарушения законов, трудовых правил, правил конфиденциальности, безопасности или прав третьих лиц;",
            "загрузки незаконного, вредного, дискриминационного, оскорбительного или нарушающего права контента;",
            "несанкционированного доступа к пользователю, рабочему пространству, системе или базе данных;",
            "нарушения работы сервиса, reverse engineering, scraping, перегрузки или обхода ограничений;",
            "удаления или обхода средств безопасности, лимитов, журналов аудита или контроля доступа;",
            "принятия полностью автоматических операционных решений без надлежащей проверки человеком.",
          ],
        },
        {
          title: "5. Результаты расписаний и AI-функции",
          paragraphs: [
            "Shifter может создавать рекомендации, черновики, предупреждения, статистику, экспорт, импорт, сводки, AI-анализ и автоматические расписания. Эти результаты могут быть неполными, неточными, задержанными или неподходящими для конкретной операционной среды.",
            "Вы должны проверять все автоматические или AI-результаты перед использованием. Мы не отвечаем за пропущенные смены, нехватку или избыток персонала, нарушения трудового законодательства, усталость, инциденты безопасности, потерю дохода или иные операционные последствия.",
          ],
        },
        {
          title: "6. Данные клиента",
          paragraphs: [
            "Вы сохраняете право собственности на имена, расписания, доступность, ограничения, файлы, заметки, отзывы и другой контент, переданный в Shifter. Вы предоставляете нам ограниченную лицензию хранить, обрабатывать, передавать, отображать, резервировать и использовать эти данные для предоставления, защиты, поддержки и улучшения сервиса.",
            "Вы подтверждаете, что имеете необходимые права и разрешения для передачи данных в Shifter.",
          ],
        },
        {
          title: "7. Подписки, биллинг, налоги и возвраты",
          paragraphs: [
            "Платные планы, пробные периоды, продления, обновления, отмены, счета, налоги и платежи могут обрабатываться через LemonSqueezy или другого платежного провайдера. Цены, лимиты, пробный период и включенные функции указаны в продукте или checkout.",
            "Если не указано иное, подписки автоматически продлеваются до отмены. Отмена обычно вступает в силу в конце текущего расчетного периода. За исключением случаев, требуемых законом или явно указанных в checkout, платежи не возвращаются, а кредиты за частичные периоды или неиспользованные функции не предоставляются.",
          ],
        },
        {
          title: "8. Сторонние сервисы",
          paragraphs: [
            "Shifter может использовать сторонних провайдеров для платежей, email, WhatsApp или SMS, мониторинга ошибок, аналитики, хранения файлов, хостинга, баз данных, резервных копий, AI и инфраструктуры. Эти сервисы регулируются собственными условиями и политиками.",
            "Мы не отвечаем за сбои, изменения политик или обработку данных вне нашего контроля.",
          ],
        },
        {
          title: "9. Интеллектуальная собственность",
          paragraphs: [
            "Shifter, включая программное обеспечение, дизайн, процессы, бренд, логотипы, документацию и алгоритмы, принадлежит Ofek Labs или ее лицензиарам. Эти Условия не передают вам права интеллектуальной собственности.",
            "Запрещено копировать, изменять, распространять, продавать, сдавать в аренду или создавать производные работы на основе сервиса без нашего явного письменного разрешения.",
          ],
        },
        {
          title: "10. Конфиденциальность, безопасность и доступность",
          paragraphs: [
            "Наша Политика конфиденциальности объясняет, как мы собираем, используем, храним и передаем персональную информацию. Мы используем разумные меры защиты, но ни один сервис не может быть гарантированно полностью безопасным или постоянно доступным.",
            "Сервис может быть недоступен из-за обслуживания, релизов, событий безопасности, сбоев третьих сторон, проблем интернета, форс-мажора или других причин.",
          ],
        },
        {
          title: "11. Приостановка и прекращение",
          paragraphs: [
            "Вы можете прекратить использование Shifter в любое время. Мы можем приостановить или прекратить доступ, если вы нарушаете Условия, создаете правовой или security-риск, не оплачиваете суммы, злоупотребляете сервисом или если этого требует закон.",
            "После прекращения мы можем сохранять данные, как описано в Политике конфиденциальности, и насколько это необходимо для юридических, security, backup, billing и audit целей.",
          ],
        },
        {
          title: "12. Отказ от гарантий и ограничение ответственности",
          paragraphs: [
            "В максимально разрешенной законом степени Shifter предоставляется «как есть» и «по доступности» без гарантий любого рода. Мы отказываемся от гарантий пригодности для продажи, пригодности для конкретной цели, ненарушения прав, точности, доступности и работы без ошибок.",
            "В максимально разрешенной законом степени Ofek Labs не несет ответственности за косвенные, случайные, специальные, последующие, штрафные убытки, потерю прибыли, данных, перерыв бизнеса, кадровые сбои, ошибки расписания или операционные потери. Наша совокупная ответственность ограничена суммой, уплаченной вами за Shifter за три месяца до события, или 100 долларами США, если оплата не производилась.",
          ],
        },
        {
          title: "13. Возмещение убытков",
          paragraphs: [
            "Вы соглашаетесь защищать и освобождать Ofek Labs от претензий, убытков, обязательств, потерь и расходов, возникающих из вашего использования Shifter, ваших данных, нарушения этих Условий, нарушения закона или нарушения прав третьих лиц.",
          ],
        },
        {
          title: "14. Применимое право, изменения и контакт",
          paragraphs: [
            "Эти Условия регулируются законами Государства Израиль без учета коллизионных норм. Компетентные суды Израиля имеют исключительную юрисдикцию, если применимое право не требует иного.",
            "Мы можем обновлять эти Условия. При существенных изменениях мы предоставим разумное уведомление в приложении, по email или другим подходящим способом. Продолжение использования после вступления изменений в силу означает принятие обновленных Условий.",
            `По вопросам этих Условий свяжитесь с нами: ${LEGAL_CONTACT_EMAIL}.`,
          ],
        },
      ],
    },
    privacy: {
      eyebrow: "Юридическая информация",
      title: "Политика конфиденциальности",
      lastUpdatedLabel: "Последнее обновление",
      calloutTitle: "Кратко",
      calloutBody:
        "Shifter использует ваши данные для работы расписаний и рабочих пространств, защиты аккаунтов, отправки уведомлений, обработки биллинга, поддержки продукта и повышения надежности. Мы не продаем персональную информацию.",
      relatedLinkLabel: "Условия использования",
      sections: [
        {
          title: "1. Кто отвечает за данные и область действия",
          paragraphs: [
            `Ofek Labs управляет Shifter и отвечает за персональную информацию, описанную в этой Политике. По вопросам или запросам конфиденциальности пишите на ${LEGAL_CONTACT_EMAIL}.`,
            "Эта Политика применяется к сайтам Shifter, web-приложениям, API, инструментам расписания, биллингу, поддержке, feedback-инструментам и связанным сервисам.",
          ],
        },
        {
          title: "2. Персональная информация, которую мы собираем",
          bullets: [
            "Данные аккаунта: имя, отображаемое имя, email, телефон, хеш пароля, язык, часовой пояс и состояние аутентификации.",
            "Данные профиля: фото профиля, дата рождения если указана, роли, членство в рабочих пространствах и уровень доступа.",
            "Данные расписаний: группы, задачи, смены, назначения, доступность, ограничения, квалификации, настройки home leave, waitlist, swaps, requests, alerts и история расписаний.",
            "Данные рабочего пространства: настройки, участники, приглашения, владение, права биллинга, audit activity и конфигурация.",
            "Биллинг: план, статус подписки, checkout metadata, продление или отмена, идентификаторы LemonSqueezy и webhook-данные. Мы не храним полные номера карт.",
            "Коммуникации: поддержка, feedback, bug reports, настройки уведомлений, email delivery events и WhatsApp/SMS metadata где настроено.",
            "Файлы и экспорт: фото профиля, импортированные данные, PDF/CSV и загруженные или созданные файлы.",
            "Технические и аналитические данные: IP, браузер, логи, cookies, local storage, session data, API usage, ошибки, diagnostics, product events и session replay когда включено.",
          ],
        },
        {
          title: "3. Как мы собираем информацию",
          bullets: [
            "непосредственно от вас при регистрации, обновлении профиля, заполнении форм, загрузке файлов или обращении к нам;",
            "от администраторов рабочих пространств, которые добавляют или приглашают пользователей и настраивают расписания;",
            "автоматически через приложение, API, логи, cookies, local storage, аналитику и мониторинг ошибок;",
            "от провайдеров платежей, email, сообщений, хостинга, хранения и AI, поддерживающих сервис.",
          ],
        },
        {
          title: "4. Как мы используем информацию",
          bullets: [
            "создание аккаунтов, аутентификация, управление сессиями и защита доступа;",
            "работа рабочих пространств, групп, расписаний, назначений, self-service shifts, уведомлений и экспорта;",
            "обработка подписок, trial, изменений биллинга, продлений, отмен и счетов;",
            "отправка операционных сообщений: verification, password reset, invitations, schedule updates и recall notices;",
            "поддержка, расследование ошибок, мониторинг надежности, выявление злоупотреблений, предотвращение мошенничества и audit logs;",
            "улучшение удобства, производительности, качества функций и безопасности;",
            "выполнение юридических, налоговых, бухгалтерских, billing, security и regulatory obligations.",
          ],
        },
        {
          title: "5. Правовые основания обработки",
          paragraphs: [
            "Когда требуется правовое основание, мы обрабатываем персональную информацию для исполнения договора, законных интересов, согласия, юридических обязательств и защиты прав и безопасности. Вы можете отозвать согласие, если обработка основана на согласии, но это не влияет на обработку до отзыва.",
          ],
        },
        {
          title: "6. Провайдеры и обработчики",
          bullets: [
            "LemonSqueezy: checkout, платежи, счета, налоги и управление подписками.",
            "SendGrid: отправка transactional email.",
            "Twilio: доставка WhatsApp или SMS где настроено.",
            "PostHog: product analytics, page events, usage events и session replay при включении в production.",
            "Sentry: error monitoring, performance traces, diagnostic context и ограниченный session replay для debugging.",
            "Хостинг, базы данных и storage providers: hosting, PostgreSQL, backups, local или S3-compatible storage и infrastructure operations.",
            "AI providers: AI-assisted parsing, import, summary или recommendations где включено.",
          ],
        },
        {
          title: "7. Мы не продаем персональную информацию",
          paragraphs: [
            "Мы не продаем персональную информацию и не передаем ее сторонним рекламным сетям. Если это изменится, мы обновим Политику и предоставим необходимые уведомления или выбор.",
          ],
        },
        {
          title: "8. Cookies, local storage и похожие технологии",
          paragraphs: ["Shifter использует cookies, local storage и похожие технологии для:"],
          bullets: [
            "аутентификации, состояния токенов, непрерывности сессии и auth guards;",
            "языка, темы, часового пояса, offline cache и настроек приложения;",
            "безопасности, предотвращения злоупотреблений, diagnostics, reliability и analytics когда настроено.",
          ],
        },
        {
          title: "9. Безопасность и хранение",
          paragraphs: [
            "Мы используем меры защиты, включая HTTPS/TLS, хеширование паролей, проверки ролей и разрешений, изоляцию рабочих пространств, audit logs, production error handling, ограниченный административный доступ и backups. Ни одна система безопасности не идеальна.",
            "Мы храним информацию столько, сколько нужно для предоставления Shifter, выполнения юридических обязательств, разрешения споров, безопасности, предотвращения злоупотреблений и исполнения соглашений. Персональные данные удаленного аккаунта обычно удаляются или анонимизируются в течение 30 дней, когда это технически и юридически возможно. Billing records, security logs, audit records и backups могут храниться дольше.",
          ],
        },
        {
          title: "10. Ваш выбор и права",
          paragraphs: [
            "В зависимости от вашего местоположения и отношения к рабочему пространству вы можете иметь права на доступ, исправление, экспорт, удаление, ограничение или возражение против обработки персональной информации. Некоторые запросы должны обрабатываться администратором рабочего пространства, если он контролирует это пространство.",
            `Для реализации прав используйте доступные инструменты в приложении или пишите на ${LEGAL_CONTACT_EMAIL}.`,
          ],
        },
        {
          title: "11. Администраторы рабочих пространств",
          paragraphs: [
            "Администраторы могут получать доступ и управлять данными участников рабочего пространства, включая расписания, доступность, ограничения, назначения, данные профиля и активность, необходимую для работы пространства. Если ваша организация использует Shifter, ее внутренние политики также могут применяться.",
          ],
        },
        {
          title: "12. Международные пользователи",
          paragraphs: [
            "Shifter может обрабатывать и хранить информацию в Израиле, США, Европейской экономической зоне или других местах, где работаем мы или наши провайдеры. Используя сервис, вы понимаете, что данные могут передаваться в страны с другими законами о конфиденциальности.",
          ],
        },
        {
          title: "13. Дети, законы о приватности и изменения",
          paragraphs: [
            "Shifter не предназначен для детей младше 16 лет, и мы сознательно не собираем персональную информацию детей младше 16 лет.",
            "Мы стремимся обрабатывать персональную информацию в соответствии с применимыми требованиями приватности Израиля и, где применимо, принципами GDPR для пользователей в ЕЭЗ или Великобритании.",
            "Мы можем обновлять эту Политику время от времени. При существенных изменениях мы предоставим разумное уведомление в приложении, по email или другим подходящим способом.",
          ],
        },
      ],
    },
    subprocessors: {
      eyebrow: "Юридическая информация",
      title: "Субобработчики",
      lastUpdatedLabel: "Последнее обновление",
      calloutTitle: "Что описывает эта страница",
      calloutBody:
        "Эта страница перечисляет сторонних провайдеров, которые могут обрабатывать персональную информацию или данные клиента, когда Shifter настроен на их использование. Доступность зависит от среды, плана и конфигурации функций.",
      relatedLinkLabel: "Политика конфиденциальности",
      sections: [
        {
          title: "1. Платежи и подписки",
          bullets: [
            "LemonSqueezy: checkout, обработка платежей, налоги, счета, статус подписки, идентификаторы клиента и billing webhooks.",
          ],
        },
        {
          title: "2. Коммуникации",
          bullets: [
            "SendGrid: transactional email, включая verification, password reset, invitations, schedule updates и service notices.",
            "Twilio: доставка WhatsApp или SMS для операционных уведомлений, если messaging настроен.",
          ],
        },
        {
          title: "3. Аналитика и надежность",
          bullets: [
            "PostHog: product analytics, page events, usage events и session diagnostics, когда пользователь дал согласие и production analytics настроена.",
            "Sentry: error monitoring, performance diagnostics и ограниченные replay-данные, когда согласие и production-настройки это позволяют.",
          ],
        },
        {
          title: "4. Инфраструктура и хранение",
          bullets: [
            "Провайдеры хостинга и баз данных: hosting приложения и API, PostgreSQL, network services, backups и operational logs.",
            "Local или S3-compatible storage: фото профиля, uploads, exports, generated files и связанные backups.",
          ],
        },
        {
          title: "5. AI-функции",
          bullets: [
            "AI model providers: parsing, importing, summarizing, recommending или помощь с scheduling-related content, когда AI-функции включены.",
          ],
        },
        {
          title: "6. Обновления и вопросы",
          paragraphs: [
            "Мы можем обновлять эту страницу, когда провайдеры добавляются, удаляются, заменяются или существенно меняются.",
            `По вопросам субобработчиков пишите на ${LEGAL_CONTACT_EMAIL}.`,
          ],
        },
      ],
    },
    dpa: {
      eyebrow: "Юридическая информация",
      title: "Дополнение об обработке данных",
      lastUpdatedLabel: "Последнее обновление",
      calloutTitle: "Как использовать это DPA",
      calloutBody:
        "Это Дополнение об обработке данных предназначено для организаций, которые используют Shifter для управления данными участников команды или персонала. Оно объясняет роли controller/processor и основные обязательства обработки. Для подписанных enterprise-документов свяжитесь с Ofek Labs.",
      relatedLinkLabel: "Политика конфиденциальности",
      sections: [
        {
          title: "1. Стороны и область действия",
          paragraphs: [
            "Это Дополнение об обработке данных (DPA) дополняет Условия использования и Политику конфиденциальности, когда клиент использует Shifter для обработки персональной информации от имени рабочего пространства, работодателя, команды, подразделения или организации.",
            "Клиент обычно является controller данных клиента, а Ofek Labs обычно является processor или service provider, который обрабатывает данные для предоставления Shifter, если применимое право или отдельное письменное соглашение не предусматривает иное.",
          ],
        },
        {
          title: "2. Инструкции обработки",
          paragraphs: [
            "Ofek Labs будет обрабатывать данные клиента только для предоставления, защиты, поддержки, обслуживания и улучшения Shifter; выполнения документированных инструкций клиента; соблюдения закона; и защиты прав, безопасности и security.",
            "Условия, Политика конфиденциальности, настройки продукта, действия администратора, запросы поддержки и это DPA являются документированными инструкциями обработки.",
          ],
        },
        {
          title: "3. Категории субъектов и персональных данных",
          bullets: [
            "Субъекты данных могут включать владельцев рабочих пространств, администраторов, сотрудников, волонтеров, подрядчиков, участников команды, приглашенных, контакты поддержки и конечных пользователей.",
            "Персональные данные могут включать account data, contact details, profile data, roles, permissions, scheduling data, availability, constraints, assignments, messages, files, billing metadata, technical logs и support communications.",
            "Клиенты не должны передавать sensitive personal data, если это не необходимо для scheduling use case и не разрешено законом.",
          ],
        },
        {
          title: "4. Обязанности клиента",
          bullets: [
            "Клиент отвечает за законное основание сбора и обработки данных участников команды в Shifter.",
            "Клиент отвечает за предоставление необходимых уведомлений пользователям и участникам команды.",
            "Клиент должен правильно настраивать роли и разрешения и удалять доступ, когда он больше не нужен.",
            "Клиент должен избегать передачи ненужной sensitive information.",
          ],
        },
        {
          title: "5. Конфиденциальность и персонал",
          paragraphs: [
            "Ofek Labs ограничивает доступ к данным клиента персоналом и service providers, которым доступ нужен для эксплуатации, защиты, поддержки или улучшения Shifter. Персонал с доступом к данным клиента должен защищать их и использовать только для разрешенных целей.",
          ],
        },
        {
          title: "6. Меры безопасности",
          bullets: [
            "HTTPS/TLS для данных при передаче.",
            "Хеширование паролей и authentication controls.",
            "Проверки ролей и разрешений.",
            "Изоляция рабочих пространств и tenant-aware controls.",
            "Production error handling и security-conscious logging.",
            "Backups, monitoring и operational controls, соответствующие сервису.",
          ],
        },
        {
          title: "7. Субобработчики",
          paragraphs: [
            "Ofek Labs может использовать субобработчиков для платежей, email, сообщений, analytics, error monitoring, hosting, storage, AI assistance и infrastructure. Актуальный публичный список доступен на странице Subprocessors.",
            "Мы остаемся ответственными за субобработчиков, которых привлекаем для обработки данных клиента от нашего имени, с учетом ограничений в Условиях.",
          ],
        },
        {
          title: "8. Помощь с запросами прав",
          paragraphs: [
            "Когда это требуется законом и разумно возможно, Ofek Labs поможет клиентам с запросами субъектов данных через функции продукта, exports, deletion workflows или support. Клиенты остаются ответственными за ответы на запросы, когда они являются controller.",
          ],
        },
        {
          title: "9. Инциденты безопасности",
          paragraphs: [
            "Если Ofek Labs узнает о подтвержденном security incident с данными клиента, мы уведомим затронутых клиентов без неоправданной задержки, предоставим разумно доступную информацию и примем разумные меры по containment, investigation и remediation.",
          ],
        },
        {
          title: "10. Возврат, удаление и аудит",
          paragraphs: [
            "При termination или deletion данные клиента будут удалены или anonymized согласно Privacy Policy, поведению продукта, backup cycles и legal retention obligations.",
            "Клиенты могут запросить разумную информацию о наших security и privacy practices. Любое audit right должно осуществляться так, чтобы защищать других клиентов, безопасность, confidential information и reliability сервиса.",
          ],
        },
        {
          title: "11. Международные передачи",
          paragraphs: [
            "Данные клиента могут обрабатываться в Израиле, США, Европейской экономической зоне и других местах, где работают Ofek Labs или subprocessors. Когда требуется, клиенты и Ofek Labs будут использовать подходящие transfer mechanisms.",
          ],
        },
        {
          title: "12. Контакт",
          paragraphs: [
            `По вопросам DPA или подписанных enterprise-документов пишите на ${LEGAL_CONTACT_EMAIL}.`,
          ],
        },
      ],
    },
  },
};
