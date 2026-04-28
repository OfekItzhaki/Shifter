"use client";

import ImageUpload from "@/components/ImageUpload";
import type { GroupMemberDto } from "../types";

const INP = "border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500";

interface MemberEditForm {
  fullName: string;
  displayName: string;
  phoneNumber: string;
  profileImageUrl: string;
  birthday: string;
}

interface Props {
  isAdmin: boolean;
  members: GroupMemberDto[];
  membersLoading: boolean;
  membersError: string | null;
  membersSearch: string;
  removeErrors: Record<string, string>;
  onSearchChange: (q: string) => void;
  onSelectMember: (m: GroupMemberDto) => void;
  onRemoveMember: (personId: string) => void;
  onOpenAddByEmail: () => void;
  onOpenCreatePerson: () => void;
  onOpenInvite: (personId: string) => void;
}

function MemberAvatar({ member }: { member: GroupMemberDto }) {
  const name = member.displayName ?? member.fullName;
  if (member.profileImageUrl) {
    return (
      <img
        src={member.profileImageUrl}
        alt=""
        style={{ width: 32, height: 32, borderRadius: "50%", objectFit: "cover" }}
      />
    );
  }
  return <>{name.charAt(0)}</>;
}

function SearchBox({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  return (
    <div className="relative max-w-sm">
      <input
        type="text"
        value={value}
        onChange={e => onChange(e.target.value)}
        placeholder="חפש לפי שם או טלפון..."
        className={`w-full ${INP} pr-9`}
      />
      <svg className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
      </svg>
    </div>
  );
}

function LoadingSpinner() {
  return (
    <div className="flex items-center gap-3 text-slate-400 text-sm py-8">
      <svg className="animate-spin h-5 w-5 text-blue-400" fill="none" viewBox="0 0 24 24">
        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
      </svg>
      טוען...
    </div>
  );
}

function ReadOnlyMemberList({ members, search, onSelect }: {
  members: GroupMemberDto[];
  search: string;
  onSelect: (m: GroupMemberDto) => void;
}) {
  const filtered = search.trim()
    ? members.filter(m =>
        (m.displayName ?? m.fullName).toLowerCase().includes(search.toLowerCase()) ||
        (m.phoneNumber ?? "").includes(search))
    : members;

  if (members.length === 0) return <p className="text-sm text-slate-400 py-8 text-center">אין חברים בקבוצה זו</p>;

  return (
    <div className="space-y-2">
      {filtered.length === 0 ? (
        <p className="text-sm text-slate-400 py-4 text-center">לא נמצאו חברים</p>
      ) : filtered.map(m => (
        <div
          key={m.personId}
          className="flex items-center gap-3 bg-white border border-slate-200 rounded-xl px-4 py-3 cursor-pointer hover:bg-slate-50 transition-colors"
          onClick={() => onSelect(m)}
        >
          <div
            className="w-8 h-8 rounded-full flex items-center justify-center text-white text-sm font-semibold flex-shrink-0"
            style={{ background: m.profileImageUrl ? "transparent" : "linear-gradient(135deg, #3b82f6, #6366f1)" }}
          >
            <MemberAvatar member={m} />
          </div>
          <span className="text-sm font-medium text-slate-900">{m.displayName ?? m.fullName}</span>
          {m.phoneNumber && <span className="text-xs text-slate-400 mr-2">{m.phoneNumber}</span>}
        </div>
      ))}
    </div>
  );
}

function EditableMemberList({ members, search, removeErrors, onSelect, onRemove, onInvite }: {
  members: GroupMemberDto[];
  search: string;
  removeErrors: Record<string, string>;
  onSelect: (m: GroupMemberDto) => void;
  onRemove: (personId: string) => void;
  onInvite: (personId: string) => void;
}) {
  const filtered = search.trim()
    ? members.filter(m =>
        (m.displayName ?? m.fullName).toLowerCase().includes(search.toLowerCase()) ||
        (m.phoneNumber ?? "").includes(search))
    : members;

  if (filtered.length === 0) {
    return (
      <p className="text-sm text-slate-400 py-4 text-center">
        {search ? "לא נמצאו חברים התואמים לחיפוש" : "אין חברים בקבוצה זו"}
      </p>
    );
  }

  return (
    <div className="space-y-2">
      {filtered.map(m => (
        <div key={m.personId}>
          <div className="flex items-center justify-between bg-white border border-slate-200 rounded-xl px-4 py-3">
            <div className="flex items-center gap-3">
              <div
                className="w-8 h-8 rounded-full flex items-center justify-center text-white text-sm font-semibold cursor-pointer flex-shrink-0"
                style={{ background: m.profileImageUrl ? "transparent" : "linear-gradient(135deg, #3b82f6, #6366f1)" }}
                onClick={() => onSelect(m)}
              >
                <MemberAvatar member={m} />
              </div>
              <div>
                <span
                  className="text-sm font-medium text-slate-900 cursor-pointer hover:text-blue-600 transition-colors"
                  onClick={() => onSelect(m)}
                >
                  {m.displayName ?? m.fullName}
                </span>
                {m.phoneNumber && <span className="text-xs text-slate-400 mr-2">{m.phoneNumber}</span>}
              </div>
              {m.isOwner && (
                <span className="text-xs text-amber-600 bg-amber-50 border border-amber-200 px-2 py-0.5 rounded-full">בעלים</span>
              )}
            </div>
            <div className="flex items-center gap-2">
              {removeErrors[m.personId] && (
                <span className="text-xs text-red-600">{removeErrors[m.personId]}</span>
              )}
              {m.invitationStatus !== "accepted" && (
                <button
                  onClick={() => onInvite(m.personId)}
                  className="text-xs text-blue-500 hover:text-blue-700 border border-blue-200 hover:border-blue-400 px-2.5 py-1 rounded-lg transition-colors"
                >
                  הזמן
                </button>
              )}
              {!m.isOwner && (
                <button
                  onClick={() => onRemove(m.personId)}
                  className="text-xs text-red-500 hover:text-red-700 border border-red-200 hover:border-red-400 px-2.5 py-1 rounded-lg transition-colors"
                >
                  הסר
                </button>
              )}
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

export default function MembersTab({
  isAdmin, members, membersLoading, membersError, membersSearch,
  removeErrors, onSearchChange, onSelectMember, onRemoveMember,
  onOpenAddByEmail, onOpenCreatePerson, onOpenInvite,
}: Props) {
  if (membersLoading) return <LoadingSpinner />;
  if (membersError) return <p className="text-sm text-red-600 py-4">{membersError}</p>;

  return (
    <div className="space-y-4">
      <SearchBox value={membersSearch} onChange={onSearchChange} />

      {isAdmin && (
        <div className="flex gap-2">
          <button
            onClick={onOpenAddByEmail}
            className="flex items-center gap-2 bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-3.5 py-2 rounded-xl transition-colors"
          >
            <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
            </svg>
            הוסף לפי אימייל/טלפון
          </button>
          <button
            onClick={onOpenCreatePerson}
            className="flex items-center gap-2 text-sm text-blue-600 hover:text-blue-800 border border-blue-200 hover:border-blue-400 px-3.5 py-2 rounded-xl transition-colors"
          >
            <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
            </svg>
            הוסף לפי שם בלבד
          </button>
        </div>
      )}

      {isAdmin ? (
        <EditableMemberList
          members={members}
          search={membersSearch}
          removeErrors={removeErrors}
          onSelect={onSelectMember}
          onRemove={onRemoveMember}
          onInvite={onOpenInvite}
        />
      ) : (
        <ReadOnlyMemberList
          members={members}
          search={membersSearch}
          onSelect={onSelectMember}
        />
      )}
    </div>
  );
}

// ── Member profile modal (view + edit) ──────────────────────────────────────

interface MemberProfileModalProps {
  member: GroupMemberDto;
  isAdmin: boolean;
  editForm: MemberEditForm | null;
  saving: boolean;
  error: string | null;
  onClose: () => void;
  onStartEdit: () => void;
  onCancelEdit: () => void;
  onChangeForm: (form: MemberEditForm) => void;
  onSave: (personId: string) => void;
}

export function MemberProfileModal({
  member, isAdmin, editForm, saving, error,
  onClose, onStartEdit, onCancelEdit, onChangeForm, onSave,
}: MemberProfileModalProps) {
  const overlayStyle: React.CSSProperties = {
    position: "fixed", inset: 0, zIndex: 50,
    background: "rgba(0,0,0,0.45)",
    display: "flex", alignItems: "center", justifyContent: "center",
    padding: "1rem",
  };
  const cardStyle: React.CSSProperties = {
    background: "white", borderRadius: 20,
    boxShadow: "0 20px 60px rgba(0,0,0,0.15)",
    width: "100%", maxWidth: 420,
    padding: "1.75rem",
    direction: "rtl",
    position: "relative",
  };

  return (
    <div style={overlayStyle} onClick={onClose}>
      <div style={cardStyle} onClick={e => e.stopPropagation()}>
        <button
          onClick={onClose}
          style={{ position: "absolute", top: "1rem", left: "1rem", background: "none", border: "none", cursor: "pointer", color: "#94a3b8", padding: 4, display: "flex", alignItems: "center" }}
        >
          <svg width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>

        {editForm ? (
          <MemberEditView
            form={editForm}
            saving={saving}
            error={error}
            onChange={onChangeForm}
            onSave={() => onSave(member.personId)}
            onCancel={onCancelEdit}
          />
        ) : (
          <MemberViewMode
            member={member}
            isAdmin={isAdmin}
            onEdit={onStartEdit}
          />
        )}
      </div>
    </div>
  );
}

function MemberViewMode({ member, isAdmin, onEdit }: {
  member: GroupMemberDto;
  isAdmin: boolean;
  onEdit: () => void;
}) {
  const name = member.displayName ?? member.fullName;
  return (
    <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: "1rem", textAlign: "center" }}>
      {member.profileImageUrl ? (
        <img src={member.profileImageUrl} alt="" style={{ width: 80, height: 80, borderRadius: "50%", objectFit: "cover" }} />
      ) : (
        <div style={{ width: 80, height: 80, borderRadius: "50%", background: "linear-gradient(135deg, #3b82f6, #6366f1)", display: "flex", alignItems: "center", justifyContent: "center", color: "white", fontSize: "1.75rem", fontWeight: 700 }}>
          {name.charAt(0)}
        </div>
      )}
      <div>
        <h2 style={{ fontSize: "1.125rem", fontWeight: 700, color: "#0f172a", margin: "0 0 0.25rem" }}>{member.fullName}</h2>
        {member.displayName && member.displayName !== member.fullName && (
          <p style={{ fontSize: "0.875rem", color: "#64748b", margin: 0 }}>{member.displayName}</p>
        )}
      </div>
      <span style={{
        display: "inline-flex", alignItems: "center", gap: "0.375rem",
        padding: "0.25rem 0.75rem", borderRadius: 999, fontSize: "0.8125rem", fontWeight: 600,
        ...(member.invitationStatus === "accepted"
          ? { background: "#f0fdf4", color: "#16a34a", border: "1px solid #bbf7d0" }
          : { background: "#fffbeb", color: "#d97706", border: "1px solid #fde68a" }),
      }}>
        {member.invitationStatus === "accepted" ? "מאושר ✓" : "ממתין לאישור"}
      </span>
      {member.phoneNumber && (
        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", color: "#475569", fontSize: "0.875rem" }}>
          <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z" />
          </svg>
          {member.phoneNumber}
        </div>
      )}
      {member.isOwner && (
        <span style={{ display: "inline-flex", padding: "0.25rem 0.75rem", borderRadius: 999, fontSize: "0.8125rem", fontWeight: 600, background: "#fffbeb", color: "#d97706", border: "1px solid #fde68a" }}>
          בעלים
        </span>
      )}
      {isAdmin && !member.isOwner && (
        <button
          onClick={onEdit}
          style={{ background: "#3b82f6", color: "white", border: "none", borderRadius: 10, padding: "0.625rem 1.5rem", fontSize: "0.875rem", fontWeight: 600, cursor: "pointer" }}
        >
          ערוך פרטים
        </button>
      )}
    </div>
  );
}

function MemberEditView({ form, saving, error, onChange, onSave, onCancel }: {
  form: MemberEditForm;
  saving: boolean;
  error: string | null;
  onChange: (f: MemberEditForm) => void;
  onSave: () => void;
  onCancel: () => void;
}) {
  const fields: { label: string; key: keyof MemberEditForm; type: string }[] = [
    { label: "שם מלא", key: "fullName", type: "text" },
    { label: "שם תצוגה", key: "displayName", type: "text" },
    { label: "מספר טלפון", key: "phoneNumber", type: "tel" },
    { label: "תאריך לידה", key: "birthday", type: "date" },
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
      <h2 style={{ fontSize: "1rem", fontWeight: 600, color: "#0f172a", margin: 0 }}>עריכת פרטי חבר</h2>
      {fields.map(f => (
        <div key={f.key}>
          <label style={{ display: "block", fontSize: "0.75rem", fontWeight: 600, color: "#94a3b8", marginBottom: "0.25rem" }}>{f.label}</label>
          <input
            type={f.type}
            value={form[f.key]}
            onChange={e => onChange({ ...form, [f.key]: e.target.value })}
            style={{ width: "100%", border: "1px solid #e2e8f0", borderRadius: 10, padding: "0.625rem 0.875rem", fontSize: "0.875rem", color: "#0f172a", outline: "none", boxSizing: "border-box" }}
          />
        </div>
      ))}
      <div>
        <label style={{ display: "block", fontSize: "0.75rem", fontWeight: 600, color: "#94a3b8", marginBottom: "0.5rem" }}>תמונת פרופיל</label>
        <ImageUpload
          value={form.profileImageUrl || null}
          onChange={url => onChange({ ...form, profileImageUrl: url })}
          shape="circle"
          size={72}
          label="העלה תמונה"
          disabled={saving}
        />
      </div>
      {error && <p style={{ fontSize: "0.875rem", color: "#dc2626", margin: 0 }}>{error}</p>}
      <div style={{ display: "flex", gap: "0.75rem" }}>
        <button
          onClick={onSave}
          disabled={saving}
          style={{ background: saving ? "#93c5fd" : "#3b82f6", color: "white", border: "none", borderRadius: 10, padding: "0.625rem 1.25rem", fontSize: "0.875rem", fontWeight: 600, cursor: saving ? "not-allowed" : "pointer" }}
        >
          {saving ? "שומר..." : "שמור"}
        </button>
        <button
          onClick={onCancel}
          style={{ background: "none", border: "1px solid #e2e8f0", borderRadius: 10, padding: "0.625rem 1.25rem", fontSize: "0.875rem", color: "#64748b", cursor: "pointer" }}
        >
          ביטול
        </button>
      </div>
    </div>
  );
}
