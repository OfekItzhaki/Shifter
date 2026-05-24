/**
 * Unit tests for SpaceBillingCard component.
 *
 * Verifies:
 * - Correct date display per subscription status (Req 4.1, 4.2, 4.3, 4.4)
 * - "No subscription found" message when subscription is null (Req 4.5)
 * - Permission gating hides section for non-admins (Req 4.6)
 * - Error state shows retry button on API failure
 *
 * Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";
import SpaceBillingCard from "../../components/billing/SpaceBillingCard";
import type { SpaceSubscriptionDto } from "@/lib/api/billing";

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockGetSpaceSubscription = vi.hoisted(() => vi.fn());

vi.mock("@/lib/api/billing", () => ({
  getSpaceSubscription: mockGetSpaceSubscription,
  createSpaceCheckout: vi.fn().mockResolvedValue({ checkoutUrl: "https://checkout.example.com" }),
  cancelSpaceSubscription: vi.fn().mockResolvedValue(undefined),
  renewSpaceSubscription: vi.fn().mockResolvedValue(undefined),
}));

// ── Test Data ─────────────────────────────────────────────────────────────────

const trialingSubscription: SpaceSubscriptionDto = {
  status: "trialing",
  tierId: "trial",
  trialStartsAt: "2025-01-01T00:00:00Z",
  trialEndsAt: "2025-01-15T00:00:00Z",
  currentPeriodStart: null,
  currentPeriodEnd: null,
  canceledAt: null,
  autoRenew: true,
  isActive: true,
  daysRemaining: 10,
};

const activeSubscription: SpaceSubscriptionDto = {
  status: "active",
  tierId: "pro",
  trialStartsAt: "2025-01-01T00:00:00Z",
  trialEndsAt: "2025-01-15T00:00:00Z",
  currentPeriodStart: "2025-01-15T00:00:00Z",
  currentPeriodEnd: "2025-02-15T00:00:00Z",
  canceledAt: null,
  autoRenew: true,
  isActive: true,
  daysRemaining: null,
};

const canceledSubscription: SpaceSubscriptionDto = {
  status: "canceled",
  tierId: "pro",
  trialStartsAt: "2025-01-01T00:00:00Z",
  trialEndsAt: "2025-01-15T00:00:00Z",
  currentPeriodStart: "2025-01-15T00:00:00Z",
  currentPeriodEnd: "2025-02-15T00:00:00Z",
  canceledAt: "2025-02-01T00:00:00Z",
  autoRenew: false,
  isActive: false,
  daysRemaining: null,
};

// ── Test Suite ────────────────────────────────────────────────────────────────

describe("SpaceBillingCard (Task 14.3)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("Req 4.6: Permission gating hides section for non-admins", () => {
    it("renders nothing when hasBillingPermission is false", () => {
      const { container } = render(
        <SpaceBillingCard spaceId="space-1" hasBillingPermission={false} />
      );

      expect(container.innerHTML).toBe("");
    });

    it("does not call getSpaceSubscription when hasBillingPermission is false", () => {
      render(
        <SpaceBillingCard spaceId="space-1" hasBillingPermission={false} />
      );

      expect(mockGetSpaceSubscription).not.toHaveBeenCalled();
    });
  });

  describe("Req 4.2: Trialing status shows trial start/end dates", () => {
    it("displays trial start and end dates in YYYY-MM-DD format", async () => {
      mockGetSpaceSubscription.mockResolvedValue(trialingSubscription);

      render(
        <SpaceBillingCard spaceId="space-1" hasBillingPermission={true} />
      );

      await waitFor(() => {
        expect(screen.getByText("2025-01-01")).toBeInTheDocument();
      });
      expect(screen.getByText("2025-01-15")).toBeInTheDocument();
      expect(screen.getByText("Trial Start")).toBeInTheDocument();
      expect(screen.getByText("Trial End")).toBeInTheDocument();
    });

    it("displays the Trialing status badge", async () => {
      mockGetSpaceSubscription.mockResolvedValue(trialingSubscription);

      render(
        <SpaceBillingCard spaceId="space-1" hasBillingPermission={true} />
      );

      await waitFor(() => {
        expect(screen.getByText("Trialing")).toBeInTheDocument();
      });
    });

    it("displays days remaining when available", async () => {
      mockGetSpaceSubscription.mockResolvedValue(trialingSubscription);

      render(
        <SpaceBillingCard spaceId="space-1" hasBillingPermission={true} />
      );

      await waitFor(() => {
        expect(screen.getByText("Days Remaining")).toBeInTheDocument();
        expect(screen.getByText("10")).toBeInTheDocument();
      });
    });
  });

  describe("Req 4.3: Active status shows period start/end dates", () => {
    it("displays period start and end dates in YYYY-MM-DD format", async () => {
      mockGetSpaceSubscription.mockResolvedValue(activeSubscription);

      render(
        <SpaceBillingCard spaceId="space-1" hasBillingPermission={true} />
      );

      await waitFor(() => {
        expect(screen.getByText("2025-01-15")).toBeInTheDocument();
      });
      expect(screen.getByText("2025-02-15")).toBeInTheDocument();
      expect(screen.getByText("Period Start")).toBeInTheDocument();
      expect(screen.getByText("Period End")).toBeInTheDocument();
    });

    it("displays the Active status badge", async () => {
      mockGetSpaceSubscription.mockResolvedValue(activeSubscription);

      render(
        <SpaceBillingCard spaceId="space-1" hasBillingPermission={true} />
      );

      await waitFor(() => {
        expect(screen.getByText("Active")).toBeInTheDocument();
      });
    });
  });

  describe("Req 4.4: Canceled status shows cancellation date and access expiry", () => {
    it("displays cancellation date and access expiry date in YYYY-MM-DD format", async () => {
      mockGetSpaceSubscription.mockResolvedValue(canceledSubscription);

      render(
        <SpaceBillingCard spaceId="space-1" hasBillingPermission={true} />
      );

      await waitFor(() => {
        expect(screen.getByText("2025-02-01")).toBeInTheDocument();
      });
      expect(screen.getByText("2025-02-15")).toBeInTheDocument();
      expect(screen.getByText("Cancellation Date")).toBeInTheDocument();
      expect(screen.getByText("Access Expires")).toBeInTheDocument();
    });

    it("displays the Canceled status badge", async () => {
      mockGetSpaceSubscription.mockResolvedValue(canceledSubscription);

      render(
        <SpaceBillingCard spaceId="space-1" hasBillingPermission={true} />
      );

      await waitFor(() => {
        expect(screen.getByText("Canceled")).toBeInTheDocument();
      });
    });
  });

  describe("Req 4.5: No subscription shows appropriate message", () => {
    it("displays 'No subscription found' when subscription is null", async () => {
      mockGetSpaceSubscription.mockResolvedValue(null);

      render(
        <SpaceBillingCard spaceId="space-1" hasBillingPermission={true} />
      );

      await waitFor(() => {
        expect(
          screen.getByText("No subscription found for this space.")
        ).toBeInTheDocument();
      });
    });
  });

  describe("Error state shows retry button", () => {
    it("displays error message and retry button on API failure", async () => {
      mockGetSpaceSubscription.mockRejectedValue(new Error("Network error"));

      render(
        <SpaceBillingCard spaceId="space-1" hasBillingPermission={true} />
      );

      await waitFor(() => {
        expect(
          screen.getByText("Could not load billing information.")
        ).toBeInTheDocument();
      });
      expect(screen.getByText("Retry")).toBeInTheDocument();
    });

    it("retries fetching subscription when retry button is clicked", async () => {
      mockGetSpaceSubscription.mockRejectedValueOnce(new Error("Network error"));
      mockGetSpaceSubscription.mockResolvedValueOnce(activeSubscription);

      render(
        <SpaceBillingCard spaceId="space-1" hasBillingPermission={true} />
      );

      await waitFor(() => {
        expect(screen.getByText("Retry")).toBeInTheDocument();
      });

      await act(async () => {
        fireEvent.click(screen.getByText("Retry"));
      });

      await waitFor(() => {
        expect(screen.getByText("Active")).toBeInTheDocument();
      });
      expect(mockGetSpaceSubscription).toHaveBeenCalledTimes(2);
    });
  });
});
