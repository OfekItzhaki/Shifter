"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { getMySpaces, createSpace, SpaceDto } from "@/lib/api/spaces";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useAuthStore } from "@/lib/store/authStore";

export default function SpacesPage() {
  const t = useTranslations();
  const router = useRouter();
  const { setCurrentSpace } = useSpaceStore();
  const { preferredLocale } = useAuthStore();

  const [spaces, setSpaces] = useState<SpaceDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [newName, setNewName] = useState("");
  const [showCreate, setShowCreate] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getMySpaces()
      .then(s => {
        setSpaces(s);
        if (s.length === 1) {
          setCurrentSpace(s[0].id, s[0].name);
          router.push("/schedule/today");
        }
      })
      .finally(() => setLoading(false));
  }, []);

  function handleSelect(space: SpaceDto) {
    setCurrentSpace(space.id, space.name);
    router.push("/schedule/today");
  }

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!newName.trim()) return;
    setCreating(true);
    setError(null);
    try {
      await createSpace(newName.trim(), null, preferredLocale);
      const updated = await getMySpaces();
      setSpaces(updated);
      setNewName("");
      setShowCreate(false);
    } catch {
      setError("שגיאה ביצירת מרחב.");
    } finally {
      setCreating(false);
    }
  }

  return (
    <main className="min-h-screen flex items-center justify-center bg-slate-50 p-6">
      {/* Background decoration */}
      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="absolute -top-40 -start-40 w-96 h-96 bg-blue-100 rounded-full opacity-40 blur-3xl" />
        <div className="absolute -bottom-40 -end-40 w-96 h-96 bg-slate-200 rounded-full opacity-40 blur-3xl" />
      </div>

      <div className="relative w-full max-w-md space-y-6">
        {/* Header */}
        <div className="text-center">
          <div className="flex justify-center mb-4">
            <div className="w-12 h-12 rounded-2xl bg-blue-500 flex items-center justify-center shadow-lg shadow-blue-500/30">
              <svg className="w-6 h-6 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M13 10V3L4 14h7v7l9-11h-7z" />
              </svg>
            </div>
          </div>
          <h1 className="text-2xl font-bold text-slate-900">{t("app.name")}</h1>
          <p className="text-sm text-slate-500 mt-1">בחר מרחב עבודה להמשך</p>
        </div>

        {/* Loading */}
        {loading && (
          <div className="flex justify-center py-8">
            <svg className="animate-spin h-6 w-6 text-blue-500" fill="none" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
            </svg>
          </div>
        )}

        {/* Empty state */}
        {!loading && spaces.length === 0 && (
          <div className="text-center py-6 bg-white rounded-2xl border border-slate-100 shadow-sm">
            <p className="text-slate-500 text-sm">
              אינך שייך למרחב עדיין. צור אחד למטה.
            </p>
          </div>
        )}

        {/* Spaces list */}
        {spaces.length > 0 && (
          <div className="space-y-2">
            {spaces.map(space => (
              <button
                key={space.id}
                onClick={() => handleSelect(space)}
                className="w-full text-start bg-white border border-slate-200 rounded-xl px-4 py-3.5 hover:border-blue-400 hover:bg-blue-50/50 hover:shadow-sm transition-all duration-150 group"
              >
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 rounded-lg bg-slate-100 group-hover:bg-blue-100 flex items-center justify-center shrink-0 transition-colors">
                    <svg className="w-4 h-4 text-slate-500 group-hover:text-blue-500 transition-colors" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                    </svg>
                  </div>
                  <div className="min-w-0">
                    <div className="font-medium text-slate-900 text-sm">{space.name}</div>
                    {space.description && (
                      <div className="text-xs text-slate-400 mt-0.5 truncate">{space.description}</div>
                    )}
                  </div>
                  <svg className="w-4 h-4 text-slate-300 group-hover:text-blue-400 ms-auto shrink-0 transition-colors" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
                  </svg>
                </div>
              </button>
            ))}
          </div>
        )}

        {/* Create space */}
        {!showCreate ? (
          <button
            onClick={() => setShowCreate(true)}
            className="w-full flex items-center justify-center gap-2 text-sm text-blue-600 hover:text-blue-700 font-medium py-2 transition-colors"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
            </svg>
            צור מרחב חדש
          </button>
        ) : (
          <form onSubmit={handleCreate} className="bg-white border border-slate-200 rounded-2xl p-5 space-y-4 shadow-sm">
            <h2 className="text-sm font-semibold text-slate-900">מרחב חדש</h2>
            <input
              type="text"
              value={newName}
              onChange={e => setNewName(e.target.value)}
              placeholder="שם המרחב"
              className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm text-slate-900 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-shadow"
              autoFocus
            />
            {error && <p className="text-xs text-red-600">{error}</p>}
            <div className="flex gap-2">
              <button
                type="submit"
                disabled={creating || !newName.trim()}
                className="flex-1 bg-blue-500 hover:bg-blue-600 text-white text-sm py-2.5 rounded-xl font-medium disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
              >
                {creating ? "יוצר..." : "צור"}
              </button>
              <button
                type="button"
                onClick={() => setShowCreate(false)}
                className="px-4 text-sm text-slate-500 hover:text-slate-700 font-medium transition-colors"
              >
                ביטול
              </button>
            </div>
          </form>
        )}
      </div>
    </main>
  );
}
