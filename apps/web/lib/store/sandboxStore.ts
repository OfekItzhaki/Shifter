import { create } from "zustand";

// ── Solver Types (mirrors backend SolverInputDto / SolverOutputDto) ───────────

export interface StabilityWeightsDto {
  today_tomorrow: number;
  days_3_4: number;
  days_5_7: number;
}

export interface PersonEligibilityDto {
  personId: string;
  roleIds: string[];
  qualificationIds: string[];
  groupIds: string[];
  home_leave_priority?: number;
}

export interface AvailabilityWindowDto {
  personId: string;
  startsAt: string;
  endsAt: string;
}

export interface PresenceWindowDto {
  personId: string;
  state: string;
  startsAt: string;
  endsAt: string;
}

export interface QualificationRequirementSolverDto {
  qualification_name: string;
  count: number;
  mandatory: boolean;
}

export interface TaskSlotDto {
  slotId: string;
  taskTypeId: string;
  taskTypeName: string;
  burdenLevel: string;
  startsAt: string;
  endsAt: string;
  requiredHeadcount: number;
  priority: number;
  requiredRoleIds: string[];
  requiredQualificationIds: string[];
  allowsOverlap: boolean;
  allowsDoubleShift?: boolean;
  qualificationRequirements?: QualificationRequirementSolverDto[];
}

export interface HardConstraintDto {
  constraintId: string;
  ruleType: string;
  scopeType: string;
  scopeId: string | null;
  payload: Record<string, unknown>;
}

export interface SoftConstraintDto {
  constraintId: string;
  ruleType: string;
  scopeType: string;
  scopeId: string | null;
  weight: number;
  payload: Record<string, unknown>;
}

export interface BaselineAssignmentDto {
  slotId: string;
  personId: string;
}

export interface FairnessCountersDto {
  personId: string;
  totalAssignments7d: number;
  hardTasks7d: number;
  nightMissions7d: number;
  consecutiveHardCount: number;
  taskTypeCounts7d?: Record<string, number>;
}

export interface HomeLeaveConfigDto {
  enabled: boolean;
  min_rest_hours: number;
  eligibility_threshold_hours: number;
  leave_capacity: number;
  leave_duration_hours: number;
  balance_value?: number;
}

export interface TaskRotationDto {
  person_id: string;
  completed_task_type_ids: string[];
}

export interface CumulativeTrackingDto {
  personId: string;
  [key: string]: unknown;
}

export interface SpecialDayDto {
  date: string;
  name: string;
  kind: string;
  home_leave_weight_multiplier: number;
  requires_coverage: boolean;
}

export interface SolverInputDto {
  spaceId: string;
  runId: string;
  triggerMode: string;
  horizonStart: string;
  horizonEnd: string;
  locale: string;
  stabilityWeights: StabilityWeightsDto;
  people: PersonEligibilityDto[];
  availabilityWindows: AvailabilityWindowDto[];
  presenceWindows: PresenceWindowDto[];
  taskSlots: TaskSlotDto[];
  hardConstraints: HardConstraintDto[];
  softConstraints: SoftConstraintDto[];
  emergencyConstraints: HardConstraintDto[];
  baselineAssignments: BaselineAssignmentDto[];
  fairnessCounters: FairnessCountersDto[];
  lockedSlotIds?: string[];
  homeLeaveConfig?: HomeLeaveConfigDto | null;
  taskRotation?: TaskRotationDto[];
  preview_mode?: boolean;
  cumulative_tracking?: CumulativeTrackingDto[];
  special_days?: SpecialDayDto[];
}

// ── Solver Output Types ───────────────────────────────────────────────────────

export interface AssignmentResultDto {
  slot_id: string;
  person_id: string;
  source: string;
}

export interface HardConflictDto {
  constraint_id: string;
  rule_type: string;
  description: string;
  affected_slot_ids: string[];
  affected_person_ids: string[];
}

export interface StabilityMetricsDto {
  today_tomorrow_changes: number;
  days_3_4_changes: number;
  days_5_7_changes: number;
  total_stability_penalty: number;
}

export interface FairnessMetricsDto {
  person_id: string;
  hated_tasks_assigned: number;
  disliked_tasks_assigned: number;
  total_assigned: number;
}

export interface HomeLeaveAssignmentDto {
  person_id: string;
  starts_at: string;
  ends_at: string;
}

