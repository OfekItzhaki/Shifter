"use client";

import { useEffect, useRef } from "react";
import { useTranslations } from "next-intl";
import type { TaskConfigSummaryDto } from "@/lib/api/groups";

interface TaskInfoPopoverProps {
  config: TaskConfigSummaryDto;
  onClose: () => void;
}

/** Returns true when all task config values are at their defaults. */
function isDefaultConfig(config: TaskConfigSummaryDto): boolean {
  return (
    !config.allowsDoubleShift &&
    !config.allowsOverlap &&
    config.dailyStartTime === null &&
    config.dailyEndTime === null &&
    config.burdenLevel === "Normal" &&
    config.requiredQualificationNames.length === 0 &&
    config.splitCount <= 1
  );
}

export default function TaskInfoPopover({ config, onClose }: TaskInfoPopoverProps) {
  const t = useTranslations("schedule");
  const popoverRef = useRef<HTMLDivElement>(null);

  // Close on click-outside or blur
  useEffect(() => {
    function handleMouseDown(e: MouseEvent) {
      if (popoverRef.current && !popoverRef.current.contains(e.target as Node)) {
        onClose();
      }
    }
    function handleFocusOut(e: FocusEvent) {
      if (
        popoverRef.current &&
        e.relatedTarget instanceof Node &&
        !popoverRef.current.contains(e.relatedTarget)
      ) {
        onClose();
      }
    }
    document.addEventListener("mousedown", handleMouseDown);
    popoverRef.current?.addEventListener("focusout", handleFocusOut);
    const ref = popoverRef.current;
    return () => {
      document.removeEventListener("mousedown", handleMouseDown);
      ref?.removeEventListener("focusout", handleFocusOut);
    };
  }, [onClose]);

  if (isDefaultConfig(config)) {
    return (
      <div
        ref={popoverRef}
        role="tooltip"
        className="absolute top-full mt-2 left-1/2 -translate-x-1/2 z-50 bg-white border border-slate-200 rounded-lg shadow-lg px-4 py-3 text-xs text-slate-500 whitespace-nowrap"
      >
        {t("taskInfo.defaultSettings")}
      </div>
    );
  }

  const timeWindow =
    config.dailyStartTime && config.dailyEndTime
      ? `${config.dailyStartTime} – ${config.dailyEndTime}`
      : t("taskInfo.allDay");

  return (
    <div
      ref={popoverRef}
      role="tooltip"
      className="absolute top-full mt-2 left-1/2 -translate-x-1/2 z-50 bg-white border border-slate-200 rounded-lg shadow-lg px-4 py-3 min-w-[200px] text-xs"
    >
      <dl className="space-y-1.5">
        <div className="flex justify-between gap-4">
          <dt className="text-slate-500">{t("taskInfo.doubleShift")}</dt>
          <dd className="font-medium text-slate-700">
            {config.allowsDoubleShift ? "✓" : "✗"}
          </dd>
        </div>

        <div className="flex justify-between gap-4">
          <dt className="text-slate-500">{t("taskInfo.overlap")}</dt>
          <dd className="font-medium text-slate-700">
            {config.allowsOverlap ? "✓" : "✗"}
          </dd>
        </div>

        <div className="flex justify-between gap-4">
          <dt className="text-slate-500">{t("taskInfo.timeWindow")}</dt>
          <dd className="font-medium text-slate-700">{timeWindow}</dd>
        </div>

        <div className="flex justify-between gap-4">
          <dt className="text-slate-500">{t("taskInfo.burden")}</dt>
          <dd className="font-medium text-slate-700">{config.burdenLevel}</dd>
        </div>

        {config.requiredQualificationNames.length > 0 && (
          <div className="flex justify-between gap-4">
            <dt className="text-slate-500">{t("taskInfo.qualifications")}</dt>
            <dd className="font-medium text-slate-700">
              {config.requiredQualificationNames.join(", ")}
            </dd>
          </div>
        )}

        {config.splitCount > 1 && (
          <div className="flex justify-between gap-4">
            <dt className="text-slate-500">{t("taskInfo.splitCount")}</dt>
            <dd className="font-medium text-slate-700">{config.splitCount}</dd>
          </div>
        )}
      </dl>
    </div>
  );
}
