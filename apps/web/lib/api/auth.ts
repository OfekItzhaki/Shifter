import { apiClient } from "./client";

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  userId: string;
  displayName: string;
  preferredLocale: string;
  isPlatformAdmin: boolean;
}

export async function login(email: string, password: string): Promise<LoginResponse> {
  const { data } = await apiClient.post<LoginResponse>("/auth/login", { email, password });
  return data;
}

export async function register(
  email: string,
  displayName: string,
  password: string,
  preferredLocale = "he",
  phoneNumber?: string,
  profileImageUrl?: string,
  birthday?: string
): Promise<{ userId: string }> {
  const { data } = await apiClient.post("/auth/register", {
    email, displayName, password, preferredLocale,
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

export async function logout(refreshToken: string): Promise<void> {
  await apiClient.post("/auth/logout", { refreshToken });
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