export interface HomeLeaveMetricDto {
  person_id: string;
  total_base_hours: number;
  total_home_hours: number;
  base_time_ratio: number;
  leave_slot_count: number;
}

export interface SolverOutputDto {
  run_id: string;
  feasible: boolean;
  timed_out: boolean;
  assignments: AssignmentResultDto[];
  uncovered_slot_ids: string[];
  hard_conflicts: HardConflictDto[];
  soft_penalty_total: number;
  stability_metrics: StabilityMetricsDto;
  fairness_metrics: FairnessMetricsDto[];
  explanation_fragments: string[];
  home_leave_assignments: HomeLeaveAssignmentDto[];
  home_leave_metrics: HomeLeaveMetricDto[];
  fairness_variance?: number;
  solver_time_ms: number;
}

// ── Sandbox Override Types ─────────────────────────────────────────────────────

export interface TaskOverride {
  action: "add" | "edit" | "remove";
  original?: TaskSlotDto;
  modified?: Partial<TaskSlotDto>;
}

export interface ConstraintOverride {
  action: "add" | "edit" | "remove";
  original?: HardConstraintDto | SoftConstraintDto;
  modified?: Partial<HardConstraintDto | SoftConstraintDto>;
}

export interface SettingsOverrides {
  minRestBetweenShiftsHours?: number;
  eligibilityThresholdHours?: number;
  leaveDurationHours?: number;
  leaveCapacity?: number;
  balanceValue?: number;
  minPeopleAtBase?: number;
}

export type TaskOverrideMap = Map<string, TaskOverride>;
export type ConstraintOverrideMap = Map<string, ConstraintOverride>;

// ── Sandbox State Interface ───────────────────────────────────────────────────

interface SandboxState {
  // Session state
  isActive: boolean;
  groupId: string | null;
  draftVersionId: string | null;

  // Baseline (original solver input fetched from backend)
  baseline: SolverInputDto | null;

  // Overrides (user modifications)
  taskOverrides: TaskOverrideMap;
  constraintOverrides: ConstraintOverrideMap;
  memberExclusions: Set<string>;
  settingsOverrides: SettingsOverrides;

  // Simulation results
  lastSimulationResult: SolverOutputDto | null;
  isSimulating: boolean;
  simulationError: string | null;

  // Actions
  enterSandbox: (groupId: string, draftVersionId: string, baseline: SolverInputDto) => void;
  exitSandbox: () => void;

  addTask: (task: TaskSlotDto) => void;
  editTask: (slotId: string, changes: Partial<TaskSlotDto>) => void;
  removeTask: (slotId: string) => void;
  restoreTask: (slotId: string) => void;

  addConstraint: (constraint: HardConstraintDto | SoftConstraintDto) => void;
  editConstraint: (constraintId: string, changes: Partial<HardConstraintDto | SoftConstraintDto>) => void;
  removeConstraint: (constraintId: string) => void;

  toggleMember: (personId: string) => void;

  updateSettings: (settings: Partial<SettingsOverrides>) => void;

  setSimulationResult: (result: SolverOutputDto | null) => void;
  setSimulating: (isSimulating: boolean) => void;
  setSimulationError: (error: string | null) => void;
}

// ── Initial State ─────────────────────────────────────────────────────────────

const initialState = {
  isActive: false,
  groupId: null,
  draftVersionId: null,
  baseline: null,
  taskOverrides: new Map<string, TaskOverride>(),
  constraintOverrides: new Map<string, ConstraintOverride>(),
  memberExclusions: new Set<string>(),
  settingsOverrides: {},
  lastSimulationResult: null,
  isSimulating: false,
  simulationError: null,
};

// ── Store Implementation ──────────────────────────────────────────────────────
// NOTE: No localStorage persistence — state is ephemeral by design (Req 7.1, 7.4)

