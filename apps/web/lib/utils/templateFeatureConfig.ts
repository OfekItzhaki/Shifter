/**
 * Feature visibility map — determines which UI features are shown
 * based on the group's template type. This is a frontend-only config;
 * no API call is needed to resolve visibility.
 */

export type GroupTemplateType = "Army" | "Restaurant" | "Hospital" | "Security" | "Custom";

export interface FeatureVisibility {
  closedBase: boolean;
  homeLeave: boolean;
  minRestBetweenShifts: boolean;
  minPeopleAtBase: boolean;
  qualifications: boolean;
  taskRotation: boolean;
  maxTaskTypePerPeriod: boolean;
  stayoverLabel: { en: string; he: string; ru: string };
}

export const FEATURE_VISIBILITY_MAP: Record<GroupTemplateType, FeatureVisibility> = {
  Army: {
    closedBase: true,
    homeLeave: true,
    minRestBetweenShifts: true,
    minPeopleAtBase: true,
    qualifications: true,
    taskRotation: true,
    maxTaskTypePerPeriod: true,
    stayoverLabel: { en: "Closed Base", he: "בסיס סגור", ru: "Закрытая база" },
  },
  Restaurant: {
    closedBase: false,
    homeLeave: false,
    minRestBetweenShifts: true,
    minPeopleAtBase: false,
    qualifications: true,
    taskRotation: false,
    maxTaskTypePerPeriod: true,
    stayoverLabel: { en: "Stayover", he: "לינה במקום", ru: "Ночёвка" },
  },
  Hospital: {
    closedBase: true,
    homeLeave: false,
    minRestBetweenShifts: true,
    minPeopleAtBase: true,
    qualifications: true,
    taskRotation: false,
    maxTaskTypePerPeriod: true,
    stayoverLabel: { en: "Stayover", he: "לינה במקום", ru: "Ночёвка" },
  },
  Security: {
    closedBase: true,
    homeLeave: false,
    minRestBetweenShifts: true,
    minPeopleAtBase: true,
    qualifications: true,
    taskRotation: false,
    maxTaskTypePerPeriod: true,
    stayoverLabel: { en: "Closed Base", he: "בסיס סגור", ru: "Закрытая база" },
  },
  Custom: {
    closedBase: true,
    homeLeave: true,
    minRestBetweenShifts: true,
    minPeopleAtBase: true,
    qualifications: true,
    taskRotation: true,
    maxTaskTypePerPeriod: true,
    stayoverLabel: { en: "Stayover", he: "לינה במקום", ru: "Ночёвка" },
  },
};
