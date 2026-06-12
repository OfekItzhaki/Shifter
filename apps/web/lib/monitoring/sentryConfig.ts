export function isSentryEnabled(dsn: string | undefined, nodeEnv: string | undefined): boolean {
  return Boolean(dsn?.trim()) && nodeEnv === "production";
}