export const useSandboxStore = create<SandboxState>()((set, get) => ({
  ...initialState,

  enterSandbox: (groupId: string, draftVersionId: string, baseline: SolverInputDto) => {
    set({
      isActive: true,
      groupId,
      draftVersionId,
      baseline,
      taskOverrides: new Map(),
      constraintOverrides: new Map(),
      memberExclusions: new Set(),
      settingsOverrides: {},
      lastSimulationResult: null,
      isSimulating: false,
      simulationError: null,
    });
  },

  exitSandbox: () => {
    set({ ...initialState });
  },

  addTask: (task: TaskSlotDto) => {
    const overrides = new Map(get().taskOverrides);
    overrides.set(task.slotId, { action: "add", modified: task });
    set({ taskOverrides: overrides });
  },

  editTask: (slotId: string, changes: Partial<TaskSlotDto>) => {
    const overrides = new Map(get().taskOverrides);
    const existing = overrides.get(slotId);

    if (existing?.action === "add") {
      // Editing a task that was added in this session — merge into the add override
      overrides.set(slotId, {
        action: "add",
        modified: { ...existing.modified, ...changes },
      });
    } else {
      // Editing a baseline task — find original from baseline
      const baseline = get().baseline;
      const original = baseline?.taskSlots.find((t) => t.slotId === slotId);
      overrides.set(slotId, {
        action: "edit",
        original: original ?? undefined,
        modified: existing?.modified
          ? { ...existing.modified, ...changes }
          : changes,
      });
    }

    set({ taskOverrides: overrides });
  },

  removeTask: (slotId: string) => {
    const overrides = new Map(get().taskOverrides);
    const existing = overrides.get(slotId);

    if (existing?.action === "add") {
      // Removing a task that was added in this session — just delete the override
      overrides.delete(slotId);
    } else {
      // Removing a baseline task — mark as removed
      const baseline = get().baseline;
      const original = baseline?.taskSlots.find((t) => t.slotId === slotId);
      overrides.set(slotId, { action: "remove", original: original ?? undefined });
    }

    set({ taskOverrides: overrides });
  },

  restoreTask: (slotId: string) => {
    const overrides = new Map(get().taskOverrides);
    overrides.delete(slotId);
    set({ taskOverrides: overrides });
  },

  addConstraint: (constraint: HardConstraintDto | SoftConstraintDto) => {
    const overrides = new Map(get().constraintOverrides);
    overrides.set(constraint.constraintId, { action: "add", modified: constraint });
    set({ constraintOverrides: overrides });
  },

  editConstraint: (constraintId: string, changes: Partial<HardConstraintDto | SoftConstraintDto>) => {
    const overrides = new Map(get().constraintOverrides);
    const existing = overrides.get(constraintId);

    if (existing?.action === "add") {
      // Editing a constraint that was added in this session — merge into the add override
      overrides.set(constraintId, {
        action: "add",
        modified: { ...existing.modified, ...changes },
      });
    } else {
      // Editing a baseline constraint — find original from baseline
      const baseline = get().baseline;
      const original =
        baseline?.hardConstraints.find((c) => c.constraintId === constraintId) ??
        baseline?.softConstraints.find((c) => c.constraintId === constraintId);
      overrides.set(constraintId, {
        action: "edit",
        original: original ?? undefined,
        modified: existing?.modified
          ? { ...existing.modified, ...changes }
          : changes,
      });
    }

    set({ constraintOverrides: overrides });
  },

  removeConstraint: (constraintId: string) => {
    const overrides = new Map(get().constraintOverrides);
    const existing = overrides.get(constraintId);

    if (existing?.action === "add") {
      // Removing a constraint that was added in this session — just delete the override
      overrides.delete(constraintId);
    } else {
      // Removing a baseline constraint — mark as removed
      const baseline = get().baseline;
      const original =
        baseline?.hardConstraints.find((c) => c.constraintId === constraintId) ??
        baseline?.softConstraints.find((c) => c.constraintId === constraintId);
      overrides.set(constraintId, { action: "remove", original: original ?? undefined });
    }

    set({ constraintOverrides: overrides });
  },

  toggleMember: (personId: string) => {
    const exclusions = new Set(get().memberExclusions);
    if (exclusions.has(personId)) {
      exclusions.delete(personId);
    } else {
      exclusions.add(personId);
    }
    set({ memberExclusions: exclusions });
  },

  updateSettings: (settings: Partial<SettingsOverrides>) => {
    set({ settingsOverrides: { ...get().settingsOverrides, ...settings } });
  },

  setSimulationResult: (result: SolverOutputDto | null) => {
    set({ lastSimulationResult: result, simulationError: null });
  },

  setSimulating: (isSimulating: boolean) => {
    set({ isSimulating });
  },

  setSimulationError: (error: string | null) => {
    set({ simulationError: error, isSimulating: false });
  },
}));
