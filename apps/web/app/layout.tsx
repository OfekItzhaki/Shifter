import type { Metadata } from "next";
import { NextIntlClientProvider } from "next-intl";
import { getLocale, getMessages } from "next-intl/server";
import { isRtl } from "@/i18n/request";
import type { Locale } from "@/i18n/request";
import ErrorBoundary from "@/components/ErrorBoundary";
import { Providers } from "./providers";
import "./globals.css";

export const metadata: Metadata = {
  title: "Jobuler",
  description: "Force Scheduling System",
};

export default async function RootLayout({ children }: { children: React.ReactNode }) {
  const locale = (await getLocale()) as Locale;
  const messages = await getMessages();
  const dir = isRtl(locale) ? "rtl" : "ltr";

  return (
    <html lang={locale} dir={dir}>
      <body>
        <Providers>
          <NextIntlClientProvider messages={messages}>
            <ErrorBoundary>
              {children}
            </ErrorBoundary>
          </NextIntlClientProvider>
        </Providers>
      </body>
    </html>
  );
}
