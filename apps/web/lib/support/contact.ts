export const DEFAULT_SUPPORT_EMAIL = "support@ofeklabs.com";

export function getConfiguredSupportEmail(value = process.env.NEXT_PUBLIC_LEGAL_EMAIL): string {
  const trimmed = value?.trim();
  return trimmed || DEFAULT_SUPPORT_EMAIL;
}

export function buildSupportMailtoHref(
  subject: string,
  email = getConfiguredSupportEmail()
): string {
  const encodedSubject = encodeURIComponent(subject);
  return `mailto:${email}?subject=${encodedSubject}`;
}
