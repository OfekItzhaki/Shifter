"use client";

import { useEffect, useState, useCallback, useMemo } from "react";
import { useTranslations } from "next-intl";
import {
  getAvailableSlots,
  adminAssignMember,
  adminRemoveMember,
  AvailableSlotDto,
  AvailableSlotsResponse,
} from "@/lib/api/selfService";
import { formatSlotDate, formatTime24h, HEBREW_DAY_NAMES } from "@/lib/utils/selfServiceFormat";
import { getSelfServiceErrorMessage } from "@/lib/utils/selfServiceErrors";
import type { GroupMemberDto } from "@/lib/api/groups";
import Modal from "@/components/Modal";
import { LoadingCard, ErrorRetry, MutationButton } from "@/components/groups/selfService";

interface AdminOverridesTabProps {
  spaceId: string;
  groupId: string;
  members: GroupMemberDto[];
  hasSchedulePublishPermission: boolean;
}

/** Tracks which members are assigned to each slot */
interface SlotAssignment {
  personId: string;
  personName: string;
}

/**
 * AdminOverridesTab — allows admins to manually assign or remove members from shift slots.
 *
 * Validates: Requirements 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7
 */
export default function AdminOverridesTab({
  spaceId,
  groupId,
  members,
  hasSchedulePublishPermission,
}: AdminOverridesTabProps) {
  const t = useTranslations("selfService.adminOverrides");
  const tCommon = useTranslations("selfService");

  // ── State ────────────────────────────────────────────────────────────────
  const [slotsResponse, setSlotsResponse] = useState<AvailableSlotsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Per-slot assigned members tracking
  const [slotAssignments, setSlotAssignments] = useState<Record<string, SlotAssignment[]>>({});

  // Assign flow state
  const [assignSlotId, setAssignSlotId] = useState<string | null>(null);
  const [selectedPersonId, setSelectedPersonId] = useState<string>("");
  const [assigning, setAssigning] = useState(false);
  const [assignError, setAssignError] = useState<string | null>(null);

  // Remove flow state
  const [removeTarget, setRemoveTarget] = useState<{ slotId: string; personId: string; personName: string } | null>(null);
  const [removing, setRemoving] = useState(false);
  const [removeError, setRemoveError] = useState<string | null>(null);

  // Action error per slot (inline)
  const [slotActionError, setSlotActionError] = useState<{ slotId: string; message: string } | null>(null);

  // ── Fetch slots ──────────────────────────────────────────────────────────
  const fetchSlots = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getAvailableSlots(spaceId, groupId, "current");
      setSlotsResponse(data);
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setError(message);
    } finally {
      setLoading(false);
    }
  }, [spaceId, groupId]);

  useEffect(() => {
    if (hasSchedulePublishPermission) {
      fetchSlots();
    } else {
      setLoading(false);
    }
  }, [fetchSlots, hasSchedulePublishPermission]);

  // ── Sorted slots ─────────────────────────────────────────────────────────
  const sortedSlots = useMemo(() => {
    if (!slotsResponse) return [];
    const slots = [...slotsResponse.slots];
    slots.sort((a, b) => {
      const dateCompare = a.date.localeCompare(b.date);
      if (dateCompare !== 0) return dateCompare;
      return a.startTime.localeCompare(b.startTime);
    });
    return slots;
  }, [slotsResponse]);

  // ── Helpers ──────────────────────────────────────────────────────────────

  /** Get members available for assignment to a specific slot (not already assigned) */
  function getAvailableMembersForSlot(slotId: string): GroupMemberDto[] {
    const assigned = slotAssignments[slotId] ?? [];
    const assignedIds = new Set(assigned.map((a) => a.personId));
    return members.filter((m) => !assignedIds.has(m.personId));
  }

  function getMemberDisplayName(member: GroupMemberDto): string {
    return member.displayName ?? member.fullName;
  }

  // ── Assign handlers ──────────────────────────────────────────────────────

  function openAssignPicker(slotId: string) {
    setAssignSlotId(slotId);
    setSelectedPersonId("");
    setAssignError(null);
  }

  function closeAssignPicker() {
    setAssignSlotId(null);
    setSelectedPersonId("");
    setAssignError(null);
  }

  async function handleAssignConfirm() {
    if (!assignSlotId || !selectedPersonId) return;

    setAssigning(true);
    setAssignError(null);
    setSlotActionError(null);

    try {
      await adminAssignMember(spaceId, groupId, assignSlotId, selectedPersonId);

      // Update local state
      const member = members.find((m) => m.personId === selectedPersonId);
      if (member) {
        setSlotAssignments((prev) => ({
          ...prev,
          [assignSlotId]: [
            ...(prev[assignSlotId] ?? []),
            { personId: member.personId, personName: getMemberDisplayName(member) },
          ],
        }));
      }

      // Update slot fill count
      setSlotsResponse((prev) => {
        if (!prev) return prev;
        return {
          ...prev,
          slots: prev.slots.map((s) =>
            s.id === assignSlotId
              ? { ...s, currentFillCount: s.currentFillCount + 1 }
              : s
          ),
        };
      });

      closeAssignPicker();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setAssignError(message);
    } finally {
      setAssigning(false);
    }
  }

  // ── Remove handlers ──────────────────────────────────────────────────────

  function openRemoveConfirm(slotId: string, personId: string, personName: string) {
    setRemoveTarget({ slotId, personId, personName });
    setRemoveError(null);
  }

  function closeRemoveConfirm() {
    setRemoveTarget(null);
    setRemoveError(null);
  }

  async function handleRemoveConfirm() {
    if (!removeTarget) return;

    setRemoving(true);
    setRemoveError(null);
    setSlotActionError(null);

    try {
      await adminRemoveMember(spaceId, groupId, removeTarget.slotId, removeTarget.personId);

      // Update local state
      setSlotAssignments((prev) => ({
        ...prev,
        [removeTarget.slotId]: (prev[removeTarget.slotId] ?? []).filter(
          (a) => a.personId !== removeTarget.personId
        ),
      }));

      // Update slot fill count
      setSlotsResponse((prev) => {
        if (!prev) return prev;
        return {
          ...prev,
          slots: prev.slots.map((s) =>
            s.id === removeTarget.slotId
              ? { ...s, currentFillCount: Math.max(0, s.currentFillCount - 1) }
              : s
          ),
        };
      });

      closeRemoveConfirm();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setRemoveError(message);
    } finally {
      setRemoving(false);
    }
  }

  // ── Permission denied state ──────────────────────────────────────────────
  if (!hasSchedulePublishPermission) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-center bg-white rounded-xl border border-slate-200">
        <div className="flex items-center justify-center w-12 h-12 rounded-full bg-red-50 mb-3">
          <svg width={24} height={24} viewBox="0 0 24 24" fill="none" className="text-red-400" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round">
            <path d="M12 9v2m0 4h.01M5.07 19h13.86c1.54 0 2.5-1.67 1.73-3L13.73 4c-.77-1.33-2.69-1.33-3.46 0L3.34 16c-.77 1.33.19 3 1.73 3z" />
          </svg>
        </div>
        <p className="text-sm text-red-600 font-medium">{t("noPermission")}</p>
      </div>
    );
  }

  // ── Loading state ────────────────────────────────────────────────────────
  if (loading) {
    return <LoadingCard rows={4} variant="slots" />;
  }

  // ── Error state ──────────────────────────────────────────────────────────
  if (error) {
    return <ErrorRetry message={error} onRetry={fetchSlots} />;
  }

  if (!slotsResponse) return null;

  // ── Render ───────────────────────────────────────────────────────────────
  return (
    <div className="space-y-4">
      {/* Title */}
      <div className="flex items-center justify-between bg-white border border-slate-200 rounded-xl px-4 py-3">
        <span className="text-sm font-medium text-slate-700">{t("title")}</span>
      </div>

      {/* Empty state */}
      {sortedSlots.length === 0 && (
        <div className="flex flex-col items-center justify-center py-12 text-center bg-white rounded-xl border border-slate-200">
          <p className="text-sm text-slate-400">{tCommon("error")}</p>
        </div>
      )}

      {/* Slot list */}
      <div className="space-y-3">
        {sortedSlots.map((slot) => {
          const assignments = slotAssignments[slot.id] ?? [];
          const availableMembers = getAvailableMembersForSlot(slot.id);
          const isAssignOpen = assignSlotId === slot.id;

          return (
            <div
              key={slot.id}
              className="bg-white border border-slate-200 rounded-xl px-4 py-3"
            >
              {/* Slot header */}
              <div className="flex items-center justify-between gap-3">
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="text-sm font-medium text-slate-900">
                      {formatSlotDate(slot.date)}
                    </span>
                    <span className="text-xs text-slate-500">
                      {formatTime24h(slot.startTime)} – {formatTime24h(slot.endTime)}
                    </span>
                  </div>
                  <div className="flex items-center gap-2 mt-1">
                    <span className="text-xs text-slate-600">{slot.taskName}</span>
                    <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border bg-slate-50 text-slate-700 border-slate-200">
                      {slot.currentFillCount}/{slot.capacity}
                    </span>
                  </div>
                </div>

                {/* Assign button */}
                <button
                  onClick={() => openAssignPicker(slot.id)}
                  disabled={slot.currentFillCount >= slot.capacity}
                  className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-sky-600 text-white hover:bg-sky-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  <svg width={14} height={14} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
                  </svg>
                  {t("assignButton")}
                </button>
              </div>

              {/* Assigned members list */}
              {assignments.length > 0 && (
                <div className="mt-3 border-t border-slate-100 pt-3 space-y-2">
                  {assignments.map((assignment) => (
                    <div
                      key={assignment.personId}
                      className="flex items-center justify-between gap-2 bg-slate-50 rounded-lg px-3 py-2"
                    >
                      <span className="text-xs text-slate-700 font-medium">
                        {assignment.personName}
                      </span>
                      <button
                        onClick={() => openRemoveConfirm(slot.id, assignment.personId, assignment.personName)}
                        className="text-xs text-red-600 hover:text-red-700 border border-red-200 bg-red-50 hover:bg-red-100 px-2 py-1 rounded-lg transition-colors"
                      >
                        {t("removeButton")}
                      </button>
                    </div>
                  ))}
                </div>
              )}

              {/* Inline assign picker */}
              {isAssignOpen && (
                <div className="mt-3 border-t border-slate-100 pt-3">
                  <div className="flex items-center gap-2">
                    <select
                      value={selectedPersonId}
                      onChange={(e) => setSelectedPersonId(e.target.value)}
                      className="flex-1 border border-slate-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-400 focus:border-transparent"
                      dir="rtl"
                    >
                      <option value="">{t("selectMember")}</option>
                      {availableMembers.map((member) => (
                        <option key={member.personId} value={member.personId}>
                          {getMemberDisplayName(member)}
                        </option>
                      ))}
                    </select>
                    <button
                      onClick={handleAssignConfirm}
                      disabled={!selectedPersonId || assigning}
                      className="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg text-xs font-medium bg-emerald-600 text-white hover:bg-emerald-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      {assigning && (
                        <svg className="animate-spin h-3.5 w-3.5" fill="none" viewBox="0 0 24 24">
                          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                        </svg>
                      )}
                      {assigning ? t("assigning") : t("confirmYes")}
                    </button>
                    <button
                      onClick={closeAssignPicker}
                      className="px-3 py-2 rounded-lg text-xs font-medium text-slate-600 border border-slate-200 hover:bg-slate-50 transition-colors"
                    >
                      {t("confirmNo")}
                    </button>
                  </div>

                  {/* No available members message */}
                  {availableMembers.length === 0 && (
                    <p className="text-xs text-slate-400 mt-2">{t("noMembers")}</p>
                  )}

                  {/* Assign error */}
                  {assignError && (
                    <p className="text-xs text-red-600 mt-2">{assignError}</p>
                  )}
                </div>
              )}

              {/* Slot action error */}
              {slotActionError?.slotId === slot.id && (
                <div className="mt-2 text-xs text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2">
                  {slotActionError.message}
                </div>
              )}
            </div>
          );
        })}
      </div>

      {/* Remove confirmation modal */}
      <Modal
        open={!!removeTarget}
        onClose={closeRemoveConfirm}
        title={t("removeConfirmTitle")}
      >
        <div className="space-y-4">
          <p className="text-sm text-slate-600">
            {t("removeConfirmMessage", { name: removeTarget?.personName ?? "" })}
          </p>

          {/* Remove error */}
          {removeError && (
            <p className="text-xs text-red-600">{removeError}</p>
          )}

          {/* Actions */}
          <div className="flex gap-3 justify-end">
            <button
              onClick={closeRemoveConfirm}
              className="px-4 py-2 text-sm text-slate-600 hover:text-slate-800 border border-slate-200 rounded-lg transition-colors"
              disabled={removing}
            >
              {t("confirmNo")}
            </button>
            <button
              onClick={handleRemoveConfirm}
              disabled={removing}
              className="px-4 py-2 text-sm text-white bg-red-600 hover:bg-red-700 rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
            >
              {removing && (
                <svg className="animate-spin h-4 w-4" fill="none" viewBox="0 0 24 24">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                </svg>
              )}
              {removing ? t("removing") : t("confirmYes")}
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
