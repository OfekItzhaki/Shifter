"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import {
  cancelGroupSubscription,
  createGroupCheckout,
  getGroupSubscription,
  GroupSubscriptionDto,
  renewGroupSubscription,
} from "@/lib/api/billing";

interface Props {
  spaceId: string;
  groupId: string;
}

type LoadState = "loading" | "loaded" | "error";
type ActionState = "upgrade" | "cancel" | "renew" | null;

export default function GroupBillingCard({ spaceId, groupId }: Props) {
  const t = useTranslations("billing");
  const [subscription, setSubscription] = useState<GroupSubscriptionDto | null>(null);
  const [loadState, setLoadState] = useState<LoadState>("loading");
  const [action, setAction] = useState<ActionState>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirmCancel, setConfirmCancel] = useState(false);

  const load = useCallback(async () => {
    setLoadState("loading");
    try {
      const data = await getGroupSubscription(spaceId, groupId);
      setSubscription(data);
      setLoadState("loaded");
    } catch {
      setLoadState("error");
    }
  }, [spaceId, groupId]);

  useEffect(() => {
    load();
  }, [load]);

  async function runAction(nextAction: Exclude<ActionState, null>) {
    setAction(nextAction);
    setError(null);
    try {
      if (nextAction === "upgrade") {
        const { checkoutUrl } = await createGroupCheckout(spaceId, groupId);
        window.location.href = checkoutUrl;
        return;
      }
      if (nextAction === "cancel") {
        await cancelGroupSubscription(spaceId, groupId);
        setConfirmCancel(false);
      }
      if (nextAction === "renew") {
        await renewGroupSubscription(spaceId, groupId);
      }
      await load();
    } catch {
      setError(t(`errors.${nextAction === "upgrade" ? "checkout" : nextAction}`));
    } finally {
      setAction(null);
    }
  }

  if (loadState === "loading") {
    return (
      <section className="rounded-xl border border-slate-200 bg-white p-4">
        <h4 className="text-sm font-semibold text-slate-900">{t("title")}</h4>
        <p className="mt-3 text-sm text-slate-400">{t("loading")}</p>
      </section>
    );
  }

  if (loadState === "error") {
    return (
      <section className="rounded-xl border border-slate-200 bg-white p-4">
        <h4 className="text-sm font-semibold text-slate-900">{t("title")}</h4>
        <p className="mt-3 text-sm text-slate-500">{t("errorLoad")}</p>
        <button
          type="button"
          onClick={load}
          className="mt-3 rounded-lg bg-sky-500 px-4 py-2 text-sm font-medium text-white hover:bg-sky-600"
        >
          {t("retry")}
        </button>
      </section>
    );
  }

  const normalizedStatus = subscription?.status?.toLowerCase() ?? "none";
  const status =
    subscription?.isActive === false && normalizedStatus === "trialing"
      ? "expired"
      : normalizedStatus === "pastdue" ? "past_due" : normalizedStatus;
  const isActive = subscription?.isActive ?? (status === "active" || status === "trialing");
  const canRenew = status === "canceled" || status === "expired";
  const canCancel = status === "active" || status === "trialing";
  const trialEnd = subscription?.trialEndsAt ? new Date(subscription.trialEndsAt) : null;
  const daysLeft = trialEnd
    ? Math.max(0, Math.ceil((trialEnd.getTime() - Date.now()) / 86400000))
    : null;
  const statusLabel = status === "none" || status === "migrated"
    ? t("status.trialing")
    : t(`status.${status}`);

  return (
    <section className="rounded-xl border border-slate-200 bg-white p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h4 className="text-sm font-semibold text-slate-900">{t("title")}</h4>
          <p className="mt-1 text-xs text-slate-500">
            {status === "none" ? t("trialPeriod") : statusLabel}
          </p>
        </div>
        <span className={`rounded-full border px-2.5 py-1 text-xs font-medium ${
          isActive
            ? "border-emerald-200 bg-emerald-50 text-emerald-700"
            : "border-amber-200 bg-amber-50 text-amber-700"
        }`}>
          {statusLabel}
        </span>
      </div>

      {daysLeft !== null && (
        <p className="mt-3 text-sm text-slate-600">
          {t("daysRemaining")}: {t("daysRemainingValue", { count: daysLeft })}
        </p>
      )}
      {subscription?.periodEndsAt && (
        <p className="mt-3 text-sm text-slate-600">
          {t("periodEnd")}: {new Date(subscription.periodEndsAt).toLocaleDateString()}
        </p>
      )}

      <div className="mt-4 flex flex-wrap gap-2 border-t border-slate-100 pt-4">
        {(status === "trialing" || status === "expired" || status === "none") && (
          <button
            type="button"
            onClick={() => runAction("upgrade")}
            disabled={action !== null}
            className="rounded-lg bg-sky-500 px-4 py-2 text-sm font-medium text-white hover:bg-sky-600 disabled:opacity-50"
          >
            {action === "upgrade" ? t("actions.loading") : t("actions.upgrade")}
          </button>
        )}
        {canRenew && (
          <button
            type="button"
            onClick={() => runAction("renew")}
            disabled={action !== null}
            className="rounded-lg bg-sky-500 px-4 py-2 text-sm font-medium text-white hover:bg-sky-600 disabled:opacity-50"
          >
            {action === "renew" ? t("actions.loading") : t("actions.renew")}
          </button>
        )}
        {canCancel && !confirmCancel && (
          <button
            type="button"
            onClick={() => setConfirmCancel(true)}
            disabled={action !== null}
            className="rounded-lg border border-red-200 px-4 py-2 text-sm font-medium text-red-600 hover:bg-red-50 disabled:opacity-50"
          >
            {t("actions.cancel")}
          </button>
        )}
      </div>

      {confirmCancel && (
        <div className="mt-4 rounded-xl border border-red-200 bg-red-50 p-3">
          <p className="text-sm font-medium text-slate-900">{t("cancelConfirm.title")}</p>
          <p className="mt-1 text-xs text-slate-600">{t("cancelConfirm.description")}</p>
          <div className="mt-3 flex gap-2">
            <button
              type="button"
              onClick={() => runAction("cancel")}
              disabled={action !== null}
              className="rounded-lg bg-red-500 px-3 py-2 text-sm font-medium text-white hover:bg-red-600 disabled:opacity-50"
            >
              {action === "cancel" ? t("actions.loading") : t("cancelConfirm.confirm")}
            </button>
            <button
              type="button"
              onClick={() => setConfirmCancel(false)}
              disabled={action !== null}
              className="rounded-lg border border-slate-200 px-3 py-2 text-sm font-medium text-slate-600 hover:bg-white disabled:opacity-50"
            >
              {t("cancelConfirm.keepSubscription")}
            </button>
          </div>
        </div>
      )}

      {error && <p className="mt-3 text-sm text-red-600">{error}</p>}
    </section>
  );
}
