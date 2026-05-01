import type { GroupMemberDto, GroupAlertDto, DeletedGroupDto, GroupWithMemberCountDto } from "@/lib/api/groups";
import type { GroupTaskDto } from "@/lib/api/tasks";
import type { ConstraintDto } from "@/lib/api/constraints";

export type ActiveTab = "schedule" | "members" | "alerts" | "messages" | "tasks" | "constraints" | "settings" | "stats" | "live-status";

export const ADMIN_ONLY_TABS: ActiveTab[] = ["tasks", "constraints", "settings", "stats"];

export interface ScheduleAssignment {
  personName: string;
  taskTypeName: string;
  slotStartsAt: string;
  slotEndsAt: string;
}

export const burdenLabels: Record<string, string> = {
  Favorable: "נוח", Neutral: "ניטרלי", Disliked: "לא אהוב", Hated: "שנוא",
  favorable: "נוח", neutral: "ניטרלי", disliked: "לא אהוב", hated: "שנוא",
};

export const burdenColors: Record<string, string> = {
  Favorable: "bg-emerald-50 text-emerald-700 border-emerald-200",
  Neutral: "bg-slate-100 text-slate-600 border-slate-200",
  Disliked: "bg-amber-50 text-amber-700 border-amber-200",
  Hated: "bg-red-50 text-red-700 border-red-200",
  favorable: "bg-emerald-50 text-emerald-700 border-emerald-200",
  neutral: "bg-slate-100 text-slate-600 border-slate-200",
  disliked: "bg-amber-50 text-amber-700 border-amber-200",
  hated: "bg-red-50 text-red-700 border-red-200",
};

export const SEVERITY_STYLES: Record<string, string> = {
  hard: "bg-red-50 text-red-700 border-red-200",
  soft: "bg-blue-50 text-blue-700 border-blue-200",
  emergency: "bg-orange-50 text-orange-700 border-orange-300",
};

export const SEVERITY_DOTS: Record<string, string> = {
  hard: "bg-red-500",
  soft: "bg-blue-500",
  emergency: "bg-orange-500",
};

// Re-export DTO types used across tabs
export type { GroupMemberDto, GroupAlertDto, DeletedGroupDto, GroupWithMemberCountDto, GroupTaskDto, ConstraintDto };
