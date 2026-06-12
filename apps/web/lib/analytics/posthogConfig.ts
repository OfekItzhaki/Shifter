export function isPostHogEnabled(key: string | undefined, nodeEnv: string | undefined): boolean {
  return Boolean(key?.trim()) && nodeEnv === "production";
}
