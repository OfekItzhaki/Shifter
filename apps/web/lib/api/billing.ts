import axios from "axios";
import { apiClient } from "./client";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

/** Unauthenticated axios instance for public endpoints (no token, no interceptors). */
const publicClient = axios.create({
  baseURL: API_URL,
  headers: { "Content-Type": "application/json" },
});

// ── Types ─────────────────────────────────────────────────────────────────────

export interface PlanDto {
  variantId: string;
  name: string;
  priceInCents: number;
  interval: string;
  description: string | null;
  sortOrder: number;
}

export interface SpaceSubscriptionDto {
  status: "trialing" | "active" | "past_due" | "canceled" | "expired";
  tierId: string | null;
  trialStartsAt: string | null;
  trialEndsAt: string | null;
  currentPeriodStart: string | null;
  currentPeriodEnd: string | null;
  canceledAt: string | null;
  autoRenew: boolean;
  isActive: boolean;
  daysRemaining: number | null;
}

export interface CheckoutResponse {
  checkoutUrl: string;
}

// ── API Functions ─────────────────────────────────────────────────────────────

export async function getPlans(): Promise<PlanDto[]> {
  const { data } = await publicClient.get("/billing/plans");
  return data as PlanDto[];
}

export async function getSpaceSubscription(
  spaceId: string
): Promise<SpaceSubscriptionDto | null> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/billing/subscription`
  );
  return data as SpaceSubscriptionDto | null;
}

export async function createSpaceCheckout(
  spaceId: string,
  variantId?: string
): Promise<CheckoutResponse> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/billing/checkout`,
    { variantId },
    { _skipRedirect: true } as any
  );
  return data as CheckoutResponse;
}

export async function cancelSpaceSubscription(
  spaceId: string
): Promise<void> {
  await apiClient.post(`/spaces/${spaceId}/billing/cancel`);
}

export async function renewSpaceSubscription(
  spaceId: string
): Promise<void> {
  await apiClient.post(`/spaces/${spaceId}/billing/renew`);
}

export async function upgradeSpacePlan(
  spaceId: string,
  variantId: string
): Promise<CheckoutResponse> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/billing/upgrade`,
    { variantId }
  );
  return data as CheckoutResponse;
}
