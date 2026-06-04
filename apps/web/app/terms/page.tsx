import Link from "next/link";
import ShifterLogo from "@/components/shell/ShifterLogo";

const CONTACT_EMAIL = process.env.NEXT_PUBLIC_LEGAL_EMAIL ?? "support@ofeklabs.com";
const LAST_UPDATED = "June 4, 2026";

export default function TermsPage() {
  return (
    <div className="min-h-screen bg-white dark:bg-slate-900">
      <header className="border-b border-slate-100 dark:border-slate-800 px-6 py-4">
        <div className="mx-auto flex max-w-3xl items-center justify-between">
          <Link href="/" className="flex items-center gap-2 text-slate-900 transition-colors hover:text-sky-600 dark:text-white">
            <ShifterLogo size={24} />
            <span className="text-sm font-bold">Shifter</span>
          </Link>
          <Link href="/privacy" className="text-sm text-sky-600 hover:underline">
            Privacy Policy
          </Link>
        </div>
      </header>

      <main className="mx-auto max-w-3xl px-6 py-12" dir="ltr">
        <p className="mb-3 text-xs font-semibold uppercase tracking-[0.18em] text-sky-600">Legal</p>
        <h1 className="mb-2 text-3xl font-bold text-slate-900 dark:text-white">Terms of Service</h1>
        <p className="mb-8 text-sm text-slate-500 dark:text-slate-400">Last updated: {LAST_UPDATED}</p>

        <div className="mb-8 rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm leading-relaxed text-amber-900 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-200">
          <p className="font-semibold">Important scheduling notice</p>
          <p className="mt-1">
            Shifter helps teams create and manage schedules, but it does not replace human review. You are responsible for
            verifying staffing, legal requirements, safety requirements, rest rules, emergency coverage, and operational
            fitness before using or publishing a schedule.
          </p>
        </div>

        <div className="space-y-7 text-sm leading-relaxed text-slate-700 dark:text-slate-300">
          <Section title="1. Who we are">
            <p>
              These Terms of Service govern your access to and use of Shifter, a shift scheduling and workforce
              coordination service operated by Ofek Labs ("Ofek Labs", "we", "us", or "our"). By accessing or using
              Shifter, creating an account, joining a workspace, or purchasing a subscription, you agree to these Terms.
            </p>
          </Section>

          <Section title="2. The service">
            <p>
              Shifter provides tools for workspace management, team and group management, shift planning, automated
              scheduling, availability and constraint collection, self-service scheduling, notifications, exports, billing
              management, analytics for operators, and related support features. We may add, change, suspend, or remove
              features from time to time.
            </p>
          </Section>

          <Section title="3. Accounts and eligibility">
            <ul className="list-disc space-y-1 pl-5">
              <li>You must provide accurate account information and keep it current.</li>
              <li>You are responsible for maintaining the confidentiality of your credentials and passkeys.</li>
              <li>You are responsible for activity performed through your account, workspace, or administrator role.</li>
              <li>You must promptly notify us if you suspect unauthorized account access.</li>
              <li>The service is intended for users who are at least 16 years old.</li>
            </ul>
          </Section>

          <Section title="4. Workspaces, administrators, and users">
            <p>
              A workspace owner or administrator controls the workspace, invites users, assigns roles, configures billing,
              manages schedules, and determines what data is entered into the service. If you use Shifter on behalf of an
              organization, you represent that you have authority to bind that organization to these Terms. Workspace
              administrators are responsible for obtaining any permissions or notices required before adding employee,
              volunteer, contractor, or team-member data to Shifter.
            </p>
          </Section>

          <Section title="5. Acceptable use">
            <p>You may not use Shifter to:</p>
            <ul className="mt-2 list-disc space-y-1 pl-5">
              <li>break applicable laws, employment rules, privacy rules, or safety rules;</li>
              <li>upload unlawful, harmful, discriminatory, abusive, or infringing content;</li>
              <li>attempt to access another user, workspace, tenant, system, or database without permission;</li>
              <li>interfere with, reverse engineer, scrape, overload, or disrupt the service;</li>
              <li>remove security controls, rate limits, audit trails, or access controls;</li>
              <li>use the service to make fully automated decisions without appropriate human review.</li>
            </ul>
          </Section>

          <Section title="6. Scheduling results and operational decisions">
            <p>
              Shifter may generate recommendations, drafts, alerts, statistics, exports, and automatic schedules. These
              outputs can be incomplete, inaccurate, delayed, or unsuitable for a specific operational environment. You
              must review all generated schedules before relying on them. We are not responsible for missed shifts,
              understaffing, overstaffing, labor-law violations, fatigue issues, safety incidents, loss of revenue,
              discipline issues, or other consequences arising from schedules or recommendations.
            </p>
          </Section>

          <Section title="7. AI-assisted features">
            <p>
              Some Shifter features may use AI or automated parsing to help import data, summarize information, parse
              constraints, or suggest actions. AI output may be wrong or incomplete. You are responsible for reviewing and
              approving AI-assisted output before using it in operational workflows.
            </p>
          </Section>

          <Section title="8. User content and data">
            <p>
              You retain ownership of the names, schedules, availability, constraints, files, notes, feedback, and other
              content that you or your workspace users submit to Shifter ("Customer Data"). You grant us a limited license
              to host, process, transmit, display, back up, and otherwise use Customer Data as needed to provide, secure,
              support, and improve the service. You represent that you have the rights and permissions needed to submit
              Customer Data to Shifter.
            </p>
          </Section>

          <Section title="9. Subscriptions, trials, billing, and taxes">
            <p>
              Paid plans, trials, renewals, upgrades, downgrades, and cancellations may be managed through LemonSqueezy or
              another payment processor. Prices, plan limits, trial periods, and included features are shown in the
              product or checkout flow. Unless stated otherwise, subscriptions renew automatically until canceled. You are
              responsible for taxes, payment details, and billing permissions. Cancellation generally takes effect at the
              end of the current billing period unless the checkout or billing portal states otherwise.
            </p>
          </Section>

          <Section title="10. Refunds">
            <p>
              Except where required by law or expressly stated in the checkout flow, payments are non-refundable and we do
              not provide credits for partial subscription periods, unused seats, unused workspace capacity, or unused
              features. If you believe a charge was made in error, contact us promptly at{" "}
              <a href={`mailto:${CONTACT_EMAIL}`} className="text-sky-600 hover:underline">{CONTACT_EMAIL}</a>.
            </p>
          </Section>

          <Section title="11. Third-party services">
            <p>
              Shifter may interoperate with third-party providers for payments, email, WhatsApp or SMS delivery, error
              monitoring, analytics, file storage, hosting, AI assistance, and infrastructure. Third-party services are
              governed by their own terms and policies. We are not responsible for third-party outages, policy changes, or
              processing outside our control.
            </p>
          </Section>

          <Section title="12. Intellectual property">
            <p>
              Shifter, including its software, design, workflows, branding, logos, documentation, and algorithms, is owned
              by Ofek Labs or its licensors. These Terms do not transfer any intellectual-property rights to you. You may
              not copy, modify, distribute, sell, lease, or create derivative works from the service except as expressly
              permitted by us in writing.
            </p>
          </Section>

          <Section title="13. Privacy and security">
            <p>
              Our Privacy Policy explains how we collect, use, store, and share personal information. We use reasonable
              administrative, technical, and organizational safeguards, but no service can be guaranteed to be perfectly
              secure or continuously available.
            </p>
          </Section>

          <Section title="14. Service availability and changes">
            <p>
              We aim to keep Shifter reliable, but we do not guarantee uninterrupted, error-free, or permanent access. The
              service may be unavailable because of maintenance, deployment, security events, third-party outages, internet
              issues, force majeure events, or other causes. We may modify, suspend, or discontinue features or the entire
              service where reasonably necessary.
            </p>
          </Section>

          <Section title="15. Suspension and termination">
            <p>
              You may stop using Shifter at any time. We may suspend or terminate access if you violate these Terms,
              create security or legal risk, fail to pay amounts due, misuse the service, or if we are required to do so by
              law. After termination, we may retain data as described in the Privacy Policy and as required for legal,
              security, backup, billing, and audit purposes.
            </p>
          </Section>

          <Section title="16. Disclaimers">
            <p>
              To the fullest extent permitted by law, Shifter is provided "as is" and "as available" without warranties of
              any kind, whether express, implied, statutory, or otherwise. We disclaim warranties of merchantability,
              fitness for a particular purpose, non-infringement, accuracy, availability, and error-free operation.
            </p>
          </Section>

          <Section title="17. Limitation of liability">
            <p>
              To the fullest extent permitted by law, Ofek Labs will not be liable for indirect, incidental, special,
              consequential, exemplary, or punitive damages, or for lost profits, lost revenue, lost data, business
              interruption, staffing failures, schedule errors, or operational losses. Our total liability for any claim
              relating to the service is limited to the amount you paid to us for the service during the three months
              before the event giving rise to the claim, or USD 100 if no amount was paid.
            </p>
          </Section>

          <Section title="18. Indemnity">
            <p>
              You agree to defend, indemnify, and hold harmless Ofek Labs from claims, damages, liabilities, losses, and
              expenses arising from your use of Shifter, your Customer Data, your violation of these Terms, your violation
              of law, or your infringement of third-party rights.
            </p>
          </Section>

          <Section title="19. Governing law and venue">
            <p>
              These Terms are governed by the laws of the State of Israel, without regard to conflict-of-law rules. The
              competent courts in Israel will have exclusive jurisdiction over disputes arising from or relating to these
              Terms or the service, unless applicable law requires otherwise.
            </p>
          </Section>

          <Section title="20. Changes to these Terms">
            <p>
              We may update these Terms from time to time. If changes are material, we will provide reasonable notice in
              the application, by email, or by another appropriate method. Your continued use after changes take effect
              means you accept the updated Terms.
            </p>
          </Section>

          <Section title="21. Contact">
            <p>
              For questions about these Terms, contact us at{" "}
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
