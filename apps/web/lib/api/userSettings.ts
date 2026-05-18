import { apiClient } from "./client";

export interface UpdateLocationResponse {
  ianaTimezoneId: string;
  offsetMinutes: number;
}

export interface UserSettingsDto {
  countryCode: string | null;
  stateCode: string | null;
  timezoneId: string;
  timezoneOffsetMinutes: number;
  timeFormat: string;
}

export async function updateUserLocation(
  countryCode: string,
  stateCode: string | null
): Promise<UpdateLocationResponse> {
  const { data } = await apiClient.put<UpdateLocationResponse>(
    "/api/user-settings/location",
    { countryCode, stateCode }
  );
  return data;
}

export async function getUserSettings(): Promise<UserSettingsDto> {
  const { data } = await apiClient.get<UserSettingsDto>("/api/user-settings");
  return data;
}
