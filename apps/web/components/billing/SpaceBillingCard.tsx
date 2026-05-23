"use client";

import { useEffect, useState, useCallback } from "react";
import {
  getSpaceSubscription,
  createSpaceCheckout,
  cancelSpaceSubscription,
  renewSpaceSubscription,
  SpaceSubscriptionDto,
} from "@/lib/api/billing";

interface Props {
  spaceId: string;
  /** Only render the card if the user has BillingManage permission (space owner) */
  hasBillingPermission: boolean;
}

type LoadingState = "loading" | "loaded" | "error";

/**
 * Displays subscription status and date information on the space settings page.
 * Permission-gated: only visible to users with BillingManage permission.
 */
export default function SpaceBillingCard({ spaceId, hasBillingPermission }: Props) {
  const [subscription, setSubscription] = useState<SpaceSubscriptionDto | null>(null);
  const [loadingState, setLoadingState] = useState<LoadingState>("loading");

  const fetchSubscription = useCallback(async () => {
    if (!spaceId) return;
    setLoadingState("loading");
    try {
      const data = await getSpaceSubscription(spaceId);
      setSubscription(data);
      setLoadingState("loaded");
    } catch {
      setLoadingState("error");
    }
  }, [spaceId]);

  useEffect(() => {
    if (hasBillingPermission) {
      fetchSubscription();
    }
  }, [hasBillingPermission, fetchSubscription]);

  // Permission gate: hide entirely for non-billing users
  if (!hasBillingPermission) return null;

  if (loadingState === "loading") {
    return (
      <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-900 dark:text-white mb-3">
          Subscription
        </h2>
        <div className="flex items-center justify-center py-4 text-slate-500 dark:text-slate-400 text-sm">
          Loading...
        </div>
      </div>
    );
  }

  if (loadingState === "error") {
    return (
      <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-900 dark:text-white mb-3">
          Subscription
        </h2>
        <div className="flex flex-col items-center gap-3 py-4">
          <p className="text-sm text-slate-500 dark:text-slate-400">
            Could not load billing information.
          </p>
          <button
            onClick={fetchSubscription}
            className="px-4 py-2 rounded-lg bg-sky-500 hover:bg-sky-600 text-white font-semibold text-sm transition-colors"
          >
            Retry
          </button>
        </div>
      </div>
    );
  }

  // No subscription exists
  if (!subscription) {
    return (
      <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-900 dark:text-white mb-3">
          Subscription
        </h2>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          No subscription found for this space.
        </p>
      </div>
    );
  }

  return (
    <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-sm font-semibold text-slate-900 dark:text-white">
          Subscription
        </h2>
        <StatusBadge status={subscription.status} />
      </div>

      <SubscriptionDetails subscription={subscription} />

      <ActionButtons
        spaceId={spaceId}
        status={subscription.status}
        onSubscriptionChange={fetchSubscription}
      />
    </div>
  );
}

// ── Status Badge ──────────────────────────────────────────────────────────────

function StatusBadge({ status }: { status: SpaceSubscriptionDto["status"] }) {
  const config = getStatusBadgeConfig(status);

  return (
    <span className={`text-xs font-medium px-2.5 py-1 rounded-full border ${config.classes}`}>
      {config.label}
    </span>
  );
}

function getStatusBadgeConfig(status: SpaceSubscriptionDto["status"]) {
  switch (status) {
    case "trialing":
      return {
        label: "Trialing",
        classes: "bg-sky-50 dark:bg-sky-900/20 text-sky-700 dark:text-sky-300 border-sky-200 dark:border-sky-700",
      };
    case "active":
      return {
        label: "Active",
        classes: "bg-green-50 dark:bg-green-900/20 text-green-700 dark:text-green-300 border-green-200 dark:border-green-700",
      };
    case "past_due":
      return {
        label: "Past Due",
        classes: "bg-amber-50 dark:bg-amber-900/20 text-amber-700 dark:text-amber-300 border-amber-200 dark:border-amber-700",
      };
    case "canceled":
      return {
        label: "Canceled",
        classes: "bg-orange-50 dark:bg-orange-900/20 text-orange-700 dark:text-orange-300 border-orange-200 dark:border-orange-700",
      };
    case "expired":
      return {
        label: "Expired",
        classes: "bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-300 border-slate-200 dark:border-slate-600",
      };
    default:
      return {
        label: status,
        classes: "bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-300 border-slate-200 dark:border-slate-600",
      };
  }
}

