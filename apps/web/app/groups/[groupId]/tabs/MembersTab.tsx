"use client";

import Modal from "@/components/Modal";
import ImageUpload from "@/components/ImageUpload";
import type { GroupMemberDto } from "@/lib/api/groups";

interface Props {
  isAdmin: boolean;
  members: GroupMemberDto[];
  membersLoading: boolean;
  membersError: string | null;
  membersSearch: string;
  removeErrors: Record<string, string>;
  onSearchChange: (v: string) => void;
  onSelectMember: (m: GroupMemberDto) => void;
  onRemoveMember: (id: string) => void;
  onOpenAddByEmail: () => void;
  onOpenCreatePerson: () => void;
  onOpenInvite: (id: string) => void;
}

export default function MembersTab({
  isAdmin, members, membersLoading, membersError, membersSearch, removeErrors,
  onSearchChange, onSelectMember, onRemoveMember, onOpenAddByEmail, onOpenCreatePerson, onOpenInvite,
}: Props) {
  const filtered = members.filter(m =>
    !membersSearch ||
    m.fullName.toLowerCase().includes(membersSearch.toLowerCase()) ||
    (m.displayName ?? "").toLowerCase().includes(membersSearch.toLowerCase())
  );

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3">
        <div className="relative flex-1 max-w-xs">
          <input
            type="text"
            value={membersSearch}
            onChange={e => onSearchChange(e.target.value)}
            placeholder="חיפוש חברים..."
            className="w-full border border-slate-200 rounded-xl px-3.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 pr-9"
          />
          <svg className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>
        {isAdmin && (
          <div className="flex gap-2">
            <button onClick={onOpenAddByEmail} className="flex items-center gap-1.5 text-sm font-medium text-blue-600 border border-blue-200 bg-blue-50 hover:bg-blue-100 px-3 py-2 rounded-xl transition-colors">
              + הוסף לפי אימייל
            </button>
            <button onClick={onOpenCreatePerson} className="flex items-center gap-1.5 text-sm font-medium text-slate-600 border border-slate-200 bg-white hover:bg-slate-50 px-3 py-2 rounded-xl transition-colors">
              + צור אדם חדש
            </button>
          </div>
        )}
      </div>

      {membersLoading && <p className="text-sm text-slate-400 py-8">טוען חברים...</p>}
      {membersError && <p className="text-sm text-red-600">{membersError}</p>}

      {!membersLoading && filtered.length === 0 && (
        <div className="flex flex-col items-center justify-center py-16 text-center bg-white rounded-xl border border-slate-200">
          <svg className="w-10 h-10 text-slate-200 mb-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
          </svg>
          <p className="text-slate-400 text-sm">אין חברים בקבוצה</p>
        </div>
      )}

      <div className="space-y-2">
        {filtered.map(m => (
          <div key={m.personId} className="flex items-center gap-3 bg-white border border-slate-200 rounded-xl px-4 py-3 hover:border-slate-300 transition-colors">
            <div className="w-9 h-9 rounded-full bg-blue-500 flex items-center justify-center text-white text-sm font-bold flex-shrink-0">
              {(m.displayName ?? m.fullName).charAt(0).toUpperCase()}
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium text-slate-900 truncate">{m.displayName ?? m.fullName}</p>
              {m.phoneNumber && <p className="text-xs text-slate-400 tabular-nums" dir="ltr">{m.phoneNumber}</p>}
            </div>
            <div className="flex items-center gap-2 flex-shrink-0">
              <button onClick={() => onSelectMember(m)} className="text-xs text-blue-600 hover:underline">פרטים</button>
              {isAdmin && (
                <>
                  <button onClick={() => onOpenInvite(m.personId)} className="text-xs text-slate-500 hover:text-slate-700 border border-slate-200 px-2 py-1 rounded-lg hover:bg-slate-50 transition-colors">הזמן</button>
                  <button onClick={() => onRemoveMember(m.personId)} className="text-xs text-red-500 hover:text-red-700 border border-red-100 px-2 py-1 rounded-lg hover:bg-red-50 transition-colors">הסר</button>
                </>
              )}
            </div>
            {removeErrors[m.personId] && <p className="text-xs text-red-600">{removeErrors[m.personId]}</p>}
          </div>
        ))}
      </div>
    </div>
  );
}

// ── Member profile modal (exported for use in page.tsx) ──────────────────────
interface MemberProfileModalProps {
  member: GroupMemberDto;
  isAdmin: boolean;
  editForm: { fullName: string; displayName: string; phoneNumber: string; profileImageUrl: string; birthday: string } | null;
  saving: boolean;
  error: string | null;
  onClose: () => void;
  onStartEdit: () => void;
  onCancelEdit: () => void;
  onChangeForm: (f: { fullName: string; displayName: string; phoneNumber: string; profileImageUrl: string; birthday: string }) => void;
  onSave: (personId: string) => void;
}

export function MemberProfileModal({ member, isAdmin, editForm, saving, error, onClose, onStartEdit, onCancelEdit, onChangeForm, onSave }: MemberProfileModalProps) {
  return (
    <Modal title={member.displayName ?? member.fullName} open onClose={onClose} maxWidth={480}>
      {editForm ? (
        <div className="space-y-4">
          <div>
            <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1">שם מלא</label>
            <input type="text" value={editForm.fullName} onChange={e => onChangeForm({ ...editForm, fullName: e.target.value })} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
          <div>
            <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1">שם תצוגה</label>
            <input type="text" value={editForm.displayName} onChange={e => onChangeForm({ ...editForm, displayName: e.target.value })} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
          <div>
            <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1">טלפון</label>
            <input type="tel" value={editForm.phoneNumber} onChange={e => onChangeForm({ ...editForm, phoneNumber: e.target.value })} dir="ltr" className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
          <div>
            <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1">תמונת פרופיל</label>
            <ImageUpload value={editForm.profileImageUrl || null} onChange={url => onChangeForm({ ...editForm, profileImageUrl: url })} shape="circle" size={64} label="העלה תמונה" disabled={saving} />
          </div>
          <div>
            <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1">תאריך לידה</label>
            <input type="date" value={editForm.birthday} onChange={e => onChangeForm({ ...editForm, birthday: e.target.value })} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
          {error && <p className="text-sm text-red-600">{error}</p>}
          <div className="flex gap-2 pt-1">
            <button onClick={() => onSave(member.personId)} disabled={saving} className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
              {saving ? "שומר..." : "שמור"}
            </button>
            <button onClick={onCancelEdit} className="text-sm text-slate-500 border border-slate-200 px-4 py-2.5 rounded-xl hover:bg-slate-50 transition-colors">ביטול</button>
          </div>
        </div>
      ) : (
        <div className="space-y-4">
          <div className="flex items-center gap-4">
            <div className="w-16 h-16 rounded-full bg-blue-500 flex items-center justify-center text-white text-2xl font-bold flex-shrink-0">
              {(member.displayName ?? member.fullName).charAt(0).toUpperCase()}
            </div>
            <div>
              <p className="text-lg font-semibold text-slate-900">{member.displayName ?? member.fullName}</p>
              {member.phoneNumber && <p className="text-sm text-slate-500 tabular-nums" dir="ltr">{member.phoneNumber}</p>}
            </div>
          </div>
          {isAdmin && (
            <button onClick={onStartEdit} className="text-sm text-blue-600 border border-blue-200 bg-blue-50 hover:bg-blue-100 px-4 py-2 rounded-xl transition-colors">
              ערוך פרטים
            </button>
          )}
        </div>
      )}
    </Modal>
  );
}
