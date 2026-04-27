import { apiClient } from "./client";

export interface NotificationDto {
  id: string;
  eventType: string;
  title: string;
  body: string;
  isRead: boolean;
  createdAt: string;
  metadataJson: string | null;
}

export async function getNotifications(
  spaceId: string
): Promise<NotificationDto[]> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/notifications`, { params: { unreadOnly: true } }
  );
  return data;
}

export async function dismissNotification(
  spaceId: string, notificationId: string
): Promise<void> {
  await apiClient.post(
    `/spaces/${spaceId}/notifications/${notificationId}/read`
  );
}

export async function dismissAllNotifications(spaceId: string): Promise<void> {
  await apiClient.post(`/spaces/${spaceId}/notifications/read-all`);
}