// ── Subscription Details ──────────────────────────────────────────────────────

function SubscriptionDetails({ subscription }: { subscription: SpaceSubscriptionDto }) {
  switch (subscription.status) {
    case "trialing":
      return <TrialingDetails subscription={subscription} />;
    case "active":
      return <ActiveDetails subscription={subscription} />;
    case "canceled":
      return <CanceledDetails subscription={subscription} />;
    case "past_due":
      return <PastDueDetails subscription={subscription} />;
    case "expired":
      return <ExpiredDetails />;
    default:
      return null;
  }
}

function TrialingDetails({ subscription }: { subscription: SpaceSubscriptionDto }) {
  return (
    <div className="space-y-2">
      <DetailRow label="Trial Start" value={formatDate(subscription.trialStartsAt)} />
      <DetailRow label="Trial End" value={formatDate(subscription.trialEndsAt)} />
      {subscription.daysRemaining !== null && (
        <DetailRow label="Days Remaining" value={String(subscription.daysRemaining)} />
      )}
    </div>
  );
}

function ActiveDetails({ subscription }: { subscription: SpaceSubscriptionDto }) {
  return (
    <div className="space-y-2">
      <DetailRow label="Period Start" value={formatDate(subscription.currentPeriodStart)} />
      <DetailRow label="Period End" value={formatDate(subscription.currentPeriodEnd)} />
    </div>
  );
}

function CanceledDetails({ subscription }: { subscription: SpaceSubscriptionDto }) {
  return (
    <div className="space-y-2">
      <DetailRow label="Cancellation Date" value={formatDate(subscription.canceledAt)} />
      <DetailRow label="Access Expires" value={formatDate(subscription.currentPeriodEnd)} />
    </div>
  );
}

function PastDueDetails({ subscription }: { subscription: SpaceSubscriptionDto }) {
  return (
    <div className="space-y-2">
      <DetailRow label="Period Start" value={formatDate(subscription.currentPeriodStart)} />
      <DetailRow label="Period End" value={formatDate(subscription.currentPeriodEnd)} />
      <p className="text-xs text-amber-600 dark:text-amber-400 mt-1">
        Payment is past due. Please update your payment method.
      </p>
    </div>
  );
}

function ExpiredDetails() {
  return (
    <p className="text-sm text-slate-500 dark:text-slate-400">
      Your subscription has expired. Renew to regain access to premium features.
    </p>
  );
}

// ── Action Buttons ────────────────────────────────────────────────────────────

type ActionType = "upgrade" | "cancel" | "renew";

interface ActionButtonsProps {
  spaceId: string;
  status: SpaceSubscriptionDto["status"];
  onSubscriptionChange: () => void;
}

