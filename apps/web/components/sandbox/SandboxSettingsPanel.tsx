"use client";

import { useState, useCallback } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useSandboxStore } from "@/lib/store/sandboxStore";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { buildOverridePayload } from "@/lib/store/sandboxPayloadBuilder";
import { runSimulation } from "@/lib/api/simulation";
import { publishSandbox, type PublishSandboxRequest, type TaskOverrideDto, type ConstraintOverrideDto, type SettingsOverrideDto } from "@/lib/api/simulation";
import { discardVersion } from "@/lib/api/schedule";
import { useSandboxNavigationGuard } from "@/hooks/useSandboxNavigationGuard";
import SandboxNavigationGuardDialog from "./SandboxNavigationGuardDialog";
import SandboxSettingsTab from "./SandboxSettingsTab";
import SandboxTasksTab from "./SandboxTasksTab";
import SandboxConstraintsTab from "./SandboxConstraintsTab";
import SandboxMembersTab from "./SandboxMembersTab";

type TabId = "tasks" | "constraints" | "members" | "settings";

/**
 * SandboxSettingsPanel — Left panel in the sandbox split view.
 *
 * Renders tabs for Tasks, Constraints, Members, and Settings.
 * Each tab reads from and writes to the Zustand sandbox store.
 *
 * IMPORTANT: This component does NOT subscribe to `lastSimulationResult`
 * to prevent re-renders when simulation completes (Req 8.1, 8.4).
 */
