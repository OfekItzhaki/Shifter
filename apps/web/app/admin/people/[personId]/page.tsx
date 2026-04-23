"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import AppShell from "@/components/shell/AppShell";
import {
  getPersonDetail, addRestriction, getSpaceRoles, assignRole, removeRole,
  PersonDetailDto, RoleDto,
} from "@/lib/api/people";
import { apiClient } from "@/lib/api/client";
import {
  getAvailabilityWindows, addAvailabilityWindow,
  getPresenceWindows, addPresenceWindow,
  AvailabilityWindowDto, PresenceWindowDto,
} from "@/lib/api/availability";
import { useSpaceStore } from "@/lib/store/spaceStore";

function fmt(dt: string) {
  return new Date(dt).toLocaleString([], { dateStyle: "short", timeStyle: "short" });
}

export default function PersonDetailPage() {
  const { personId } = useParams<{ personId: string }>();
  const { currentSpaceId } = useSpaceStore();
  const [person, setPerson] = useState<PersonDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState<string | null>(null);

  // Restriction form
  const [showRestriction, setShowRestriction] = useState(false);
  const [restrictionType, setRestrictionType] = useState("no_task_type_restriction");
  const [effectiveFrom, setEffectiveFrom] = useState("");
  const [effectiveUntil, setEffectiveUntil] = useState("");
  const [note, setNote] = useState("");
  const [saving, setSaving] = useState(false);

  // Role assignment
  const [allRoles, setAllRoles] = useState<RoleDto[]>([]);
  const [selectedRoleId, setSelectedRoleId] = useState("");
  const [roleWorking, setRoleWorking] = useState(false);

  // Availability windows
  const [availability, setAvailability] = useState<AvailabilityWindowDto[]>([]);
  const [showAvailForm, setShowAvailForm] = useState(false);
  const [availStart, setAvailStart] = useState("");
  const [availEnd, setAvailEnd] = useState("");
  const [availNote, setAvailNote] = useState("");
  const [savingAvail, setSavingAvail] = useState(false);

  // Presence windows
  const [presence, setPresence] = useState<PresenceWindowDto[]>([]);
  const [showPresenceForm, setShowPresenceForm] = useState(false);
  const [presenceState, setPresenceState] = useState<"at_home" | "free_in_base">("at_home");
  const [presenceStart, setPresenceStart] = useState("");
  const [presenceEnd, setPresenceEnd] = useState("");
  const [presenceNote, setPresenceNote] = useState("");
  const [savingPresence, setSavingPresence] = useState(false);

  // Qualifications
  const [showQualForm, setShowQualForm] = useState(false);
  const [newQual, setNewQual] = useState("");
  const [savingQual, setSavingQual] = useState(false);

  async function reload() {
    if (!currentSpaceId || !personId) return;
    const [p, roles, avail, pres] = await Promise.all([
      getPersonDetail(currentSpaceId, personId),
      getSpaceRoles(currentSpaceId),
      getAvailabilityWindows(currentSpaceId, personId),
      getPresenceWindows(currentSpaceId, personId),
    ]);
    setPerson(p);
    setAllRoles(roles);
    setAvailability(avail);
    setPresence(pres);
  }

  useEffect(() => {
    reload().finally(() => setLoading(false));
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentSpaceId, personId]);

  async function handleAddRestriction(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId || !personId) return;
    setSaving(true);
    try {
      await addRestriction(currentSpaceId, personId, restrictionType,
        effectiveFrom, effectiveUntil || null, note || null, null);
      await reload();
      setShowRestriction(false);
      setMessage("הגבלה נוספה בהצלחה.");
    } catch { setMessage("שגיאה בהוספת הגבלה."); }
    finally { setSaving(false); }
  }

  async function handleAssignRole() {
    if (!currentSpaceId || !personId || !selectedRoleId) return;
    setRoleWorking(true);
    try {
      await assignRole(currentSpaceId, personId, selectedRoleId);
      await reload();
      setSelectedRoleId("");
      setMessage("תפקיד הוקצה בהצלחה.");
    } catch { setMessage("שגיאה בהקצאת תפקיד."); }
    finally { setRoleWorking(false); }
  }

  async function handleRemoveRole(roleId: string) {
    if (!currentSpaceId || !personId) return;
    setRoleWorking(true);
    try {
      await removeRole(currentSpaceId, personId, roleId);
      await reload();
      setMessage("תפקיד הוסר.");
    } catch { setMessage("שגיאה בהסרת תפקיד."); }
    finally { setRoleWorking(false); }
  }

  async function handleAddAvailability(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId || !personId) return;
    setSavingAvail(true);
    try {
      await addAvailabilityWindow(currentSpaceId, personId,
        availStart, availEnd, availNote || null);
      await reload();
      setShowAvailForm(false);
      setAvailStart(""); setAvailEnd(""); setAvailNote("");
      setMessage("חלון זמינות נוסף.");
    } catch { setMessage("שגיאה בהוספת חלון זמינות."); }
    finally { setSavingAvail(false); }
  }

  async function handleAddPresence(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId || !personId) return;
    setSavingPresence(true);
    try {
      await addPresenceWindow(currentSpaceId, personId,
        presenceState, presenceStart, presenceEnd, presenceNote || null);
      await reload();
      setShowPresenceForm(false);
      setPresenceStart(""); setPresenceEnd(""); setPresenceNote("");
      setMessage("חלון נוכחות נוסף.");
    } catch { setMessage("שגיאה בהוספת חלון נוכחות."); }
    finally { setSavingPresence(false); }
  }

  async function handleAddQualification(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId || !personId || !newQual.trim()) return;
    setSavingQual(true);
    try {
      await apiClient.post(`/spaces/${currentSpaceId}/people/${personId}/qualifications`, {
        qualification: newQual.trim(), issuedAt: null, expiresAt: null,
      });
      await reload();
      setNewQual(""); setShowQualForm(false);
      setMessage("כישור נוסף בהצלחה");
    } catch { setMessage("שגיאה בהוספת כישור"); }
    finally { setSavingQual(false); }
  }

  const assignedRoleIds = new Set(person?.roles?.map(r => r.roleId) ?? []);
  const availableRoles = allRoles.filter(r => !assignedRoleIds.has(r.id));

  if (loading) return <AppShell><p className="text-gray-400 text-sm">טוען...</p></AppShell>;
  if (!person) return <AppShell><p className="text-gray-500 text-sm">אדם לא נמצא.</p></AppShell>;

  return (
    <AppShell>
      <div className="space-y-6 max-w-2xl">
        <div>
          <h1 className="text-xl font-semibold">{person.fullName}</h1>
          {person.displayName && <p className="text-gray-500 text-sm">{person.displayName}</p>}
        </div>

        {message && <p className="text-sm text-green-600">{message}</p>}

        {/* Roles */}
        <div className="bg-white border border-gray-200 rounded-xl p-4 space-y-3">
          <h2 className="text-xs font-semibold text-gray-500 uppercase">תפקידים</h2>
          <div className="flex flex-wrap gap-1 min-h-[24px]">
            {(person.roles ?? []).length === 0
              ? <p className="text-xs text-gray-400">אין תפקידים מוקצים</p>
              : (person.roles ?? []).map(r => (
                <span key={r.roleId}
                  className="inline-flex items-center gap-1 bg-blue-50 text-blue-700 text-xs px-2 py-0.5 rounded-full">
                  {r.name}
                  <button onClick={() => handleRemoveRole(r.roleId)} disabled={roleWorking}
                    className="text-blue-400 hover:text-red-500 disabled:opacity-40 leading-none"
                    aria-label={`הסר תפקיד ${r.name}`}>×</button>
                </span>
              ))}
          </div>
          {availableRoles.length > 0 && (
            <div className="flex gap-2 items-center border-t pt-3">
              <select value={selectedRoleId} onChange={e => setSelectedRoleId(e.target.value)}
                className="flex-1 border rounded-lg px-3 py-1.5 text-sm">
                <option value="">בחר תפקיד להקצאה...</option>
                {availableRoles.map(r => (
                  <option key={r.id} value={r.id}>{r.name}</option>
                ))}
              </select>
              <button onClick={handleAssignRole} disabled={!selectedRoleId || roleWorking}
                className="bg-blue-600 text-white text-xs px-3 py-1.5 rounded-lg hover:bg-blue-700 disabled:opacity-50">
                {roleWorking ? "…" : "הקצה"}
              </button>
            </div>
          )}
        </div>

        {/* Groups */}
        <div className="bg-white border border-gray-200 rounded-xl p-4">
          <h2 className="text-xs font-semibold text-gray-500 uppercase mb-2">קבוצות</h2>
          {person.groupNames.length === 0
            ? <p className="text-xs text-gray-400">לא משויך לקבוצות</p>
            : person.groupNames.map(g => (
              <span key={g} className="inline-block bg-gray-100 text-gray-700 text-xs px-2 py-0.5 rounded-full me-1 mb-1">{g}</span>
            ))}
        </div>

        {/* Qualifications */}
        <div className="bg-white border border-gray-200 rounded-xl p-4 space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="text-xs font-semibold text-gray-500 uppercase">כישורים</h2>
            <button onClick={() => setShowQualForm(!showQualForm)}
              className="text-xs text-blue-600 hover:underline">+ הוסף</button>
          </div>
          {showQualForm && (
            <form onSubmit={handleAddQualification} className="flex gap-2 border-t pt-3">
              <input value={newQual} onChange={e => setNewQual(e.target.value)} required
                placeholder="לדוגמה: חובש, נהג" className="flex-1 border rounded-lg px-3 py-1.5 text-sm" />
              <button type="submit" disabled={savingQual}
                className="bg-green-600 text-white text-xs px-3 py-1.5 rounded-lg hover:bg-green-700 disabled:opacity-50">
                {savingQual ? "..." : "שמור"}
              </button>
            </form>
          )}
          {person.qualifications.length === 0
            ? <p className="text-xs text-gray-400">אין כישורים</p>
            : person.qualifications.map(q => (
              <span key={q} className="inline-block bg-green-50 text-green-700 text-xs px-2 py-0.5 rounded-full me-1 mb-1">{q}</span>
            ))}
        </div>

        {/* Availability Windows */}
        <div className="bg-white border border-gray-200 rounded-xl p-4 space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="text-xs font-semibold text-gray-500 uppercase">חלונות זמינות</h2>
            <button onClick={() => setShowAvailForm(!showAvailForm)}
              className="text-xs text-blue-600 hover:underline">+ הוסף</button>
          </div>
          {showAvailForm && (
            <form onSubmit={handleAddAvailability} className="space-y-3 border-t pt-3">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="text-xs text-gray-500">מתאריך</label>
                  <input type="datetime-local" value={availStart}
                    onChange={e => setAvailStart(e.target.value)} required
                    className="w-full border rounded-lg px-3 py-2 text-sm mt-1" />
                </div>
                <div>
                  <label className="text-xs text-gray-500">עד תאריך</label>
                  <input type="datetime-local" value={availEnd}
                    onChange={e => setAvailEnd(e.target.value)} required
                    className="w-full border rounded-lg px-3 py-2 text-sm mt-1" />
                </div>
                <div className="col-span-2">
                  <label className="text-xs text-gray-500">הערה (אופציונלי)</label>
                  <input value={availNote} onChange={e => setAvailNote(e.target.value)}
                    className="w-full border rounded-lg px-3 py-2 text-sm mt-1" placeholder="הערה אופציונלית" />
                </div>
              </div>
              <button type="submit" disabled={savingAvail}
                className="bg-blue-600 text-white text-xs px-3 py-1.5 rounded-lg hover:bg-blue-700 disabled:opacity-50">
                {savingAvail ? "..." : "שמור"}
              </button>
            </form>
          )}
          {availability.length === 0
            ? <p className="text-xs text-gray-400">אין חלונות זמינות</p>
            : availability.map(a => (
              <div key={a.id} className="text-sm border-t pt-2">
                <span className="font-medium">{fmt(a.startsAt)}</span>
                <span className="text-gray-400 text-xs mx-1">→</span>
                <span className="font-medium">{fmt(a.endsAt)}</span>
                {a.note && <p className="text-xs text-gray-500 mt-0.5">{a.note}</p>}
              </div>
            ))}
        </div>

        {/* Presence Windows */}
        <div className="bg-white border border-gray-200 rounded-xl p-4 space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="text-xs font-semibold text-gray-500 uppercase">חלונות נוכחות</h2>
            <button onClick={() => setShowPresenceForm(!showPresenceForm)}
              className="text-xs text-blue-600 hover:underline">+ הוסף</button>
          </div>
          {showPresenceForm && (
            <form onSubmit={handleAddPresence} className="space-y-3 border-t pt-3">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="text-xs text-gray-500">מצב</label>
                  <select value={presenceState}
                    onChange={e => setPresenceState(e.target.value as "at_home" | "free_in_base")}
                    className="w-full border rounded-lg px-3 py-2 text-sm mt-1">
                    <option value="at_home">לא זמין</option>
                    <option value="free_in_base">זמין חלקית</option>
                  </select>
                </div>
                <div />
                <div>
                  <label className="text-xs text-gray-500">מתאריך</label>
                  <input type="datetime-local" value={presenceStart}
                    onChange={e => setPresenceStart(e.target.value)} required
                    className="w-full border rounded-lg px-3 py-2 text-sm mt-1" />
                </div>
                <div>
                  <label className="text-xs text-gray-500">עד תאריך</label>
                  <input type="datetime-local" value={presenceEnd}
                    onChange={e => setPresenceEnd(e.target.value)} required
                    className="w-full border rounded-lg px-3 py-2 text-sm mt-1" />
                </div>
                <div className="col-span-2">
                  <label className="text-xs text-gray-500">הערה (אופציונלי)</label>
                  <input value={presenceNote} onChange={e => setPresenceNote(e.target.value)}
                    className="w-full border rounded-lg px-3 py-2 text-sm mt-1" placeholder="הערה אופציונלית" />
                </div>
              </div>
              <button type="submit" disabled={savingPresence}
                className="bg-blue-600 text-white text-xs px-3 py-1.5 rounded-lg hover:bg-blue-700 disabled:opacity-50">
                {savingPresence ? "..." : "שמור"}
              </button>
            </form>
          )}
          {presence.length === 0
            ? <p className="text-xs text-gray-400">אין חלונות נוכחות</p>
            : presence.map(p => (
              <div key={p.id} className="text-sm border-t pt-2">
                <span className="inline-block bg-purple-50 text-purple-700 text-xs px-2 py-0.5 rounded-full me-2">
                  {p.state === "at_home" ? "לא זמין" : "זמין חלקית"}
                </span>
                <span className="text-gray-500 text-xs">{fmt(p.startsAt)} → {fmt(p.endsAt)}</span>
                {p.isDerived && <span className="text-xs text-gray-400 ms-2">(נגזר)</span>}
                {p.note && <p className="text-xs text-gray-500 mt-0.5">{p.note}</p>}
              </div>
            ))}
        </div>

        {/* Restrictions */}
        <div className="bg-white border border-gray-200 rounded-xl p-4 space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="text-xs font-semibold text-gray-500 uppercase">הגבלות</h2>
            <button onClick={() => setShowRestriction(!showRestriction)}
              className="text-xs text-blue-600 hover:underline">+ הוסף</button>
          </div>
          {showRestriction && (
            <form onSubmit={handleAddRestriction} className="space-y-3 border-t pt-3">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="text-xs text-gray-500">סוג</label>
                  <select value={restrictionType} onChange={e => setRestrictionType(e.target.value)}
                    className="w-full border rounded-lg px-3 py-2 text-sm mt-1">
                    <option value="no_task_type_restriction">ללא משימה ספציפית</option>
                    <option value="no_night">ללא משמרות לילה</option>
                    <option value="no_kitchen">ללא מטבח</option>
                    <option value="medical_leave">חופשת מחלה</option>
                  </select>
                </div>
                <div>
                  <label className="text-xs text-gray-500">מתאריך</label>
                  <input type="date" value={effectiveFrom} onChange={e => setEffectiveFrom(e.target.value)}
                    required className="w-full border rounded-lg px-3 py-2 text-sm mt-1" />
                </div>
                <div>
                  <label className="text-xs text-gray-500">עד תאריך (אופציונלי)</label>
                  <input type="date" value={effectiveUntil} onChange={e => setEffectiveUntil(e.target.value)}
                    className="w-full border rounded-lg px-3 py-2 text-sm mt-1" />
                </div>
                <div>
                  <label className="text-xs text-gray-500">הערה</label>
                  <input value={note} onChange={e => setNote(e.target.value)}
                    className="w-full border rounded-lg px-3 py-2 text-sm mt-1" placeholder="הערה אופציונלית" />
                </div>
              </div>
              <button type="submit" disabled={saving}
                className="bg-blue-600 text-white text-xs px-3 py-1.5 rounded-lg hover:bg-blue-700 disabled:opacity-50">
                {saving ? "..." : "שמור הגבלה"}
              </button>
            </form>
          )}
          {person.restrictions.length === 0
            ? <p className="text-xs text-gray-400">אין הגבלות</p>
            : person.restrictions.map(r => (
              <div key={r.id} className="text-sm border-t pt-2">
                <span className="font-medium">{r.restrictionType}</span>
                <span className="text-gray-400 text-xs ms-2">
                  {r.effectiveFrom} {r.effectiveUntil ? `→ ${r.effectiveUntil}` : "ואילך"}
                </span>
                {r.operationalNote && <p className="text-xs text-gray-500 mt-0.5">{r.operationalNote}</p>}
              </div>
            ))}
        </div>
      </div>
    </AppShell>
  );
}
