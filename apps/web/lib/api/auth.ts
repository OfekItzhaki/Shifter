import { apiClient } from "./client";

export interface LoginResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  userId: string;
  displayName: string;
  preferredLocale: string;
  isPlatformAdmin: boolean;
  timezoneId: string | null;
  timezoneOffsetMinutes: number | null;
}

export async function login(identifier: string, password: string): Promise<LoginResponse> {
  const { data } = await apiClient.post<LoginResponse>("/auth/login", { identifier, password });
  return data;
}

export async function register(
  displayName: string,
  password: string,
  preferredLocale = "he",
  email?: string,
  phoneNumber?: string,
  profileImageUrl?: string,
  birthday?: string
): Promise<{ userId: string }> {
  const { data } = await apiClient.post("/auth/register", {
    displayName, password, preferredLocale,
    ...(email ? { email } : {}),
    ...(phoneNumber ? { phoneNumber } : {}),
    ...(profileImageUrl ? { profileImageUrl } : {}),
    ...(birthday ? { birthday } : {}),
  });
  return data;
}

export interface MeDto {
  userId: string;
  email: string;
  displayName: string;
  phoneNumber: string | null;
  profileImageUrl: string | null;
  birthday: string | null;
  createdAt: string;
  emailVerified: boolean;
  isPlatformAdmin?: boolean;
}

export async function getMe(): Promise<MeDto> {
  const { data } = await apiClient.get<MeDto>("/auth/me");
  return data;
}

export async function updateMe(payload: {
  displayName?: string;
  phoneNumber?: string;
  profileImageUrl?: string;
  birthday?: string;
}): Promise<void> {
  await apiClient.put("/auth/me", payload);
}

export async function logout(): Promise<void> {
  await apiClient.post("/auth/logout", {});
}

export async function forgotPassword(email: string): Promise<void> {
  try {
    await apiClient.post("/auth/forgot-password", { email });
  } catch {
    // Silently ignore errors — we never reveal whether the email exists
  }
}

export async function resetPassword(token: string, newPassword: string): Promise<void> {
  const { data } = await apiClient.post("/auth/reset-password", { token, newPassword });
  return data;
}

export async function verifyEmail(token: string): Promise<void> {
  await apiClient.post("/auth/verify-email", { token });
}

export async function resendVerification(): Promise<void> {
  await apiClient.post("/auth/resend-verification");
}
