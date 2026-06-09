import { apiClient } from "@/lib/api/client";

export interface AiChatMessage {
  role: "user" | "assistant";
  content: string;
}

export interface AiChatAction {
  type: "feedback" | "contact" | "open_path";
  label: string;
  payload: string | null;
}

export interface AiChatResponse {
  message: string;
  suggestedActions: AiChatAction[];
}

export async function sendAiChatMessage(
  spaceId: string,
  payload: {
    message: string;
    locale: string;
    currentPath: string;
    isAdminMode: boolean;
    recentMessages: AiChatMessage[];
  }
): Promise<AiChatResponse> {
  const { data } = await apiClient.post<AiChatResponse>(`/spaces/${spaceId}/ai/chat`, payload);
  return data;
}
