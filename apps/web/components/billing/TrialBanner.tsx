"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { getSpaceSubscription, SpaceSubscriptionDto } from "@/lib/api/billing";

export default function TrialBanner() {
  const { currentSpaceId } = useSpaceStore();
  const router = useRouter();
  const [sub, setSub] = useState<SpaceSubscriptionDto | null>(null);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    if (!currentSpaceId) return;
    getSpaceSubscription(currentSpaceId)
      .then((data) => {
        setSub(data);
        setLoaded(true);
      })
      .catch(() => {
        // Req 3.5: fail silent — hide banner on API failure
        setSub(null);
        setLoaded(true);
      });
  }, [currentSpaceId]);

  // Don't render until loaded (fail silent means render nothing on error)
  if (!loaded) return null;
  if (!sub) return null;

  // Req 3.3: Hide when active + auto-renewing
  if (sub.status === "active" && sub.autoRenew) return null;

  // Canceled subscription — show days remaining until access expires
  if (sub.status === "canceled" && sub.currentPeriodEnd) {
    const daysUntilExpiry = Math.max(
      0,
      Math.ceil(
        (new Date(sub.currentPeriodEnd).getTime() - Date.now()) /
          (1000 * 60 * 60 * 24)
      )
    );

    if (daysUntilExpiry <= 0) {
      // Period ended — show renew prompt
      return (
        <div className="bg-red-50 border border-red-200 rounded-xl px-4 py-3 flex items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <svg className="w-5 h-5 text-red-500 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
            </svg>
            <span className="text-sm text-red-800 font-medium">
              המנוי שלך הסתיים. חדש את המנוי כדי להמשיך להשתמש בסידור האוטומטי.
            </span>
          </div>
          <button
            onClick={() => router.push("/pricing")}
            className="flex-shrink-0 bg-red-500 hover:bg-red-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg transition-colors"
          >
            חדש מנוי
          </button>
        </div>
      );
    }

    // Still has access — show warning
    const colorClass = daysUntilExpiry <= 3 ? "bg-red-50 border-red-200" : daysUntilExpiry <= 7 ? "bg-amber-50 border-amber-200" : "bg-sky-50 border-sky-200";
    const textClass = daysUntilExpiry <= 3 ? "text-red-800" : daysUntilExpiry <= 7 ? "text-amber-800" : "text-sky-800";
    const btnClass = daysUntilExpiry <= 3 ? "text-red-700 border-red-300 hover:bg-red-100" : daysUntilExpiry <= 7 ? "text-amber-700 border-amber-300 hover:bg-amber-100" : "text-sky-700 border-sky-300 hover:bg-sky-100";

    return (
      <div className={`border rounded-xl px-4 py-2.5 flex items-center justify-between gap-3 ${colorClass}`}>
        <span className={`text-sm ${textClass}`}>
          ⚠️ המנוי שלך בוטל. הגישה תסתיים בעוד <strong>{daysUntilExpiry}</strong> ימים.
        </span>
        <button
          onClick={() => router.push("/pricing")}
          className={`flex-shrink-0 text-xs border px-3 py-1.5 rounded-lg transition-colors font-medium ${btnClass}`}
        >
          חדש מנוי
        </button>
      </div>
    );
  }

  // Expired subscription — show renew prompt
  if (sub.status === "expired") {
    return (
      <div className="bg-red-50 border border-red-200 rounded-xl px-4 py-3 flex items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <svg className="w-5 h-5 text-red-500 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
          </svg>
          <span className="text-sm text-red-800 font-medium">
            המנוי שלך הסתיים. חדש את המנוי כדי להמשיך להשתמש בסידור האוטומטי.
          </span>
        </div>
        <button
          onClick={() => router.push("/pricing")}
          className="flex-shrink-0 bg-red-500 hover:bg-red-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg transition-colors"
        >
          חדש מנוי
        </button>
      </div>
    );
  }

  // Req 3.4: Active + not auto-renewing + within 7 days of expiry
  if (sub.status === "active" && !sub.autoRenew && sub.currentPeriodEnd) {
    const daysUntilExpiry = Math.max(
      0,
      Math.ceil(
        (new Date(sub.currentPeriodEnd).getTime() - Date.now()) /
          (1000 * 60 * 60 * 24)
      )
    );

    if (daysUntilExpiry > 14) return null;

    const colorClass =
      daysUntilExpiry <= 3
        ? "bg-red-50 border-red-200"
        : daysUntilExpiry <= 7
          ? "bg-amber-50 border-amber-200"
          : "bg-sky-50 border-sky-200";

    const textClass =
      daysUntilExpiry <= 3
        ? "text-red-800"
        : daysUntilExpiry <= 7
          ? "text-amber-800"
          : "text-sky-800";

    const btnClass =
      daysUntilExpiry <= 3
        ? "text-red-700 border-red-300 hover:bg-red-100"
        : daysUntilExpiry <= 7
          ? "text-amber-700 border-amber-300 hover:bg-amber-100"
          : "text-sky-700 border-sky-300 hover:bg-sky-100";

    return (
      <div
        className={`border rounded-xl px-4 py-2.5 flex items-center justify-between gap-3 ${colorClass}`}
      >
        <span className={`text-sm ${textClass}`}>
          ⚠️ המנוי שלך יסתיים בעוד{" "}
          <strong>{daysUntilExpiry}</strong> ימים ולא יתחדש אוטומטית.
        </span>
        <button
          onClick={() => router.push("/pricing")}
          className={`flex-shrink-0 text-xs border px-3 py-1.5 rounded-lg transition-colors font-medium ${btnClass}`}
        >
          חדש מנוי
        </button>
      </div>
    );
  }

  // Trialing state
  if (sub.status === "trialing") {
    const daysLeft = sub.daysRemaining ?? 0;

    // Req 3.2: days remaining is 0 — show upgrade prompt
    if (daysLeft <= 0) {
      return (
        <div className="bg-red-50 border border-red-200 rounded-xl px-4 py-3 flex flex-col gap-2">
          <div className="flex items-center justify-between gap-3">
            <div className="flex items-center gap-2">
              <svg
                className="w-5 h-5 text-red-500 flex-shrink-0"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                strokeWidth={2}
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z"
                />
              </svg>
              <span className="text-sm text-red-800 font-medium">
                תקופת הניסיון הסתיימה. שדרג כדי להמשיך להשתמש בסידור האוטומטי.
              </span>
            </div>
            <button
              onClick={() => router.push("/pricing")}
              className="flex-shrink-0 bg-red-500 hover:bg-red-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg transition-colors"
            >
              שדרג עכשיו
            </button>
          </div>
        </div>
      );
    }

    // Req 3.1 + 3.6: Display days remaining with color logic
    const colorClass =
      daysLeft <= 3
        ? "bg-red-50 border-red-200"
        : daysLeft <= 7
          ? "bg-amber-50 border-amber-200"
          : "bg-sky-50 border-sky-200";

    const textClass =
      daysLeft <= 3
        ? "text-red-800"
        : daysLeft <= 7
          ? "text-amber-800"
          : "text-sky-800";

    const btnClass =
      daysLeft <= 3
        ? "text-red-700 border-red-300 hover:bg-red-100"
        : daysLeft <= 7
          ? "text-amber-700 border-amber-300 hover:bg-amber-100"
          : "text-sky-700 border-sky-300 hover:bg-sky-100";

    return (
      <div
        className={`border rounded-xl px-4 py-2.5 flex items-center justify-between gap-3 ${colorClass}`}
      >
        <span className={`text-sm ${textClass}`}>
          ⏳ נותרו <strong>{daysLeft}</strong> ימים לתקופת הניסיון
        </span>
        <button
          onClick={() => router.push("/pricing")}
          className={`flex-shrink-0 text-xs border px-3 py-1.5 rounded-lg transition-colors font-medium ${btnClass}`}
        >
          צפה בתוכניות
        </button>
      </div>
    );
  }

  return null;
}
