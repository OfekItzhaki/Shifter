"use client";

import { useEffect, useState } from "react";
import AppShell from "@/components/shell/AppShell";
import { apiClient } from "@/lib/api/client";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useAuthStore } from "@/lib/store/authStore";
import { useRouter } from "next/navigation";

interface GroupDto { id: string; name: string; memberCount: number; solverHorizonDays: number; }

export default function GroupsPage() {
  const { currentSpaceId } = useSpaceStore();
  const { isAdminMode } = useAuthStore();
  const router = useRouter();

  const [groups, setGroups] = useState<GroupDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [newGroupName, setNewGroupName] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!currentSpaceId) { setLoading(false); return; }
    apiClient.get(`/spaces/${currentSpaceId}/groups`)
      .then(r => setGroups(r.data))
      .finally(() => setLoading(false));
  }, [currentSpaceId]);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId || !newGroupName.trim()) return;
    setSaving(true); setError(null);
    try {
      await apiClient.post(`/spaces/${currentSpaceId}/groups`, { name: newGroupName.trim(), description: null });
      const { data } = await apiClient.get(`/spaces/${currentSpaceId}/groups`);
      setGroups(data);
      setNewGroupName("");
    } catch (err: any) {
      setError(err?.response?.data?.error || "שגיאה ביצירת קבוצה");
    } finally { setSaving(false); }
  }

  return (
    <AppShell>
      <div className="max-w-4xl space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-slate-900">קבוצות</h1>
            <p className="text-sm text-slate-500 mt-1">הקבוצות שאתה משויך אליהן</p>
          </div>
        </div>

        {/* Admin: create group */}
        {isAdminMode && (
          <form onSubmit={handleCreate} className="flex gap-2 max-w-sm">
            <input value={newGroupName} onChange={e => setNewGroupName(e.target.value)}
              placeholder="שם קבוצה חדשה"
              className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            <button type="submit" disabled={saving}
              className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 whitespace-nowrap">
              {saving ? "..." : "+ קבוצה חדשה"}
            </button>
          </form>
        )}

        {error && <p className="text-sm text-red-600">{error}</p>}

        {loading ? (
          <p className="text-slate-400 text-sm py-8">טוען...</p>
        ) : groups.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-16 text-center bg-white rounded-xl border border-slate-200">
            <svg className="w-10 h-10 text-slate-200 mb-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
            <p className="text-slate-400 text-sm">אינך משויך לאף קבוצה עדיין.</p>
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            {groups.map(g => (
              <button key={g.id} onClick={() => router.push(`/groups/${g.id}`)}
                className="text-start bg-white border border-slate-200 rounded-2xl p-5 hover:border-blue-300 hover:shadow-md transition-all group">
                <div className="flex items-start justify-between mb-3">
                  <div className="w-10 h-10 rounded-xl bg-blue-50 flex items-center justify-center group-hover:bg-blue-100 transition-colors">
                    <svg className="w-5 h-5 text-blue-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
                    </svg>
                  </div>
                  <svg className="w-4 h-4 text-slate-300 group-hover:text-blue-400 transition-colors mt-1" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
                  </svg>
                </div>
                <h2 className="text-base font-semibold text-slate-900">{g.name}</h2>
                <p className="text-xs text-slate-400 mt-1">{g.memberCount} חברים</p>
              </button>
            ))}
          </div>
        )}
      </div>
    </AppShell>
  );
}
