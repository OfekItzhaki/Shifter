"use client";

import { useState, useCallback } from "react";
import { getGroups, getGroupMembers } from "@/lib/api/groups";
import { listGroupTasks } from "@/lib/api/tasks";
import { getConstraints } from "@/lib/api/constraints";
import { getScheduleVersions } from "@/lib/api/schedule";
import { computeStepCompletion } from "@/lib/onboarding/decisions";
import { useSpaceStore } from "@/lib/store/spaceStore";
import type { StepCompletionMap } from "@/lib/onboarding/storage";
import { EMPTY_STEPS } from "@/lib/onboarding/storage";

export function useStepCompletion(): {
  steps: StepCompletionMap;
  isLoading: boolean;
  refresh: () => void;
} {
  const [steps, setSteps] = useState<StepCompletionMap>(EMPTY_STEPS);
  const [isLoading, setIsLoading] = useState(false);
  const currentSpaceId = useSpaceStore((s) => s.currentSpaceId);

  const refresh = useCallback(async () => {
    if (!currentSpaceId) return;

    setIsLoading(true);
    try {
      let groupCount = 0;
      let memberCount = 0;
      let taskCount = 0;
      let constraintCount = 0;
      let solverRunCount = 0;

      // Fetch groups first — other queries depend on having a groupId
      let groups: Awaited<ReturnType<typeof getGroups>> = [];
      try {
        groups = await getGroups(currentSpaceId);
        groupCount = groups.length;
      } catch {
        // leave groupCount as 0
      }

      const firstGroupId = groups.length > 0 ? groups[0].id : null;

      // Fetch group-scoped data only if a group exists
      if (firstGroupId) {
        const [membersResult, tasksResult] = await Promise.allSettled([
          getGroupMembers(currentSpaceId, firstGroupId),
          listGroupTasks(currentSpaceId, firstGroupId),
        ]);

        if (membersResult.status === "fulfilled") {
          memberCount = membersResult.value.length;
        }
        if (tasksResult.status === "fulfilled") {
          taskCount = tasksResult.value.length;
        }
      }

      // Fetch space-scoped data in parallel
      const [constraintsResult, versionsResult] = await Promise.allSettled([
        getConstraints(currentSpaceId),
        getScheduleVersions(currentSpaceId),
      ]);

      if (constraintsResult.status === "fulfilled") {
        constraintCount = constraintsResult.value.length;
      }
      if (versionsResult.status === "fulfilled") {
        solverRunCount = versionsResult.value.length;
      }

      const computed = computeStepCompletion({
        groupCount,
        memberCount,
        taskCount,
        constraintCount,
        solverRunCount,
      });

      setSteps(computed);
    } finally {
      setIsLoading(false);
    }
  }, [currentSpaceId]);

  return { steps, isLoading, refresh };
}