export default function SandboxSettingsPanel() {
  const t = useTranslations("sandbox");
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<TabId>("tasks");
  const [showDiscardConfirm, setShowDiscardConfirm] = useState(false);
  const [isDiscarding, setIsDiscarding] = useState(false);
  const [discardError, setDiscardError] = useState<string | null>(null);

  // Navigation guard — warns admin when navigating away with unsaved changes (Req 7.3, 7.4)
  const { showLeaveDialog, confirmLeave, cancelLeave } = useSandboxNavigationGuard();

  const [isPublishing, setIsPublishing] = useState(false);
  const [publishError, setPublishError] = useState<string | null>(null);

  // Subscribe only to override state — NOT simulation results
  const taskOverrides = useSandboxStore((s) => s.taskOverrides);
  const constraintOverrides = useSandboxStore((s) => s.constraintOverrides);
  const memberExclusions = useSandboxStore((s) => s.memberExclusions);
  const settingsOverrides = useSandboxStore((s) => s.settingsOverrides);
  const baseline = useSandboxStore((s) => s.baseline);
  const isSimulating = useSandboxStore((s) => s.isSimulating);

  const handleRunSimulation = useCallback(async () => {
    const store = useSandboxStore.getState();
    const spaceId = useSpaceStore.getState().currentSpaceId;

    if (!store.baseline || !spaceId || !store.groupId) return;

    // Build the override payload from current sandbox state
    const payload = buildOverridePayload(
      store.baseline,
      store.taskOverrides,
      store.constraintOverrides,
      store.memberExclusions,
      store.settingsOverrides
    );

    // Set simulating state before request
    store.setSimulating(true);
    store.setSimulationError(null);

    try {
      const result = await runSimulation(spaceId, store.groupId, payload);

      if (result.timed_out) {
        store.setSimulationError(t("errors.solverTimeout"));
      } else if (!result.feasible) {
        store.setSimulationResult(result);
      } else {
        store.setSimulationResult(result);
      }
    } catch {
      store.setSimulationError(t("errors.networkError"));
    } finally {
      store.setSimulating(false);
    }
  }, [t]);

  /**
   * Publish flow: construct PublishSandboxRequest from sandbox state,
   * call POST /publish-sandbox, then exit sandbox and navigate to group view.
   * Requirements: 9.1, 9.2, 9.3, 9.4, 9.5
   */
  const handlePublish = useCallback(async () => {
    const store = useSandboxStore.getState();
    const spaceId = useSpaceStore.getState().currentSpaceId;

    if (!spaceId || !store.groupId || !store.draftVersionId) return;

    setIsPublishing(true);
    setPublishError(null);

    try {
      // Transform frontend Map-based task overrides into backend DTO format
      const taskOverrideDtos: TaskOverrideDto[] = [];
      store.taskOverrides.forEach((override, slotId) => {
        const dto: TaskOverrideDto = { action: override.action };

        if (override.action === "edit" || override.action === "remove") {
          dto.existingTaskId = slotId;
        }

        if (override.action === "add" || override.action === "edit") {
          const mod = override.modified;
          if (mod) {
            dto.name = mod.taskTypeName ?? null;
            dto.startsAt = mod.startsAt ?? null;
            dto.endsAt = mod.endsAt ?? null;
            dto.requiredHeadcount = mod.requiredHeadcount ?? null;
            dto.burdenLevel = mod.burdenLevel ?? null;
            dto.requiredQualificationNames = mod.qualificationRequirements
              ?.map((q) => q.qualification_name) ?? null;
          }
        }

        taskOverrideDtos.push(dto);
      });

      // Transform frontend Map-based constraint overrides into backend DTO format
      const constraintOverrideDtos: ConstraintOverrideDto[] = [];
      store.constraintOverrides.forEach((override, constraintId) => {
        const dto: ConstraintOverrideDto = { action: override.action };

        if (override.action === "edit" || override.action === "remove") {
          dto.existingConstraintId = constraintId;
        }

        if (override.action === "add" || override.action === "edit") {
          const mod = override.modified;
          if (mod) {
            dto.ruleType = (mod as Record<string, unknown>).ruleType as string ?? null;
            dto.scopeType = (mod as Record<string, unknown>).scopeType as string ?? null;
            dto.scopeId = (mod as Record<string, unknown>).scopeId as string ?? null;
            dto.payload = (mod as Record<string, unknown>).payload as Record<string, unknown> ?? null;

            // Determine severity: soft constraints have a "weight" field
            if ("weight" in (mod as Record<string, unknown>)) {
              dto.severity = "soft";
            } else {
              dto.severity = "hard";
            }
          }
        }

        constraintOverrideDtos.push(dto);
      });

      // Build settings override DTO (only include if any settings were modified)
      let settingsDto: SettingsOverrideDto | null = null;
      const so = store.settingsOverrides;
      if (
        so.minRestBetweenShiftsHours !== undefined ||
        so.eligibilityThresholdHours !== undefined ||
        so.leaveDurationHours !== undefined ||
        so.leaveCapacity !== undefined ||
        so.balanceValue !== undefined ||
        so.minPeopleAtBase !== undefined
      ) {
        settingsDto = {
          minRestBetweenShiftsHours: so.minRestBetweenShiftsHours ?? null,
          eligibilityThresholdHours: so.eligibilityThresholdHours ?? null,
          leaveDurationHours: so.leaveDurationHours ?? null,
          leaveCapacity: so.leaveCapacity ?? null,
          balanceValue: so.balanceValue ?? null,
          minPeopleAtBase: so.minPeopleAtBase ?? null,
        };
      }

      const request: PublishSandboxRequest = {
        versionId: store.draftVersionId,
        taskOverrides: taskOverrideDtos,
        constraintOverrides: constraintOverrideDtos,
        memberExclusions: Array.from(store.memberExclusions),
        settingsOverrides: settingsDto,
      };

      await publishSandbox(spaceId, store.groupId, request);

      // On success: exit sandbox and navigate to group schedule view
      const groupId = store.groupId;
      store.exitSandbox();
      router.push(`/groups/${groupId}`);
    } catch (err: unknown) {
      // On failure: display error message, preserve sandbox state
      const status = (err as { response?: { status?: number } })?.response?.status;
      if (status === 409) {
        setPublishError(t("publishConflict"));
      } else {
        setPublishError(t("publishError"));
      }
    } finally {
      setIsPublishing(false);
    }
  }, [t, router]);

  /**
   * Discard flow: call existing discard version endpoint, then exit sandbox
   * and navigate to the group schedule view.
   * Requirements: 10.1, 10.2, 10.3, 10.4
   */
  const handleDiscard = useCallback(async () => {
    const store = useSandboxStore.getState();
    const spaceId = useSpaceStore.getState().currentSpaceId;

    if (!spaceId || !store.draftVersionId || !store.groupId) return;

    setIsDiscarding(true);
    setDiscardError(null);

    try {
      await discardVersion(spaceId, store.draftVersionId);

      const groupId = store.groupId;
      store.exitSandbox();
      router.push(`/groups/${groupId}`);
    } catch {
      setDiscardError(t("discard.error"));
    } finally {
      setIsDiscarding(false);
      setShowDiscardConfirm(false);
    }
  }, [t, router]);

  const tabs: { id: TabId; label: string }[] = [
    { id: "tasks", label: t("tabs.tasks") },
    { id: "constraints", label: t("tabs.constraints") },
    { id: "members", label: t("tabs.members") },
    { id: "settings", label: t("tabs.settings") },
  ];

  return (
    <div className="flex flex-col h-full bg-white dark:bg-slate-900 border-l border-slate-200 dark:border-slate-700">
      {/* Navigation guard dialog — warns when navigating away with unsaved changes */}
      <SandboxNavigationGuardDialog
        open={showLeaveDialog}
        onConfirm={confirmLeave}
        onCancel={cancelLeave}
      />

      {/* Tab bar */}
      <div className="flex gap-1 p-3 border-b border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/50 overflow-x-auto">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            className={`px-3 py-1.5 rounded-lg text-xs font-medium transition-all whitespace-nowrap ${
              activeTab === tab.id
                ? "bg-white dark:bg-slate-700 text-slate-900 dark:text-white shadow-sm"
                : "text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-300"
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Tab content */}
      <div className="flex-1 overflow-y-auto p-4">
        {activeTab === "tasks" && (
          <SandboxTasksTab
            taskOverrides={taskOverrides}
            baseline={baseline}
          />
        )}
        {activeTab === "constraints" && (
          <SandboxConstraintsTab
            constraintOverrides={constraintOverrides}
            baseline={baseline}
          />
        )}
        {activeTab === "members" && (
          <SandboxMembersTab
            memberExclusions={memberExclusions}
            baseline={baseline}
          />
        )}
        {activeTab === "settings" && (
          <SandboxSettingsTab
            settingsOverrides={settingsOverrides}
            baseline={baseline}
          />
        )}
      </div>

      {/* Action buttons */}
      <div className="p-4 border-t border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/50 space-y-3">
        {/* Error messages */}
        {publishError && (
          <p className="text-xs text-red-600 dark:text-red-400 text-center">{publishError}</p>
        )}
        {discardError && (
          <p className="text-xs text-red-600 dark:text-red-400 text-center">{discardError}</p>
        )}

        {/* Discard confirmation dialog */}
        {showDiscardConfirm && (
          <div className="bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 rounded-xl p-3 space-y-2">
            <p className="text-xs text-red-700 dark:text-red-300 font-medium">
              {t("discard.confirmMessage")}
            </p>
            <div className="flex gap-2">
              <button
                onClick={handleDiscard}
                disabled={isDiscarding}
                className={`flex-1 py-2 px-3 rounded-lg text-xs font-semibold transition-all ${
                  isDiscarding
                    ? "bg-red-300 dark:bg-red-800 text-white cursor-not-allowed opacity-60"
                    : "bg-red-500 hover:bg-red-600 text-white"
                }`}
              >
                {isDiscarding ? t("discard.discarding") : t("discard.confirmButton")}
              </button>
              <button
                onClick={() => setShowDiscardConfirm(false)}
                disabled={isDiscarding}
                className="flex-1 py-2 px-3 rounded-lg text-xs font-medium border border-slate-200 dark:border-slate-600 text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-700 transition-all"
              >
                {t("discard.cancelButton")}
              </button>
            </div>
          </div>
        )}

        {/* Run Simulation button */}
        <button
          onClick={handleRunSimulation}
          disabled={isSimulating || isPublishing || isDiscarding}
          className={`w-full py-2.5 px-4 rounded-xl text-sm font-semibold transition-all ${
            isSimulating || isPublishing || isDiscarding
              ? "bg-sky-300 dark:bg-sky-800 text-white cursor-not-allowed opacity-60"
              : "bg-sky-500 hover:bg-sky-600 text-white shadow-sm hover:shadow"
          }`}
        >
          {isSimulating ? t("runningSimulation") : t("runSimulation")}
        </button>

        {/* Publish + Discard row */}
        {!showDiscardConfirm && (
          <div className="flex gap-2">
            <button
              onClick={handlePublish}
              disabled={isPublishing || isSimulating || isDiscarding}
              className={`flex-1 py-2.5 px-4 rounded-xl text-sm font-semibold transition-all ${
                isPublishing || isSimulating || isDiscarding
                  ? "bg-emerald-300 dark:bg-emerald-800 text-white cursor-not-allowed opacity-60"
                  : "bg-emerald-500 hover:bg-emerald-600 text-white shadow-sm hover:shadow"
              }`}
            >
              {isPublishing ? t("publishing") : t("publish")}
            </button>
            <button
              onClick={() => setShowDiscardConfirm(true)}
              disabled={isSimulating || isPublishing || isDiscarding}
              className="flex-1 py-2.5 px-4 rounded-xl text-sm font-medium border border-red-200 dark:border-red-800 text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-950/30 transition-all disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {t("discard.button")}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
