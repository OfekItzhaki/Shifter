"use client";
import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import AppShell from "@/components/shell/AppShell";
import { apiClient } from "@/lib/api/client";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useAuthStore } from "@/lib/store/authStore";
import Link from "next/link";
import ScheduleTable from "@/components/schedule/ScheduleTable";
import { getTaskTypes, createTaskType, getTaskSlots, createTaskSlot, TaskTypeDto, TaskSlotDto } from "@/lib/api/tasks";
import { getConstraints, createConstraint, ConstraintDto } from "@/lib/api/constraints";
import { clsx } from "clsx";

interface GroupDto { id: string; name: string; memberCount: number; solverHorizonDays: number; }
interface MemberDto { personId: string; fullName: string; displayName: string | null; }
type Tab = "schedule" | "members" | "tasks" | "constraints" | "settings";

