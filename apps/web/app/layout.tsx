import type { Metadata } from "next";
import { NextIntlClientProvider } from "next-intl";
import { getLocale, getMessages } from "next-intl/server";
import { isRtl } from "@/i18n/request";
import type { Locale } from "@/i18n/request";
import ErrorBoundary from "@/components/ErrorBoundary";
import { Providers } from "./providers";
import "./globals.css";

export const metadata: Metadata = {
  title: "Shifter",
  description: "Smart Shift Scheduling",
  icons: {
    icon: "/favicon.jpeg",
    shortcut: "/favicon.jpeg",
    apple: "/favicon.jpeg",
  },
};

export default async function RootLayout({ children }: { children: React.ReactNode }) {
  const locale = (await getLocale()) as Locale;
  const messages = await getMessages();
  const dir = isRtl(locale) ? "rtl" : "ltr";

  return (
    <html lang={locale} dir={dir}>
      <head>
        <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no, viewport-fit=cover" />
        <meta name="apple-mobile-web-app-capable" content="yes" />
        <meta name="apple-mobile-web-app-status-bar-style" content="black-translucent" />
        <meta name="theme-color" content="#0f172a" />
        <link rel="manifest" href="/manifest.json" />
      </head>
      <body>
        <Providers>
          <NextIntlClientProvider messages={messages}>
            <ErrorBoundary>
              {children}
            </ErrorBoundary>
          </NextIntlClientProvider>
        </Providers>
        {/* Crisp Live Chat — replace CRISP_WEBSITE_ID with your actual ID from crisp.chat */}
        <script
          dangerouslySetInnerHTML={{
            __html: `
              window.$crisp=[];
              window.CRISP_WEBSITE_ID="CRISP_WEBSITE_ID_PLACEHOLDER";
              (function(){var d=document;var s=d.createElement("script");s.src="https://client.crisp.chat/l.js";s.async=1;d.getElementsByTagName("head")[0].appendChild(s);})();
            `,
          }}
        />
      </body>
    </html>
  );
}
