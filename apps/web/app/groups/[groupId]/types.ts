import type { GroupMemberDto, GroupAlertDto, DeletedGroupDto, GroupWithMemberCountDto } from "@/lib/api/groups";
import type { GroupTaskDto } from "@/lib/api/tasks";
import type { ConstraintDto } from "@/lib/api/constraints";

export type ActiveTab = "schedule" | "members" | "qualifications" | "roles" | "alerts" | "messages" | "tasks" | "constraints" | "settings" | "stats" | "live-status";

export const ADMIN_ONLY_TABS: ActiveTab[] = ["tasks", "constraints", "roles", "settings", "stats"];

export interface ScheduleAssignment {
  id: string;
  personId: string;
  personName: string;
  taskTypeName: string;
  slotStartsAt: string;
  slotEndsAt: string;
  source: string;
}

export const burdenLabels: Record<string, string> = {
  hard: "קשה", normal: "רגיל", easy: "קל",
  Hard: "קשה", Normal: "רגיל", Easy: "קל",
};

export const burdenColors: Record<string, string> = {
  hard: "bg-red-50 text-red-700 border-red-200",
  normal: "bg-slate-100 text-slate-600 border-slate-200",
  easy: "bg-emerald-50 text-emerald-700 border-emerald-200",
  Hard: "bg-red-50 text-red-700 border-red-200",
  Normal: "bg-slate-100 text-slate-600 border-slate-200",
  Easy: "bg-emerald-50 text-emerald-700 border-emerald-200",
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
