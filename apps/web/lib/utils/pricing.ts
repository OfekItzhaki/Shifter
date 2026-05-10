/**
 * Pricing configuration — easy to update.
 * Change prices here and they reflect everywhere (pricing page, paywall, etc.)
 */

export interface PricingTier {
  id: string;
  maxMembers: number; // Infinity for unlimited
  priceMonthly: number; // in ILS (₪)
  label: string;
  labelHe: string;
}

export const PRICING_TIERS: PricingTier[] = [
  { id: "starter", maxMembers: 15, priceMonthly: 50, label: "Up to 15 members", labelHe: "עד 15 חברים" },
  { id: "growth", maxMembers: 30, priceMonthly: 90, label: "Up to 30 members", labelHe: "עד 30 חברים" },
  { id: "team", maxMembers: 60, priceMonthly: 150, label: "Up to 60 members", labelHe: "עד 60 חברים" },
  { id: "org", maxMembers: 90, priceMonthly: 250, label: "Up to 90 members", labelHe: "עד 90 חברים" },
  { id: "unlimited", maxMembers: Infinity, priceMonthly: 350, label: "Unlimited", labelHe: "ללא הגבלה" },
];

export const FREE_TRIAL_DAYS = 14;

/**
 * Get the required tier for a given member count.
 */
export function getRequiredTier(memberCount: number): PricingTier {
  return PRICING_TIERS.find(t => memberCount <= t.maxMembers) ?? PRICING_TIERS[PRICING_TIERS.length - 1];
}
