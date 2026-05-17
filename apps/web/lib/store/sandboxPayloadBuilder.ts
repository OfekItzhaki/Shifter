import type {
  SolverInputDto,
  TaskSlotDto,
  HardConstraintDto,
  SoftConstraintDto,
  TaskOverrideMap,
  ConstraintOverrideMap,
  SettingsOverrides,
  HomeLeaveConfigDto,
} from "./sandboxStore";

/**
 * Builds a complete SolverInputDto by merging the baseline with all sandbox overrides.
 * This is a PURE function — no side effects, no mutation of the baseline object.
 *
 * @param baseline - The original SolverInputDto fetched from the backend
 * @param taskOverrides - Map of slotId → TaskOverride (add/edit/remove)
 * @param constraintOverrides - Map of constraintId → ConstraintOverride (add/edit/remove)
 * @param memberExclusions - Set of personIds to exclude from the People list
 * @param settingsOverrides - Settings modifications (rest hours, home-leave params)
 * @returns A new SolverInputDto with all overrides applied
 */
export function buildOverridePayload(
  baseline: SolverInputDto,
  taskOverrides: TaskOverrideMap,
  constraintOverrides: ConstraintOverrideMap,
  memberExclusions: Set<string>,
  settingsOverrides: SettingsOverrides
): SolverInputDto {
  // 1. Apply task overrides
  const taskSlots = applyTaskOverrides(baseline.taskSlots, taskOverrides);

  // 2. Apply constraint overrides (both hard and soft)
  let hardConstraints = applyHardConstraintOverrides(baseline.hardConstraints, constraintOverrides);
  const softConstraints = applySoftConstraintOverrides(baseline.softConstraints, constraintOverrides);

  // 3. Apply member exclusions
  const people = applyMemberExclusions(baseline.people, memberExclusions);

  // 4. Apply settings overrides (on top of already-processed constraints)
  hardConstraints = applyMinRestSetting(hardConstraints, settingsOverrides);
  const homeLeaveConfig = applyHomeLeaveSettings(baseline.homeLeaveConfig, settingsOverrides);

  return {
    ...baseline,
    taskSlots,
    hardConstraints,
    softConstraints,
    people,
    homeLeaveConfig,
  };
}

// ── Task Overrides ────────────────────────────────────────────────────────────

function applyTaskOverrides(
  baselineSlots: TaskSlotDto[],
  overrides: TaskOverrideMap
): TaskSlotDto[] {
  if (overrides.size === 0) return baselineSlots.map((s) => ({ ...s }));

  const removeIds = new Set<string>();
  const editMap = new Map<string, Partial<TaskSlotDto>>();
  const additions: TaskSlotDto[] = [];

  for (const [slotId, override] of overrides) {
    switch (override.action) {
      case "remove":
        removeIds.add(slotId);
        break;
      case "edit":
        if (override.modified) {
          editMap.set(slotId, override.modified);
        }
        break;
      case "add":
        if (override.modified) {
          additions.push(override.modified as TaskSlotDto);
        }
        break;
    }
  }

  // Filter out removed slots, apply edits to remaining
  const result = baselineSlots
    .filter((slot) => !removeIds.has(slot.slotId))
    .map((slot) => {
      const edits = editMap.get(slot.slotId);
      if (edits) {
        return { ...slot, ...edits };
      }
      return { ...slot };
    });

  // Append additions
  return [...result, ...additions];
}

// ── Constraint Overrides (Hard) ───────────────────────────────────────────────

function applyHardConstraintOverrides(
  baselineConstraints: HardConstraintDto[],
  overrides: ConstraintOverrideMap
): HardConstraintDto[] {
  if (overrides.size === 0) return baselineConstraints.map((c) => ({ ...c }));

  const removeIds = new Set<string>();
  const editMap = new Map<string, Partial<HardConstraintDto>>();
  const additions: HardConstraintDto[] = [];

  for (const [constraintId, override] of overrides) {
    switch (override.action) {
      case "remove":
        removeIds.add(constraintId);
        break;
      case "edit":
        if (override.modified) {
          editMap.set(constraintId, override.modified as Partial<HardConstraintDto>);
        }
        break;
      case "add":
        // Only add if the modified constraint looks like a hard constraint (no weight field)
        if (override.modified && !("weight" in override.modified)) {
          additions.push(override.modified as HardConstraintDto);
        }
        break;
    }
  }

  const result = baselineConstraints
    .filter((c) => !removeIds.has(c.constraintId))
    .map((c) => {
      const edits = editMap.get(c.constraintId);
      if (edits) {
        return { ...c, ...edits };
      }
      return { ...c };
    });

  return [...result, ...additions];
}

