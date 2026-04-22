import { apiClient } from "./client";

export interface AvailabilityWindowDto {
  id: string;
  personId: string;
  startsAt: string;
  endsAt: string;
  note: string | null;
}

export interface PresenceWindowDto {
  id: string;
  personId: string;
  state: string;
  startsAt: string;
  endsAt: string;
  note: string | null;
  isDerived: boolean;
}

export async function getAvailabilityWindows(
  spaceId: string, personId: string
): Promise<AvailabilityWindowDto[]> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/people/${personId}/availability`
  );
  return data;
}

export async function getPresenceWindows(
  spaceId: string, personId: string
): Promise<PresenceWindowDto[]> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/people/${personId}/presence`
  );
  return data;
}

export async function addAvailabilityWindow(
  spaceId: string, personId: string,
  startsAt: string, endsAt: string, note: string | null
): Promise<void> {
  await apiClient.post(`/spaces/${spaceId}/people/${personId}/availability`, {
    startsAt, endsAt, note,
  });
}

export async function addPresenceWindow(
  spaceId: string, personId: string,
  state: "free_in_base" | "at_home",
  startsAt: string, endsAt: string, note: string | null
): Promise<void> {
  await apiClient.post(`/spaces/${spaceId}/people/${personId}/presence`, {
    state, startsAt, endsAt, note,
  });
}
