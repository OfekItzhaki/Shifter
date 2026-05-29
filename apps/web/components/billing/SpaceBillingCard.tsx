"use client";

import { useEffect, useState, useCallback } from "react";
import { useTranslations, useLocale } from "next-intl";
import {
  getSpaceSubscription,
  createSpaceCheckout,
  cancelSpaceSubscription,
  renewSpaceSubscription,
  getPlans,
  SpaceSubscriptionDto,
  PlanDto,
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
  const t = useTranslations("billing");
  const locale = useLocale();
  const [subscription, setSubscription] = useState<SpaceSubscriptionDto | null>(null);
  const [plans, setPlans] = useState<PlanDto[]>([]);
  const [loadingState, setLoadingState] = useState<LoadingState>("loading");

  const fetchSubscription = useCallback(async () => {
    if (!spaceId) return;
    setLoadingState("loading");
    try {
      const [data, plansData] = await Promise.all([
        getSpaceSubscription(spaceId),
        getPlans().catch(() => [] as PlanDto[]),
      ]);
      setSubscription(data);
      setPlans(plansData);
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
          {t("title")}
        </h2>
        <div className="flex items-center justify-center py-4 text-slate-500 dark:text-slate-400 text-sm">
          {t("loading")}
        </div>
      </div>
    );
  }

  if (loadingState === "error") {
    return (
      <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-900 dark:text-white mb-3">
          {t("title")}
        </h2>
        <div className="flex flex-col items-center gap-3 py-4">
          <p className="text-sm text-slate-500 dark:text-slate-400">
            {t("errorLoad")}
          </p>
          <button
            onClick={fetchSubscription}
            className="px-4 py-2 rounded-lg bg-sky-500 hover:bg-sky-600 text-white font-semibold text-sm transition-colors"
          >
            {t("retry")}
          </button>
        </div>
      </div>
    );
  }

  // No subscription exists — space is on free trial
  if (!subscription) {
    return (
      <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-sm font-semibold text-slate-900 dark:text-white">
            {t("title")}
          </h2>
          <span className="text-xs font-medium px-2.5 py-1 rounded-full border bg-sky-50 dark:bg-sky-900/20 text-sky-700 dark:text-sky-300 border-sky-200 dark:border-sky-700">
            {t("status.trialing")}
          </span>
        </div>
        <p className="text-sm text-slate-600 dark:text-slate-300 mb-4">
          {t("trialPeriod")}
        </p>
        <UpgradeButton spaceId={spaceId} />
      </div>
    );
  }

  // Subscription exists but expired — trial ended
  if (subscription.status === "expired") {
    return (
      <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-sm font-semibold text-slate-900 dark:text-white">
            {t("title")}
          </h2>
          <span className="text-xs font-medium px-2.5 py-1 rounded-full border bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-300 border-slate-200 dark:border-slate-600">
            {t("status.expired")}
          </span>
        </div>
        <p className="text-sm text-slate-600 dark:text-slate-300 mb-4">
          {t("trialExpired")}
        </p>
        <UpgradeButton spaceId={spaceId} />
      </div>
    );
  }

  // Resolve plan name from tierId
  const planName = resolvePlanName(subscription.tierId, plans);

  return (
    <div className={`rounded-2xl p-5 shadow-sm border ${
      subscription.status === "active"
        ? "bg-green-50/50 dark:bg-green-900/10 border-green-200 dark:border-green-800"
        : "bg-white dark:bg-slate-800 border-slate-200 dark:border-slate-700"
    }`}>
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <h2 className="text-sm font-semibold text-slate-900 dark:text-white">
            {t("title")}
          </h2>
          {planName && subscription.status === "active" && (
            <span className="text-xs font-medium px-2 py-0.5 rounded-md bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-300">
              {planName}
            </span>
          )}
        </div>
        <StatusBadge status={subscription.status} />
      </div>

      <SubscriptionDetails subscription={subscription} locale={locale} planName={planName} />

      <ActionButtons
        spaceId={spaceId}
        status={subscription.status}
        onSubscriptionChange={fetchSubscription}
      />
    </div>
  );
}

// ── Plan Name Resolution ──────────────────────────────────────────────────────

function resolvePlanName(tierId: string | null, plans: PlanDto[]): string | null {
  if (!tierId || tierId === "trial") return null;
  const matchedPlan = plans.find((p) => p.variantId === tierId);
  if (matchedPlan) return matchedPlan.name;
  // Fallback: format the tierId nicely
  return null;
}

// ── Status Badge ──────────────────────────────────────────────────────────────

function StatusBadge({ status }: { status: SpaceSubscriptionDto["status"] }) {
  const t = useTranslations("billing");
  const config = getStatusBadgeConfig(status, t);

  return (
    <span className={`text-xs font-medium px-2.5 py-1 rounded-full border ${config.classes}`}>
      {config.label}
    </span>
  );
}

function getStatusBadgeConfig(status: SpaceSubscriptionDto["status"], t: (key: string) => string) {
  switch (status) {
    case "trialing":
      return {
        label: t("status.trialing"),
        classes: "bg-sky-50 dark:bg-sky-900/20 text-sky-700 dark:text-sky-300 border-sky-200 dark:border-sky-700",
      };
    case "active":
      return {
        label: t("status.active"),
        classes: "bg-green-50 dark:bg-green-900/20 text-green-700 dark:text-green-300 border-green-200 dark:border-green-700",
      };
    case "past_due":
      return {
        label: t("status.past_due"),
        classes: "bg-amber-50 dark:bg-amber-900/20 text-amber-700 dark:text-amber-300 border-amber-200 dark:border-amber-700",
      };
    case "canceled":
      return {
        label: t("status.canceled"),
        classes: "bg-orange-50 dark:bg-orange-900/20 text-orange-700 dark:text-orange-300 border-orange-200 dark:border-orange-700",
      };
    case "expired":
      return {
        label: t("status.expired"),
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

function SubscriptionDetails({
  subscription,
  locale,
  planName,
}: {
  subscription: SpaceSubscriptionDto;
  locale: string;
  planName: string | null;
}) {
  switch (subscription.status) {
    case "trialing":
      return <TrialingDetails subscription={subscription} locale={locale} />;
    case "active":
      return <ActiveDetails subscription={subscription} locale={locale} planName={planName} />;
    case "canceled":
      return <CanceledDetails subscription={subscription} locale={locale} />;
    case "past_due":
      return <PastDueDetails subscription={subscription} locale={locale} />;
    case "expired":
      return <ExpiredDetails />;
    default:
      return null;
  }
}

function TrialingDetails({ subscription, locale }: { subscription: SpaceSubscriptionDto; locale: string }) {
  const t = useTranslations("billing");
  return (
    <div className="space-y-2">
      <DetailRow label={t("trialStart")} value={formatDateLocalized(subscription.trialStartsAt, locale)} />
      <DetailRow label={t("trialEnd")} value={formatDateLocalized(subscription.trialEndsAt, locale)} />
      {subscription.daysRemaining !== null && (
        <DetailRow
          label={t("daysRemaining")}
          value={t("daysRemainingValue", { count: subscription.daysRemaining })}
          highlight={subscription.daysRemaining <= 3}
        />
      )}
    </div>
  );
}

function ActiveDetails({
  subscription,
  locale,
  planName,
}: {
  subscription: SpaceSubscriptionDto;
  locale: string;
  planName: string | null;
}) {
  const t = useTranslations("billing");
  return (
    <div className="space-y-2">
      {planName && (
        <DetailRow label={t("planLabel")} value={planName} />
      )}
      <DetailRow label={t("periodStart")} value={formatDateLocalized(subscription.currentPeriodStart, locale)} />
      <DetailRow label={t("periodEnd")} value={formatDateLocalized(subscription.currentPeriodEnd, locale)} />
      {subscription.daysRemaining !== null && subscription.daysRemaining > 0 && (
        <DetailRow
          label={t("daysRemaining")}
          value={t("daysRemainingValue", { count: subscription.daysRemaining })}
        />
      )}
      {subscription.cardLast4 && (
        <DetailRow label={t("paymentMethod")} value={`•••• ${subscription.cardLast4}`} />
      )}
    </div>
  );
}

function CanceledDetails({ subscription, locale }: { subscription: SpaceSubscriptionDto; locale: string }) {
  const t = useTranslations("billing");
  return (
    <div className="space-y-2">
      <DetailRow label={t("canceledAt")} value={formatDateLocalized(subscription.canceledAt, locale)} />
      <DetailRow label={t("accessExpires")} value={formatDateLocalized(subscription.currentPeriodEnd, locale)} />
      {subscription.daysRemaining !== null && subscription.daysRemaining > 0 && (
        <DetailRow
          label={t("daysRemaining")}
          value={t("daysRemainingValue", { count: subscription.daysRemaining })}
          highlight
        />
      )}
    </div>
  );
}

function PastDueDetails({ subscription, locale }: { subscription: SpaceSubscriptionDto; locale: string }) {
  const t = useTranslations("billing");
  return (
    <div className="space-y-2">
      <DetailRow label={t("periodStart")} value={formatDateLocalized(subscription.currentPeriodStart, locale)} />
      <DetailRow label={t("periodEnd")} value={formatDateLocalized(subscription.currentPeriodEnd, locale)} />
      <p className="text-xs text-amber-600 dark:text-amber-400 mt-1">
        {t("pastDueWarning")}
      </p>
    </div>
  );
}

function ExpiredDetails() {
  const t = useTranslations("billing");
  return (
    <p className="text-sm text-slate-500 dark:text-slate-400">
      {t("expiredMessage")}
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
  const t = useTranslations("billing");
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
      setError(t("errors.checkout"));
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
      setError(t("errors.cancel"));
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
      setError(t("errors.renew"));
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
            {loadingAction === "upgrade" ? t("actions.loading") : t("actions.upgrade")}
          </button>
        )}
        {showRenew && (
          <button
            onClick={handleRenew}
            disabled={isLoading}
            className="bg-sky-500 hover:bg-sky-600 text-white text-sm font-medium px-4 py-2 rounded-lg disabled:opacity-50 transition-colors"
          >
            {loadingAction === "renew" ? t("actions.loading") : t("actions.renew")}
          </button>
        )}
        {showCancel && (
          <button
            onClick={handleCancel}
            disabled={isLoading}
            className="bg-red-500 hover:bg-red-600 text-white text-sm font-medium px-4 py-2 rounded-lg disabled:opacity-50 transition-colors"
          >
            {loadingAction === "cancel" ? t("actions.loading") : t("actions.cancel")}
          </button>
        )}
      </div>

      <ErrorToast message={error} onDismiss={() => setError(null)} />
    </>
  );
}

// ── Upgrade Button ─────────────────────────────────────────────────────────────

function UpgradeButton({ spaceId }: { spaceId: string }) {
  const t = useTranslations("billing");

  return (
    <a
      href="/pricing"
      className="inline-block bg-sky-500 hover:bg-sky-600 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors no-underline text-center"
    >
      {t("upgradeNow")}
    </a>
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
      className="fixed bottom-6 end-6 z-[100] animate-in slide-in-from-bottom-4 fade-in duration-300"
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

function DetailRow({ label, value, highlight }: { label: string; value: string; highlight?: boolean }) {
  return (
    <div className="flex items-center justify-between">
      <span className="text-xs text-slate-500 dark:text-slate-400">{label}</span>
      <span className={`text-sm font-medium ${
        highlight
          ? "text-amber-600 dark:text-amber-400"
          : "text-slate-900 dark:text-white"
      }`}>
        {value}
      </span>
    </div>
  );
}

/**
 * Formats an ISO date string using the browser's Intl.DateTimeFormat for localized display.
 * Returns "—" if the value is null or invalid.
 */
function formatDateLocalized(isoDate: string | null, locale: string): string {
  if (!isoDate) return "—";
  try {
    const date = new Date(isoDate);
    if (isNaN(date.getTime())) return "—";
    return new Intl.DateTimeFormat(locale, {
      year: "numeric",
      month: "short",
      day: "numeric",
    }).format(date);
  } catch {
    return "—";
  }
}
