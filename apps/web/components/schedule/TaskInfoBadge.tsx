"use client";

import { useState } from "react";
import type { TaskConfigSummaryDto } from "@/lib/api/groups";
import TaskInfoPopover from "./TaskInfoPopover";

interface TaskInfoBadgeProps {
  config: TaskConfigSummaryDto | null | undefined;
}

/**
 * A small "ℹ" icon button displayed next to task names in the schedule grid.
 * Opens a TaskInfoPopover on click showing the task's configuration.
 * Renders nothing if config data is unavailable.
 */
export default function TaskInfoBadge({ config }: TaskInfoBadgeProps) {
  const [open, setOpen] = useState(false);

  if (!config) {
    return null;
  }

  return (
    <span className="relative inline-flex items-center">
      <button
        type="button"
        aria-label="Task configuration info"
        onClick={() => setOpen((prev) => !prev)}
        className="inline-flex items-center justify-center w-4 h-4 text-[10px] leading-none text-slate-400 hover:text-slate-600 rounded-full transition-colors focus:outline-none focus:ring-1 focus:ring-slate-300"
      >
        ℹ
      </button>
      {open && <TaskInfoPopover config={config} onClose={() => setOpen(false)} />}
    </span>
  );
}
