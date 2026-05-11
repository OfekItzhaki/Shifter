"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { apiClient } from "@/lib/api/client";
import { useSpaceStore } from "@/lib/store/spaceStore";

interface Props {
  groupId: string;
}

interface SubscriptionStatus {
  status: string;
  tierId: string | null;
  trialEndsAt: string | null;
  isActive: boolean;
}

export default function TrialBanner({ groupId }: Props) {
  const { currentSpaceId } = useSpaceStore();
  const router = useRouter();
  const [sub, setSub] = useState<SubscriptionStatus | null>(null);

  useEffect(() => {
    if (!currentSpaceId || !groupId) return;
    apiClient
      .get(`/spaces/${currentSpaceId}/billing/groups/${groupId}/subscription`)
      .then(res => setSub(res.data))
      .catch(() => {});
  }, [currentSpaceId, groupId]);

  if (!sub) return null;

  // Active subscription — no banner needed
  if (sub.status === "active") return null;

  // Trial — show countdown
  if (sub.status === "trialing" && sub.trialEndsAt) {
    const daysLeft = Math.max(0, Math.ceil(
      (new Date(sub.trialEndsAt).getTime() - Date.now()) / (1000 * 60 * 60 * 24)
    ));

    if (daysLeft <= 0) {
      // Trial expired
      return (
        <div className="bg-red-50 border border-red-200 rounded-xl px-4 py-3 flex items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <svg className="w-5 h-5 text-red-500 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
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
      );
    }

    // Trial active — show days remaining
    if (daysLeft <= 7) {
      return (
        <div className="bg-amber-50 border border-amber-200 rounded-xl px-4 py-2.5 flex items-center justify-between gap-3">
          <span className="text-sm text-amber-800">
            ⏳ נותרו <strong>{daysLeft}</strong> ימים לתקופת הניסיון
          </span>
          <button
            onClick={() => router.push("/pricing")}
            className="flex-shrink-0 text-xs text-amber-700 border border-amber-300 hover:bg-amber-100 px-3 py-1.5 rounded-lg transition-colors font-medium"
          >
            צפה בתוכניות
          </button>
        </div>
      );
    }
  }

  // No subscription at all
  if (sub.status === "none") return null;

  return null;
}
