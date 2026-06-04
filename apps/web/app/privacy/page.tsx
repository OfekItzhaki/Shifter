import Link from "next/link";
import ShifterLogo from "@/components/shell/ShifterLogo";

const CONTACT_EMAIL = process.env.NEXT_PUBLIC_LEGAL_EMAIL ?? "support@ofeklabs.com";
const LAST_UPDATED = "June 4, 2026";

export default function PrivacyPage() {
  return (
    <div className="min-h-screen bg-white dark:bg-slate-900">
      <header className="border-b border-slate-100 dark:border-slate-800 px-6 py-4">
        <div className="mx-auto flex max-w-3xl items-center justify-between">
          <Link href="/" className="flex items-center gap-2 text-slate-900 transition-colors hover:text-sky-600 dark:text-white">
            <ShifterLogo size={24} />
            <span className="text-sm font-bold">Shifter</span>
          </Link>
          <Link href="/terms" className="text-sm text-sky-600 hover:underline">
            Terms of Service
          </Link>
        </div>
      </header>

      <main className="mx-auto max-w-3xl px-6 py-12" dir="ltr">
        <p className="mb-3 text-xs font-semibold uppercase tracking-[0.18em] text-sky-600">Legal</p>
        <h1 className="mb-2 text-3xl font-bold text-slate-900 dark:text-white">Privacy Policy</h1>
        <p className="mb-8 text-sm text-slate-500 dark:text-slate-400">Last updated: {LAST_UPDATED}</p>

        <div className="mb-8 rounded-lg border border-sky-200 bg-sky-50 p-4 text-sm leading-relaxed text-sky-900 dark:border-sky-800 dark:bg-sky-950/30 dark:text-sky-200">
          <p className="font-semibold">Short version</p>
          <p className="mt-1">
            Shifter uses your data to operate scheduling workspaces, secure accounts, send notifications, process billing,
            support the product, and improve reliability. We do not sell personal information.
          </p>
        </div>

        <div className="space-y-7 text-sm leading-relaxed text-slate-700 dark:text-slate-300">
          <Section title="1. Who controls your data">
            <p>
              Ofek Labs ("we", "us", or "our") operates Shifter and is responsible for the personal information described
              in this Privacy Policy. For privacy questions or requests, contact{" "}
              <a href={`mailto:${CONTACT_EMAIL}`} className="text-sky-600 hover:underline">{CONTACT_EMAIL}</a>.
            </p>
          </Section>

          <Section title="2. Scope">
            <p>
              This Policy applies to Shifter websites, web applications, APIs, scheduling tools, billing flows, support
              channels, feedback tools, and related services. It does not apply to third-party websites or services that
              have their own privacy policies.
            </p>
          </Section>

          <Section title="3. Personal information we collect">
            <p>Depending on how you use Shifter, we may collect:</p>
            <ul className="mt-2 list-disc space-y-1 pl-5">
              <li><strong>Account data:</strong> name, display name, email address, phone number, password hash, preferred language, timezone, and authentication state.</li>
              <li><strong>Profile data:</strong> profile image, birthday if provided, role information, workspace membership, and permission level.</li>
              <li><strong>Scheduling data:</strong> groups, tasks, shifts, assignments, availability, constraints, qualifications, home-leave settings, waitlists, swaps, requests, alerts, and schedule history.</li>
              <li><strong>Workspace data:</strong> workspace name, settings, members, invitations, ownership, billing permissions, audit activity, and configuration choices.</li>
              <li><strong>Billing data:</strong> plan, subscription status, checkout metadata, renewal/cancellation state, LemonSqueezy customer or subscription identifiers, and transaction-related webhook data. We do not store full payment card numbers.</li>
              <li><strong>Communications:</strong> support requests, feedback, bug reports, notification preferences, email delivery events, and WhatsApp/SMS delivery metadata where configured.</li>
              <li><strong>Files and exports:</strong> profile images, imported scheduling data, generated PDFs/CSVs, and other files you upload or generate.</li>
              <li><strong>Technical data:</strong> IP address, device/browser data, logs, cookies, local storage identifiers, session data, API usage, errors, diagnostics, and security events.</li>
              <li><strong>Analytics data:</strong> product usage events, page views, page leaves, feature usage, and session replay data when analytics is enabled in production.</li>
            </ul>
          </Section>

          <Section title="4. How we collect information">
            <ul className="list-disc space-y-1 pl-5">
              <li>directly from you when you register, update your profile, submit forms, upload files, or contact us;</li>
              <li>from workspace administrators who add or invite users and configure schedules;</li>
              <li>automatically through the application, API, logs, cookies, local storage, and analytics/error-monitoring tools;</li>
              <li>from payment, email, messaging, hosting, storage, and AI providers that support the service.</li>
            </ul>
          </Section>

          <Section title="5. How we use personal information">
            <p>We use personal information to:</p>
            <ul className="mt-2 list-disc space-y-1 pl-5">
              <li>create accounts, authenticate users, manage sessions, and secure access;</li>
              <li>operate workspaces, groups, schedules, assignments, self-service shifts, notifications, and exports;</li>
              <li>process subscriptions, trials, billing changes, renewals, cancellations, and invoices;</li>
              <li>send transactional messages such as verification, password reset, invitations, schedule updates, and recall notices;</li>
              <li>provide support, respond to feedback, investigate bugs, and communicate service notices;</li>
              <li>monitor reliability, detect abuse, prevent fraud, enforce access controls, and maintain audit logs;</li>
              <li>analyze and improve product usability, performance, and feature quality;</li>
              <li>comply with legal, tax, accounting, billing, security, and regulatory obligations.</li>
            </ul>
          </Section>

          <Section title="6. Legal bases for processing">
            <p>
              Where a legal basis is required, we process personal information based on contract performance, legitimate
              interests, consent, legal obligations, and protection of rights and security. You may withdraw consent where
              processing depends on consent, but this will not affect processing that happened before withdrawal.
            </p>
          </Section>

          <Section title="7. Third-party processors and service providers">
            <p>We may share necessary data with providers that help operate Shifter, including:</p>
            <ul className="mt-2 list-disc space-y-1 pl-5">
              <li><strong>LemonSqueezy:</strong> checkout, payment processing, invoices, tax handling, and subscription management.</li>
              <li><strong>SendGrid:</strong> transactional email delivery.</li>
              <li><strong>Twilio:</strong> WhatsApp or SMS notification delivery where configured.</li>
              <li><strong>PostHog:</strong> product analytics, page events, usage events, and session replay when enabled in production.</li>
              <li><strong>Sentry:</strong> error monitoring, performance traces, diagnostic context, and limited session replay for debugging.</li>
              <li><strong>Hosting, database, and storage providers:</strong> application hosting, PostgreSQL databases, backups, local or S3-compatible file storage, and infrastructure operations.</li>
              <li><strong>AI providers:</strong> AI-assisted parsing, import, summary, or recommendation features where enabled.</li>
            </ul>
            <p className="mt-2">
              These providers may process information in countries other than your own. We use them to provide the
              service and require them to protect information according to their agreements and applicable law.
            </p>
          </Section>

          <Section title="8. We do not sell personal information">
            <p>
              We do not sell personal information. We do not share personal information for third-party advertising
              networks. If that changes, we will update this Policy and provide any required choices or notices.
            </p>
          </Section>

          <Section title="9. Cookies, local storage, and similar technologies">
            <p>Shifter uses cookies, local storage, and similar technologies for:</p>
            <ul className="mt-2 list-disc space-y-1 pl-5">
              <li>authentication, token state, session continuity, and auth guards;</li>
              <li>language, theme, timezone, and app preferences;</li>
              <li>offline cache and background refresh behavior;</li>
              <li>security, abuse prevention, diagnostics, and reliability;</li>
              <li>analytics and product improvement when configured.</li>
            </ul>
            <p className="mt-2">
              You can control cookies and local storage through your browser, but disabling them may break login,
              preferences, offline access, or core app behavior.
            </p>
          </Section>

          <Section title="10. Security">
            <p>
              We use safeguards intended to protect personal information, including HTTPS/TLS, password hashing, role and
              permission checks, workspace isolation, audit logs, production error handling, restricted administrative
              access, and backups. No security system is perfect. You are responsible for using strong credentials,
              protecting your devices, and limiting access to authorized workspace users.
            </p>
          </Section>

          <Section title="11. Retention">
            <p>
              We keep personal information for as long as needed to provide Shifter, comply with legal obligations,
              resolve disputes, maintain security, prevent abuse, and enforce agreements. Typical retention periods are:
            </p>
            <ul className="mt-2 list-disc space-y-1 pl-5">
              <li>account and workspace data: while the account or workspace remains active;</li>
              <li>deleted account personal data: generally deleted or anonymized within 30 days where technically and legally feasible;</li>
              <li>security, audit, and diagnostic logs: retained as needed for security, reliability, and investigation;</li>
              <li>billing records: retained as required for tax, accounting, chargeback, and compliance purposes;</li>
              <li>backups: overwritten or deleted on normal backup cycles and may temporarily retain deleted data.</li>
            </ul>
          </Section>

          <Section title="12. Your choices and rights">
            <p>
              Depending on your location and relationship to a workspace, you may have rights to access, correct, export,
              delete, restrict, or object to processing of personal information. Some requests may need to be handled by
              your workspace administrator if they control the workspace. To exercise rights, use in-app controls where
              available or contact{" "}
              <a href={`mailto:${CONTACT_EMAIL}`} className="text-sky-600 hover:underline">{CONTACT_EMAIL}</a>.
            </p>
          </Section>

          <Section title="13. Workspace administrators">
            <p>
              Workspace administrators may access and manage data submitted by members of their workspace, including
              schedule data, availability, constraints, assignments, profile details, and activity needed to operate the
              workspace. If your organization uses Shifter, its internal policies may also apply to your use of the
              service.
            </p>
          </Section>

          <Section title="14. International users">
            <p>
              Shifter may process and store information in Israel, the United States, the European Economic Area, or other
              locations where we or our providers operate. By using Shifter, you understand that information may be
              transferred to countries with privacy laws different from those in your jurisdiction.
            </p>
          </Section>

          <Section title="15. Children">
            <p>
              Shifter is not intended for children under 16. We do not knowingly collect personal information from
              children under 16. If you believe a child provided personal information, contact us and we will take
              appropriate action.
            </p>
          </Section>

          <Section title="16. Israeli privacy law and GDPR">
            <p>
              We aim to handle personal information consistently with applicable Israeli privacy requirements and, where
              applicable, GDPR principles for users in the European Economic Area or United Kingdom. This includes
              transparency, purpose limitation, data minimization, security, and appropriate handling of access,
              correction, portability, objection, restriction, and deletion requests.
            </p>
          </Section>

          <Section title="17. Changes to this Policy">
            <p>
              We may update this Privacy Policy from time to time. If changes are material, we will provide reasonable
              notice in the application, by email, or by another appropriate method. The updated Policy will apply from the
              date shown above unless a later effective date is provided.
            </p>
          </Section>

          <Section title="18. Contact">
            <p>
              For privacy questions, requests, or complaints, contact{" "}
              <a href={`mailto:${CONTACT_EMAIL}`} className="text-sky-600 hover:underline">{CONTACT_EMAIL}</a>.
            </p>
          </Section>
        </div>

        <footer className="mt-12 border-t border-slate-200 pt-8 text-center text-xs text-slate-500 dark:border-slate-700 dark:text-slate-400">
          <div className="mb-2 flex items-center justify-center gap-4">
            <Link href="/terms" className="text-sky-600 hover:underline">Terms</Link>
            <Link href="/privacy" className="text-sky-600 hover:underline">Privacy</Link>
            <Link href="/" className="text-sky-600 hover:underline">Home</Link>
          </div>
          <p>© {new Date().getFullYear()} Ofek Labs. All rights reserved.</p>
        </footer>
      </main>
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section>
      <h2 className="mb-2 text-base font-semibold text-slate-900 dark:text-white">{title}</h2>
      {children}
    </section>
  );
}
