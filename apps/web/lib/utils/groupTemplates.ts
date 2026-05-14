import type { GroupTaskPayload } from "@/lib/api/tasks";

export interface GroupTemplate {
  id: string;
  name: string;
  nameHe: string;
  description: string;
  icon: string;
  color: string;
  tasks: GroupTaskPayload[];
  constraints: Array<{
    scopeType: string;
    severity: string;
    ruleType: string;
    rulePayloadJson: string;
  }>;
  solverHorizonDays: number;
  qualifications: Array<{ name: string; description?: string }>;
  unavailabilityReasons: string[];
}

function futureDate(daysFromNow: number): string {
  const d = new Date();
  d.setDate(d.getDate() + daysFromNow);
  return d.toISOString();
}

function endDate(): string {
  return futureDate(90);
}

function startDate(): string {
  return new Date().toISOString();
}

export const GROUP_TEMPLATES: GroupTemplate[] = [
  {
    id: "army-base",
    name: "Army / Military Base",
    nameHe: "בסיס צבאי",
    description: "24h guard shifts, kitchen duty, patrol. Designed for military platoons.",
    icon: "🎖️",
    color: "#16a34a",
    solverHorizonDays: 7,
    tasks: [
      {
        name: "Guard Duty",
        startsAt: startDate(),
        endsAt: endDate(),
        shiftDurationMinutes: 1440, // 24h
        requiredHeadcount: 2,
        burdenLevel: "hard",
        allowsDoubleShift: false,
        allowsOverlap: false,
      },
      {
        name: "Kitchen",
        startsAt: startDate(),
        endsAt: endDate(),
        shiftDurationMinutes: 480, // 8h
        requiredHeadcount: 2,
        burdenLevel: "hard",
        allowsDoubleShift: false,
        allowsOverlap: false,
        dailyStartTime: "06:00",
        dailyEndTime: "22:00",
      },
      {
        name: "Patrol",
        startsAt: startDate(),
        endsAt: endDate(),
        shiftDurationMinutes: 480, // 8h
        requiredHeadcount: 2,
        burdenLevel: "normal",
        allowsDoubleShift: false,
        allowsOverlap: false,
      },
    ],
    constraints: [
      { scopeType: "group", severity: "hard", ruleType: "min_rest_hours", rulePayloadJson: '{"hours": 8}' },
      { scopeType: "group", severity: "soft", ruleType: "no_consecutive_burden", rulePayloadJson: '{"burden_level": "hard"}' },
    ],
    qualifications: [
      { name: "Combat Medic" },
      { name: "Radio Operator" },
      { name: "Driver" },
      { name: "Commander" },
      { name: "Sharpshooter" },
    ],
    unavailabilityReasons: ["חופשה", "מחלה", "אישי", "לימודים"],
  },
  {
    id: "restaurant",
    name: "Restaurant / Cafe",
    nameHe: "מסעדה / בית קפה",
    description: "Morning, afternoon, and evening shifts. Waiters, kitchen, bar.",
    icon: "🍽️",
    color: "#ea580c",
    solverHorizonDays: 7,
    tasks: [
      {
        name: "Morning Shift",
        startsAt: startDate(),
        endsAt: endDate(),
        shiftDurationMinutes: 360, // 6h
        requiredHeadcount: 3,
        burdenLevel: "neutral",
        allowsDoubleShift: false,
        allowsOverlap: false,
        dailyStartTime: "07:00",
        dailyEndTime: "13:00",
      },
      {
        name: "Evening Shift",
        startsAt: startDate(),
        endsAt: endDate(),
        shiftDurationMinutes: 360, // 6h
        requiredHeadcount: 4,
        burdenLevel: "neutral",
        allowsDoubleShift: false,
        allowsOverlap: false,
        dailyStartTime: "16:00",
        dailyEndTime: "23:00",
      },
      {
        name: "Closing Shift",
        startsAt: startDate(),
        endsAt: endDate(),
        shiftDurationMinutes: 180, // 3h
        requiredHeadcount: 2,
        burdenLevel: "hard",
        allowsDoubleShift: false,
        allowsOverlap: false,
        dailyStartTime: "22:00",
        dailyEndTime: "01:00",
      },
    ],
    constraints: [
      { scopeType: "group", severity: "hard", ruleType: "min_rest_hours", rulePayloadJson: '{"hours": 10}' },
    ],
    qualifications: [
      { name: "Bartender" },
      { name: "Waiter" },
      { name: "Cook" },
      { name: "Shift Manager" },
      { name: "Barista" },
    ],
    unavailabilityReasons: ["חופשה", "מחלה", "אישי", "לימודים"],
  },
  {
    id: "hospital",
    name: "Hospital / Clinic",
    nameHe: "בית חולים / מרפאה",
    description: "Day, evening, and night shifts. 8-hour rotations with handoff overlap.",
    icon: "🏥",
    color: "#dc2626",
    solverHorizonDays: 14,
    tasks: [
      {
        name: "Day Shift",
        startsAt: startDate(),
        endsAt: endDate(),
        shiftDurationMinutes: 480, // 8h
        requiredHeadcount: 3,
        burdenLevel: "neutral",
        allowsDoubleShift: false,
        allowsOverlap: false,
        dailyStartTime: "07:00",
        dailyEndTime: "15:00",
      },
      {
        name: "Evening Shift",
        startsAt: startDate(),
        endsAt: endDate(),
        shiftDurationMinutes: 480, // 8h
        requiredHeadcount: 2,
        burdenLevel: "hard",
        allowsDoubleShift: false,
        allowsOverlap: false,
        dailyStartTime: "15:00",
        dailyEndTime: "23:00",
      },
      {
        name: "Night Shift",
        startsAt: startDate(),
        endsAt: endDate(),
        shiftDurationMinutes: 480, // 8h
        requiredHeadcount: 2,
        burdenLevel: "hard",
        allowsDoubleShift: false,
        allowsOverlap: false,
        dailyStartTime: "23:00",
        dailyEndTime: "07:00",
      },
    ],
    constraints: [
      { scopeType: "group", severity: "hard", ruleType: "min_rest_hours", rulePayloadJson: '{"hours": 12}' },
      { scopeType: "group", severity: "soft", ruleType: "no_consecutive_burden", rulePayloadJson: '{"burden_level": "hard"}' },
    ],
    qualifications: [
      { name: "Nurse" },
      { name: "Doctor" },
      { name: "Paramedic" },
      { name: "Lab Technician" },
      { name: "Receptionist" },
    ],
    unavailabilityReasons: ["חופשה", "מחלה", "אישי", "לימודים"],
  },
  {
    id: "security",
    name: "Security / Guard Service",
    nameHe: "שמירה / אבטחה",
    description: "12-hour day/night rotations with minimum rest between shifts.",
    icon: "🛡️",
    color: "#7c3aed",
    solverHorizonDays: 7,
    tasks: [
      {
        name: "Day Watch",
        startsAt: startDate(),
        endsAt: endDate(),
        shiftDurationMinutes: 720, // 12h
        requiredHeadcount: 2,
        burdenLevel: "neutral",
        allowsDoubleShift: false,
        allowsOverlap: false,
        dailyStartTime: "06:00",
        dailyEndTime: "18:00",
      },
      {
        name: "Night Watch",
        startsAt: startDate(),
        endsAt: endDate(),
        shiftDurationMinutes: 720, // 12h
        requiredHeadcount: 2,
        burdenLevel: "hard",
        allowsDoubleShift: false,
        allowsOverlap: false,
        dailyStartTime: "18:00",
        dailyEndTime: "06:00",
      },
    ],
    constraints: [
      { scopeType: "group", severity: "hard", ruleType: "min_rest_hours", rulePayloadJson: '{"hours": 10}' },
    ],
    qualifications: [
      { name: "Armed Guard" },
      { name: "CCTV Operator" },
      { name: "Patrol" },
      { name: "Shift Supervisor" },
      { name: "First Aid Certified" },
    ],
    unavailabilityReasons: ["חופשה", "מחלה", "אישי", "לימודים"],
  },
  {
    id: "custom",
    name: "Custom (Empty)",
    nameHe: "מותאם אישית",
    description: "Start from scratch. Add your own tasks and constraints.",
    icon: "✏️",
    color: "#64748b",
    solverHorizonDays: 7,
    tasks: [],
    constraints: [],
    qualifications: [],
    unavailabilityReasons: [],
  },
];