// ── Constraint Overrides (Soft) ───────────────────────────────────────────────

function applySoftConstraintOverrides(
  baselineConstraints: SoftConstraintDto[],
  overrides: ConstraintOverrideMap
): SoftConstraintDto[] {
  if (overrides.size === 0) return baselineConstraints.map((c) => ({ ...c }));

  const removeIds = new Set<string>();
  const editMap = new Map<string, Partial<SoftConstraintDto>>();
  const additions: SoftConstraintDto[] = [];

  for (const [constraintId, override] of overrides) {
    switch (override.action) {
      case "remove":
        removeIds.add(constraintId);
        break;
      case "edit":
        if (override.modified) {
          editMap.set(constraintId, override.modified as Partial<SoftConstraintDto>);
        }
        break;
      case "add":
        // Only add if the modified constraint has a weight field (soft constraint)
        if (override.modified && "weight" in override.modified) {
          additions.push(override.modified as SoftConstraintDto);
        }
        break;
    }
  }

  const result = baselineConstraints
    .filter((c) => !removeIds.has(c.constraintId))
    .map((c) => {
      const edits = editMap.get(c.constraintId);
      if (edits) {
        return { ...c, ...edits };
      }
      return { ...c };
    });

  return [...result, ...additions];
}

// ── Member Exclusions ─────────────────────────────────────────────────────────

function applyMemberExclusions(
  baselinePeople: SolverInputDto["people"],
  exclusions: Set<string>
): SolverInputDto["people"] {
  if (exclusions.size === 0) return baselinePeople.map((p) => ({ ...p }));
  return baselinePeople
    .filter((person) => !exclusions.has(person.personId))
    .map((p) => ({ ...p }));
}

// ── Settings: Min Rest Between Shifts ─────────────────────────────────────────

function applyMinRestSetting(
  hardConstraints: HardConstraintDto[],
  settings: SettingsOverrides
): HardConstraintDto[] {
  if (settings.minRestBetweenShiftsHours === undefined) return hardConstraints;

  const minHours = settings.minRestBetweenShiftsHours;
  const existingIndex = hardConstraints.findIndex(
    (c) => c.ruleType === "min_rest_between_assignments"
  );

  if (existingIndex >= 0) {
    // Update existing constraint's payload
    return hardConstraints.map((c, i) => {
      if (i === existingIndex) {
        return {
          ...c,
          payload: { ...c.payload, min_hours: minHours },
        };
      }
      return c;
    });
  }

  // Create new constraint and append
  const newConstraint: HardConstraintDto = {
    constraintId: "sandbox-min-rest",
    ruleType: "min_rest_between_assignments",
    scopeType: "global",
    scopeId: null,
    payload: { min_hours: minHours },
  };

  return [...hardConstraints, newConstraint];
}

// ── Settings: Home Leave Config ───────────────────────────────────────────────

function applyHomeLeaveSettings(
  baselineConfig: HomeLeaveConfigDto | null | undefined,
  settings: SettingsOverrides
): HomeLeaveConfigDto | null | undefined {
  const hasOverrides =
    settings.eligibilityThresholdHours !== undefined ||
    settings.leaveDurationHours !== undefined ||
    settings.leaveCapacity !== undefined ||
    settings.balanceValue !== undefined;

  if (!hasOverrides) {
    // Return a shallow copy if config exists, otherwise pass through
    return baselineConfig ? { ...baselineConfig } : baselineConfig;
  }

  // Start from existing config or create a default enabled config
  const base: HomeLeaveConfigDto = baselineConfig
    ? { ...baselineConfig }
    : {
        enabled: true,
        min_rest_hours: 0,
        eligibility_threshold_hours: 0,
        leave_capacity: 0,
        leave_duration_hours: 0,
      };

  if (settings.eligibilityThresholdHours !== undefined) {
    base.eligibility_threshold_hours = settings.eligibilityThresholdHours;
  }
  if (settings.leaveDurationHours !== undefined) {
    base.leave_duration_hours = settings.leaveDurationHours;
  }
  if (settings.leaveCapacity !== undefined) {
    base.leave_capacity = settings.leaveCapacity;
  }
  if (settings.balanceValue !== undefined) {
    base.balance_value = settings.balanceValue;
  }

  return base;
}
