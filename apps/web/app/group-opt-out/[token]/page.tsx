"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { apiClient } from "@/lib/api/client";

export default function GroupOptOutPage() {
  const { token } = useParams<{ token: string }>();
  const router = useRouter();
  const [step, setStep] = useState<"confirm" | "loading" | "done" | "error">("confirm");
  const [groupName, setGroupName] = useState<string | null>(null);

  async function handleConfirm() {
    setStep("loading");
    try {
      const { data } = await apiClient.post(`/group-opt-out/${token}`);
      setGroupName(data.groupName);
      setStep("done");
    } catch {
      setStep("error");
    }
  }

  return (
    <main className="min-h-screen flex items-center justify-center bg-slate-50 p-6">
      <div className="w-full max-w-sm bg-white border border-slate-200 rounded-2xl p-8 shadow-sm space-y-6 text-center">
        <div className="w-12 h-12 rounded-2xl bg-red-50 flex items-center justify-center mx-auto">
          <svg className="w-6 h-6 text-red-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
          </svg>
        </div>

        {step === "confirm" && (
          <>
            <div>
              <h1 className="text-lg font-bold text-slate-900">עזיבת קבוצה</h1>
              <p className="text-sm text-slate-500 mt-2">
                האם אתה בטוח שברצונך לעזוב את הקבוצה? לאחר העזיבה תצטרך להתווסף מחדש על ידי מנהל.
              </p>
            </div>
            <div className="flex gap-3">
              <button onClick={() => router.push("/")}
                className="flex-1 border border-slate-200 text-slate-600 text-sm font-medium py-2.5 rounded-xl hover:bg-slate-50">
                ביטול
              </button>
              <button onClick={handleConfirm}
                className="flex-1 bg-red-500 hover:bg-red-600 text-white text-sm font-medium py-2.5 rounded-xl">
                כן, עזוב
              </button>
            </div>
          </>
        )}

        {step === "loading" && (
          <p className="text-slate-400 text-sm">מעבד...</p>
        )}

        {step === "done" && (
          <>
            <div>
              <h1 className="text-lg font-bold text-slate-900">עזבת את הקבוצה</h1>
              <p className="text-sm text-slate-500 mt-2">
                הוסרת בהצלחה{groupName ? ` מהקבוצה "${groupName}"` : ""}. תוכל להתווסף מחדש רק על ידי מנהל.
              </p>
            </div>
            <button onClick={() => router.push("/")}
              className="w-full bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium py-2.5 rounded-xl">
              חזור לדף הבית
            </button>
          </>
        )}

        {step === "error" && (
          <>
            <div>
              <h1 className="text-lg font-bold text-slate-900">שגיאה</h1>
              <p className="text-sm text-slate-500 mt-2">הקישור אינו תקין או שכבר עזבת את הקבוצה.</p>
            </div>
            <button onClick={() => router.push("/")}
              className="w-full bg-slate-500 hover:bg-slate-600 text-white text-sm font-medium py-2.5 rounded-xl">
              חזור לדף הבית
            </button>
          </>
        )}
      </div>
    </main>
  );
}
