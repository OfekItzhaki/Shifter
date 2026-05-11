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
  if (sub.status === "active") return null;
  if (sub.status === "none") return null;

  if (sub.status === "trialing" && sub.trialEndsAt) {
    const daysLeft = Math.max(0, Math.ceil(
      (new Date(sub.trialEndsAt).getTime() - Date.now()) / (1000 * 60 * 60 * 24)
    ));

    if (daysLeft <= 0) {
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

    // Trial active — always show days remaining with color based on urgency
    const colorClass = daysLeft <= 3
      ? "bg-red-50 border-red-200"
      : daysLeft <= 7
      ? "bg-amber-50 border-amber-200"
      : "bg-blue-50 border-blue-200";

    const textClass = daysLeft <= 3
      ? "text-red-800"
      : daysLeft <= 7
      ? "text-amber-800"
      : "text-blue-800";

    const btnClass = daysLeft <= 3
      ? "text-red-700 border-red-300 hover:bg-red-100"
      : daysLeft <= 7
      ? "text-amber-700 border-amber-300 hover:bg-amber-100"
      : "text-blue-700 border-blue-300 hover:bg-blue-100";

    return (
      <div className={`border rounded-xl px-4 py-2.5 flex items-center justify-between gap-3 ${colorClass}`}>
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
