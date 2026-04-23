"use client";

import { useEffect, useState } from "react";
import AppShell from "@/components/shell/AppShell";
import { getPeople, createPerson, PersonDto } from "@/lib/api/people";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useAuthStore } from "@/lib/store/authStore";
import Link from "next/link";
import { clsx } from "clsx";

export default function PeoplePage() {
  const { currentSpaceId } = useSpaceStore();
  const { isAdminMode } = useAuthStore();
  const [people, setPeople] = useState<PersonDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [showCreate, setShowCreate] = useState(false);
  const [fullName, setFullName] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!currentSpaceId) { setLoading(false); return; }
    getPeople(currentSpaceId).then(setPeople).finally(() => setLoading(false));
  }, [currentSpaceId]);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId || !fullName.trim()) return;
    setCreating(true);
    setError(null);
    try {
      await createPerson(currentSpaceId, fullName.trim(), displayName.trim() || null);
      const updated = await getPeople(currentSpaceId);
      setPeople(updated);
      setFullName(""); setDisplayName(""); setShowCreate(false);
    } catch {
      setError("שגיאה ביצירת אדם.");
    } finally {
      setCreating(false);
    }
  }

  if (!isAdminMode) {
    return (
      <AppShell>
        <div className="flex flex-col items-center justify-center py-20 text-center">
          <svg className="w-12 h-12 text-slate-200 mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
          </svg>
          <p className="text-slate-500 text-sm">נדרש מצב ניהול.</p>
        </div>
      </AppShell>
    );
  }

  return (
    <AppShell>
      <div className="max-w-3xl space-y-6">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-slate-900">אנשים</h1>
            <p className="text-sm text-slate-500 mt-1">
              {people.length} {people.length === 1 ? "אדם" : "אנשים"} במרחב זה
            </p>
          </div>
          <button
            onClick={() => setShowCreate(!showCreate)}
            className="flex items-center gap-2 bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl shadow-sm shadow-blue-500/20 transition-all"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
            </svg>
            הוסף אדם
          </button>
        </div>

        {/* Create form */}
        {showCreate && (
          <form onSubmit={handleCreate}
            className="bg-white border border-slate-200 rounded-2xl p-5 space-y-4 shadow-sm">
            <h2 className="text-sm font-semibold text-slate-900">אדם חדש</h2>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">שם מלא *</label>
                <input
                  value={fullName}
                  onChange={e => setFullName(e.target.value)}
                  className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-shadow"
                  placeholder="שם מלא"
                  required
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">שם תצוגה</label>
                <input
                  value={displayName}
                  onChange={e => setDisplayName(e.target.value)}
                  className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-shadow"
                  placeholder="כינוי (אופציונלי)"
                />
              </div>
            </div>
            {error && <p className="text-xs text-red-600">{error}</p>}
            <div className="flex gap-2">
              <button
                type="submit"
                disabled={creating}
                className="bg-emerald-500 hover:bg-emerald-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
              >
                {creating ? "שומר..." : "שמור"}
              </button>
              <button
                type="button"
                onClick={() => setShowCreate(false)}
                className="text-sm text-slate-500 hover:text-slate-700 px-3 transition-colors"
              >
                ביטול
              </button>
            </div>
          </form>
        )}

        {/* Loading */}
        {loading && (
          <div className="flex items-center gap-3 text-slate-400 text-sm py-8">
            <svg className="animate-spin h-5 w-5 text-blue-400" fill="none" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
            </svg>
            טוען...
          </div>
        )}

        {/* Table */}
        <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-100 bg-slate-50/80">
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">שם</th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">שם תצוגה</th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">סטטוס</th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {people.map(p => (
                <tr key={p.id} className="hover:bg-slate-50/60 transition-colors">
                  <td className="px-4 py-3.5 font-medium text-slate-900">{p.fullName}</td>
                  <td className="px-4 py-3.5 text-slate-500">{p.displayName ?? "—"}</td>
                  <td className="px-4 py-3.5">
                    <span className={clsx(
                      "inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-xs font-medium border",
                      p.isActive
                        ? "bg-emerald-50 text-emerald-700 border-emerald-200"
                        : "bg-slate-100 text-slate-500 border-slate-200"
                    )}>
                      <span className={clsx(
                        "w-1.5 h-1.5 rounded-full",
                        p.isActive ? "bg-emerald-500" : "bg-slate-400"
                      )} />
                      {p.isActive ? "פעיל" : "לא פעיל"}
                    </span>
                  </td>
                  <td className="px-4 py-3.5">
                    <Link
                      href={`/admin/people/${p.id}`}
                      className="text-xs font-medium text-blue-600 hover:text-blue-700 transition-colors"
                    >
                      פרטים →
                    </Link>
                  </td>
                </tr>
              ))}
              {!loading && people.length === 0 && (
                <tr>
                  <td colSpan={4} className="px-4 py-12 text-center text-slate-400 text-sm">
                    אין אנשים עדיין. הוסף מישהו למעלה.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </AppShell>
  );
}
