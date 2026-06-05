"use client";

import { useState } from "react";
import { useTranslations, useLocale } from "next-intl";
import type { GroupMemberDto, GroupQualificationDto, MemberQualificationDto } from "@/lib/api/groups";
import { isRtl as isRtlLocale } from "@/lib/i18n/locales";

interface Props {
  isAdmin: boolean;
  members: GroupMemberDto[];
  qualifications: GroupQualificationDto[];
  memberQualifications: MemberQualificationDto[];
  loading: boolean;
  // Qualification management
  onCreateQualification: (name: string, description: string | null) => Promise<void>;
  onDeactivateQualification: (id: string) => Promise<void>;
  // Member assignment
  onAssign: (personId: string, qualificationId: string) => Promise<void>;
  onRemove: (personId: string, qualificationId: string) => Promise<void>;
}

export default function QualificationsTab({
  isAdmin, members, qualifications, memberQualifications, loading,
  onCreateQualification, onDeactivateQualification, onAssign, onRemove,
}: Props) {
  const t = useTranslations("groups.qualifications_tab");
  const locale = useLocale();
  const isRtl = isRtlLocale(locale);
  const [newQualName, setNewQualName] = useState("");
  const [newQualDesc, setNewQualDesc] = useState("");
  const [qualSaving, setQualSaving] = useState(false);
  const [qualError, setQualError] = useState<string | null>(null);
  const [confirmDeactivate, setConfirmDeactivate] = useState<string | null>(null);

  // Build a map: personId → Set of qualificationIds
  const memberQualMap = new Map<string, Set<string>>();
  for (const mq of memberQualifications) {
    if (!memberQualMap.has(mq.personId)) memberQualMap.set(mq.personId, new Set());
    memberQualMap.get(mq.personId)!.add(mq.qualificationId);
  }

  async function handleCreateQual(e: React.FormEvent) {
    e.preventDefault();
    if (!newQualName.trim()) return;
    setQualSaving(true);
    setQualError(null);
    try {
      await onCreateQualification(newQualName.trim(), newQualDesc.trim() || null);
      setNewQualName(""); setNewQualDesc("");
    } catch {
      setQualError(t("errorCreate"));
    } finally {
      setQualSaving(false);
    }
  }

  if (loading) return <p className="text-sm text-slate-400 py-8">{t("loadingQualifications")}</p>;

  return (
    <div className="space-y-6" dir={isRtl ? "rtl" : "ltr"}>
      {/* Qualification definitions */}
      <div className="bg-white border border-slate-200 rounded-2xl p-5 space-y-4">
        <h3 className="text-sm font-semibold text-slate-700">{t("title")}</h3>

        {qualifications.length === 0 ? (
          <p className="text-xs text-slate-400">{t("noQualifications")}</p>
        ) : (
          <div className="flex flex-wrap gap-2">
            {qualifications.map(q => (
              <div key={q.id} className="flex items-center gap-1.5 bg-slate-100 border border-slate-200 rounded-full px-3 py-1">
                <span className="text-sm text-slate-700">{q.name}</span>
                {isAdmin && (
                  confirmDeactivate === q.id ? (
                    <>
                      <button onClick={() => { setConfirmDeactivate(null); onDeactivateQualification(q.id); }} className="text-xs text-red-600 hover:text-red-800">✓</button>
                      <button onClick={() => setConfirmDeactivate(null)} className="text-xs text-slate-400 hover:text-slate-600">✕</button>
                    </>
                  ) : (
                    <button onClick={() => setConfirmDeactivate(q.id)} className="text-slate-400 hover:text-red-500 transition-colors">
                      <svg width="12" height="12" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                        <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
                      </svg>
                    </button>
                  )
                )}
              </div>
            ))}
          </div>
        )}

        {isAdmin && (
          <form onSubmit={handleCreateQual} className="flex gap-2 pt-1">
            <input
              type="text"
              value={newQualName}
              onChange={e => setNewQualName(e.target.value)}
              placeholder={t("addQualification")}
              className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-500"
            />
            <button type="submit" disabled={qualSaving || !newQualName.trim()}
              className="bg-sky-500 hover:bg-sky-600 text-white text-sm font-medium px-4 py-2 rounded-xl disabled:opacity-50 transition-colors">
              {qualSaving ? "..." : t("add")}
            </button>
          </form>
        )}
        {qualError && <p className="text-xs text-red-600">{qualError}</p>}
      </div>

      {/* Members × Qualifications matrix */}
      {qualifications.length > 0 && members.length > 0 && (
        <div className="bg-white border border-slate-200 rounded-2xl overflow-hidden">
          <div className="px-5 py-4 border-b border-slate-100">
            <h3 className="text-sm font-semibold text-slate-700">{t("memberQualifications")}</h3>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm border-collapse">
              <thead>
                <tr className="bg-slate-50/80 border-b border-slate-100">
                  <th className={`px-4 py-3 text-xs font-semibold text-slate-500 sticky bg-slate-50/80 ${isRtl ? "text-right right-0" : "text-left left-0"}`}>{t("member")}</th>
                  {qualifications.map(q => (
                    <th key={q.id} className="px-3 py-3 text-center text-xs font-semibold text-slate-600 whitespace-nowrap">
                      {q.name}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {members.filter(m => !m.isOwner || true).map(m => {
                  const memberQuals = memberQualMap.get(m.personId) ?? new Set<string>();
                  return (
                    <tr key={m.personId} className="hover:bg-slate-50/40 transition-colors">
                      <td className={`px-4 py-3 sticky bg-white ${isRtl ? "right-0 border-l border-slate-100" : "left-0 border-r border-slate-100"}`}>
                        <div className="flex items-center gap-2">
                          <div className="w-7 h-7 rounded-full bg-sky-500 flex items-center justify-center text-white text-xs font-bold flex-shrink-0">
                            {m.fullName.charAt(0)}
                          </div>
                          <span className="text-sm font-medium text-slate-800 whitespace-nowrap">{m.displayName ?? m.fullName}</span>
                        </div>
                      </td>
                      {qualifications.map(q => {
                        const has = memberQuals.has(q.id);
                        return (
                          <td key={q.id} className="px-3 py-3 text-center">
                            {isAdmin ? (
                              <button
                                onClick={() => has
                                  ? onRemove(m.personId, q.id)
                                  : onAssign(m.personId, q.id)
                                }
                                className={`w-6 h-6 rounded-full border-2 transition-all mx-auto flex items-center justify-center ${
                                  has
                                    ? "bg-emerald-500 border-emerald-500 text-white hover:bg-red-500 hover:border-red-500"
                                    : "border-slate-300 hover:border-sky-400 hover:bg-sky-50"
                                }`}
                                title={has ? `${t("deactivate")} ${q.name}` : `${t("add")} ${q.name}`}
                              >
                                {has && (
                                  <svg width="10" height="10" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}>
                                    <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
                                  </svg>
                                )}
                              </button>
                            ) : (
                              has ? (
                                <span className="text-emerald-500">✓</span>
                              ) : (
                                <span className="text-slate-200">—</span>
                              )
                            )}
                          </td>
                        );
                      })}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
