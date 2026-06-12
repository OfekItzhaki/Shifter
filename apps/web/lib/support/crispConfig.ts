export function getConfiguredCrispWebsiteId(value: string | undefined): string | null {
  const websiteId = value?.trim();
  return websiteId ? websiteId : null;
}