function ActionButtons({ spaceId, status, onSubscriptionChange }: ActionButtonsProps) {
  const [loadingAction, setLoadingAction] = useState<ActionType | null>(null);
  const [error, setError] = useState<string | null>(null);

  const showUpgrade = status === "trialing" || status === "active";
  const showCancel = status === "active" || status === "trialing";
  const showRenew = status === "canceled" || status === "expired";

  if (!showUpgrade && !showCancel && !showRenew) return null;

  const handleUpgrade = async () => {
    setLoadingAction("upgrade");
    setError(null);
    try {
      const { checkoutUrl } = await createSpaceCheckout(spaceId);
      window.location.href = checkoutUrl;
    } catch {
      setError("Could not create checkout. Please try again.");
    } finally {
      setLoadingAction(null);
    }
  };

  const handleCancel = async () => {
    setLoadingAction("cancel");
    setError(null);
    try {
      await cancelSpaceSubscription(spaceId);
      onSubscriptionChange();
    } catch {
      setError("Could not cancel subscription. Please try again.");
    } finally {
      setLoadingAction(null);
    }
  };

  const handleRenew = async () => {
    setLoadingAction("renew");
    setError(null);
    try {
      await renewSpaceSubscription(spaceId);
      onSubscriptionChange();
    } catch {
      setError("Could not renew subscription. Please try again.");
    } finally {
      setLoadingAction(null);
    }
  };

  const isLoading = loadingAction !== null;

  return (
    <>
      <div className="flex items-center gap-2 mt-4 pt-4 border-t border-slate-100 dark:border-slate-700">
        {showUpgrade && (
          <button
            onClick={handleUpgrade}
            disabled={isLoading}
            className="bg-sky-500 hover:bg-sky-600 text-white text-sm font-medium px-4 py-2 rounded-lg disabled:opacity-50 transition-colors"
          >
            {loadingAction === "upgrade" ? "Loading…" : "Upgrade"}
          </button>
        )}
        {showRenew && (
          <button
            onClick={handleRenew}
            disabled={isLoading}
            className="bg-sky-500 hover:bg-sky-600 text-white text-sm font-medium px-4 py-2 rounded-lg disabled:opacity-50 transition-colors"
          >
            {loadingAction === "renew" ? "Loading…" : "Renew"}
          </button>
        )}
        {showCancel && (
          <button
            onClick={handleCancel}
            disabled={isLoading}
            className="bg-red-500 hover:bg-red-600 text-white text-sm font-medium px-4 py-2 rounded-lg disabled:opacity-50 transition-colors"
          >
            {loadingAction === "cancel" ? "Loading…" : "Cancel"}
          </button>
        )}
      </div>

      <ErrorToast message={error} onDismiss={() => setError(null)} />
    </>
  );
}

// ── Error Toast ───────────────────────────────────────────────────────────────

function ErrorToast({ message, onDismiss }: { message: string | null; onDismiss: () => void }) {
  useEffect(() => {
    if (!message) return;
    const timer = setTimeout(onDismiss, 5000);
    return () => clearTimeout(timer);
  }, [message, onDismiss]);

  if (!message) return null;

  return (
    <div
      role="alert"
      aria-live="assertive"
      className="fixed bottom-6 right-6 z-[100] animate-in slide-in-from-bottom-4 fade-in duration-300"
    >
      <div className="bg-white dark:bg-slate-800 rounded-2xl shadow-2xl border border-red-200 dark:border-red-700 px-5 py-4 flex items-center gap-3 max-w-sm">
        <div className="w-10 h-10 rounded-xl bg-red-50 dark:bg-red-900/30 flex items-center justify-center flex-shrink-0">
          <svg
            width="20"
            height="20"
            fill="none"
            viewBox="0 0 24 24"
            stroke="#ef4444"
            strokeWidth={2}
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
            />
          </svg>
        </div>
        <p className="text-sm font-medium text-slate-900 dark:text-white flex-1">
          {message}
        </p>
        <button
          onClick={onDismiss}
          className="flex-shrink-0 text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-colors"
          aria-label="Dismiss"
        >
          <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>
    </div>
  );
}

// ── Shared UI ─────────────────────────────────────────────────────────────────

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between">
      <span className="text-xs text-slate-500 dark:text-slate-400">{label}</span>
      <span className="text-sm font-medium text-slate-900 dark:text-white">{value}</span>
    </div>
  );
}

/**
 * Formats an ISO date string to YYYY-MM-DD.
 * Returns "—" if the value is null or invalid.
 */
function formatDate(isoDate: string | null): string {
  if (!isoDate) return "—";
  try {
    const date = new Date(isoDate);
    if (isNaN(date.getTime())) return "—";
    return date.toISOString().split("T")[0];
  } catch {
    return "—";
  }
}
