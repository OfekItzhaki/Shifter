import type { Metadata } from "next";
import { NextIntlClientProvider } from "next-intl";
import { getLocale, getMessages } from "next-intl/server";
import { isRtl } from "@/i18n/request";
import type { Locale } from "@/i18n/request";
import ErrorBoundary from "@/components/ErrorBoundary";
import CrispChat from "@/components/shell/CrispChat";
import { getConfiguredCrispWebsiteId } from "@/lib/support/crispConfig";
import { Providers } from "./providers";
import "./globals.css";

export const metadata: Metadata = {
  metadataBase: new URL("https://shifter.ofeklabs.com"),
  title: {
    default: "Shifter | Smart Shift Scheduling",
    template: "%s | Shifter",
  },
  description: "Automatic, fair shift scheduling for teams. No spreadsheets, no headaches. Works for military, security, hospitals, restaurants and more.",
  keywords: ["shift scheduling", "roster", "workforce management", "military scheduling", "fair shifts", "automatic scheduling"],
  authors: [{ name: "Shifter" }],
  openGraph: {
    type: "website",
    locale: "en_US",
    url: "https://shifter.ofeklabs.com",
    siteName: "Shifter",
    title: "Shifter | Smart Shift Scheduling",
    description: "Automatic, fair shift scheduling for teams. No spreadsheets, no headaches.",
    images: [{ url: "/shifter_full_logo.png", width: 1059, height: 294, alt: "Shifter Logo" }],
  },
  twitter: {
    card: "summary",
    title: "Shifter | Smart Shift Scheduling",
    description: "Automatic, fair shift scheduling for teams.",
  },
  robots: { index: true, follow: true },
  icons: {
    icon: [
      { url: "/shifter_favicon16.png", sizes: "16x16", type: "image/png" },
      { url: "/shifter_favicon32.png", sizes: "32x32", type: "image/png" },
    ],
    shortcut: "/shifter_favicon32.png",
    apple: "/shifter_icon.png",
  },
};

export default async function RootLayout({ children }: { children: React.ReactNode }) {
  const locale = (await getLocale()) as Locale;
  const messages = await getMessages();
  const dir = isRtl(locale) ? "rtl" : "ltr";
  const crispWebsiteId = getConfiguredCrispWebsiteId(process.env.NEXT_PUBLIC_CRISP_WEBSITE_ID);

  return (
    <html lang={locale} dir={dir}>
      <head>
        <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no, viewport-fit=cover" />
        <meta name="mobile-web-app-capable" content="yes" />
        <meta name="apple-mobile-web-app-status-bar-style" content="black-translucent" />
        <meta name="theme-color" content="#0f172a" />
        <link rel="manifest" href="/manifest.json" />
      </head>
      <body>
        <NextIntlClientProvider messages={messages}>
          <Providers>
            <ErrorBoundary>
              {children}
            </ErrorBoundary>
          </Providers>
        </NextIntlClientProvider>
        {crispWebsiteId && <CrispChat websiteId={crispWebsiteId} />}
      </body>
    </html>
  );
}
