import Link from "next/link";
import { getLocale } from "next-intl/server";
import ShifterLogo from "@/components/shell/ShifterLogo";
import {
  getLegalContent,
  legalDir,
  LEGAL_LAST_UPDATED,
  type LegalPageKey,
} from "@/lib/legal/legalContent";

interface LegalPageProps {
  page: LegalPageKey;
}

export default async function LegalPage({ page }: LegalPageProps) {
  const locale = await getLocale();
  const content = getLegalContent(page, locale);
  const dir = legalDir(locale);
  const relatedHref = page === "terms" ? "/privacy" : page === "privacy" ? "/terms" : "/privacy";

  return (
    <div className="min-h-screen bg-white dark:bg-slate-900">
      <header className="border-b border-slate-100 px-6 py-4 dark:border-slate-800">
        <div className="mx-auto flex max-w-3xl items-center justify-between gap-4">
          <Link href="/" className="flex items-center gap-2 text-slate-900 transition-colors hover:text-sky-600 dark:text-white">
            <ShifterLogo size={24} />
            <span className="text-sm font-bold">Shifter</span>
          </Link>
          <Link href={relatedHref} className="text-sm text-sky-600 hover:underline">
            {content.relatedLinkLabel}
          </Link>
        </div>
      </header>

      <main className="mx-auto max-w-3xl px-6 py-12" dir={dir}>
        <p className="mb-3 text-xs font-semibold uppercase tracking-[0.18em] text-sky-600">
          {content.eyebrow}
        </p>
        <h1 className="mb-2 text-3xl font-bold text-slate-900 dark:text-white">{content.title}</h1>
        <p className="mb-8 text-sm text-slate-500 dark:text-slate-400">
          {content.lastUpdatedLabel}: {LEGAL_LAST_UPDATED}
        </p>

        <div className="mb-8 rounded-lg border border-sky-200 bg-sky-50 p-4 text-sm leading-relaxed text-sky-900 dark:border-sky-800 dark:bg-sky-950/30 dark:text-sky-200">
          <p className="font-semibold">{content.calloutTitle}</p>
          <p className="mt-1">{content.calloutBody}</p>
        </div>

        <div className="space-y-7 text-sm leading-relaxed text-slate-700 dark:text-slate-300">
          {content.sections.map((section) => (
            <section key={section.title}>
              <h2 className="mb-2 text-base font-semibold text-slate-900 dark:text-white">{section.title}</h2>
              {section.paragraphs?.map((paragraph) => (
                <p key={paragraph} className="mb-2 last:mb-0">
                  {paragraph}
                </p>
              ))}
              {section.bullets && (
                <ul className={`${dir === "rtl" ? "pr-5" : "pl-5"} list-disc space-y-1`}>
                  {section.bullets.map((bullet) => (
                    <li key={bullet}>{bullet}</li>
                  ))}
                </ul>
              )}
            </section>
          ))}
        </div>

        <footer className="mt-12 border-t border-slate-200 pt-8 text-center text-xs text-slate-500 dark:border-slate-700 dark:text-slate-400">
          <div className="mb-2 flex items-center justify-center gap-4">
            <Link href="/terms" className="text-sky-600 hover:underline">
              {page === "terms" ? content.title : getLegalContent("terms", locale).title}
            </Link>
            <Link href="/privacy" className="text-sky-600 hover:underline">
              {page === "privacy" ? content.title : getLegalContent("privacy", locale).title}
            </Link>
            <Link href="/subprocessors" className="text-sky-600 hover:underline">
              {page === "subprocessors" ? content.title : getLegalContent("subprocessors", locale).title}
            </Link>
            <Link href="/dpa" className="text-sky-600 hover:underline">
              {page === "dpa" ? content.title : getLegalContent("dpa", locale).title}
            </Link>
            <Link href="/privacy-requests" className="text-sky-600 hover:underline">
              {page === "privacyRequests" ? content.title : getLegalContent("privacyRequests", locale).title}
            </Link>
            <Link href="/" className="text-sky-600 hover:underline">
              Shifter
            </Link>
          </div>
          <p>© {new Date().getFullYear()} Ofek Labs. All rights reserved.</p>
        </footer>
      </main>
    </div>
  );
}
