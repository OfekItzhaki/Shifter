import { apiClient } from "@/lib/api/client";

// ─── Feature Detection ───────────────────────────────────────────────────────

/**
 * Returns true if the browser supports WebAuthn (PublicKeyCredential API).
 */
export function isWebAuthnSupported(): boolean {
  return (
    typeof window !== "undefined" &&
    window.PublicKeyCredential !== undefined &&
    typeof window.PublicKeyCredential === "function"
  );
}

/**
 * Returns true if the device has a platform authenticator available
 * (e.g. fingerprint reader, Windows Hello, Face ID, Touch ID).
 *
 * Use this before offering biometric registration — a desktop PC without
 * Windows Hello or a fingerprint reader will return false, preventing
 * the biometric prompt from appearing on devices that can't use it.
 */
export async function isPlatformAuthenticatorAvailable(): Promise<boolean> {
  if (!isWebAuthnSupported()) return false;
  try {
    return await PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable();
  } catch {
    return false;
  }
}

// ─── Base64url ↔ ArrayBuffer Helpers ─────────────────────────────────────────

function base64urlToArrayBuffer(base64url: string): ArrayBuffer {
  let base64 = base64url.replace(/-/g, "+").replace(/_/g, "/");
  while (base64.length % 4 !== 0) {
    base64 += "=";
  }
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes.buffer;
}

function arrayBufferToBase64url(buffer: ArrayBuffer): string {
  const bytes = new Uint8Array(buffer);
  let binary = "";
  for (let i = 0; i < bytes.length; i++) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

// ─── Types ───────────────────────────────────────────────────────────────────

export interface WebAuthnCredential {
  id: string;
  nickname: string | null;
  createdAt: string;
  lastUsedAt: string | null;
  isDisabled: boolean;
}

export interface LoginTokens {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  userId: string;
  displayName: string;
  preferredLocale: string;
  isPlatformAdmin: boolean;
  timezoneId: string | null;
  timezoneOffsetMinutes: number | null;
}

// ─── Registration ────────────────────────────────────────────────────────────

/**
 * Full WebAuthn credential registration flow:
 * 1. Fetch registration options from server
 * 2. Call navigator.credentials.create() with those options
 * 3. Send the attestation response back to the server
 *
 * @param nickname Optional friendly name for the credential
 * @returns The new credential ID (GUID)
 * @throws Error with message "USER_CANCELLED" if user cancels the authenticator prompt
 */
export async function registerCredential(nickname?: string): Promise<string> {
  // Step 1: Get registration options from server
  const { data: optionsData } = await apiClient.post("/auth/webauthn/register/options");
  const options = JSON.parse(optionsData.optionsJson);
  const challengeId: string = optionsData.challengeId;

  // Convert base64url fields to ArrayBuffer for the browser API
  options.challenge = base64urlToArrayBuffer(options.challenge);
  options.user.id = base64urlToArrayBuffer(options.user.id);
  if (options.excludeCredentials) {
    options.excludeCredentials = options.excludeCredentials.map(
      (cred: { id: string; type: string; transports?: string[] }) => ({
        ...cred,
        id: base64urlToArrayBuffer(cred.id),
      })
    );
  }

  // Step 2: Call navigator.credentials.create()
  let credential: PublicKeyCredential;
  try {
    const result = await navigator.credentials.create({
      publicKey: options,
    });
    if (!result) throw new Error("USER_CANCELLED");
    credential = result as PublicKeyCredential;
  } catch (err: any) {
    if (err.name === "NotAllowedError") {
      throw new Error("USER_CANCELLED");
    }
    throw err;
  }

  // Step 3: Send attestation response to server
  const attestationResponse = credential.response as AuthenticatorAttestationResponse;
  const attestationResponseJson = JSON.stringify({
    id: credential.id,
    rawId: arrayBufferToBase64url(credential.rawId),
    type: credential.type,
    response: {
      attestationObject: arrayBufferToBase64url(attestationResponse.attestationObject),
      clientDataJSON: arrayBufferToBase64url(attestationResponse.clientDataJSON),
    },
  });

  const { data: completeData } = await apiClient.post("/auth/webauthn/register/complete", {
    challengeId,
    attestationResponseJson,
    nickname: nickname || null,
  });

  return completeData.credentialId;
}

/**
 * Check if the browser supports conditional mediation (passkey autofill).
 */
export async function isConditionalMediationAvailable(): Promise<boolean> {
  if (!isWebAuthnSupported()) return false;
  if (typeof PublicKeyCredential.isConditionalMediationAvailable !== "function") return false;
  return PublicKeyCredential.isConditionalMediationAvailable();
}

// ─── Authentication ──────────────────────────────────────────────────────────

export interface AuthenticateOptions {
  /** Use "conditional" for passkey autofill (triggered by autocomplete). Default is undefined (modal). */
  mediation?: "conditional" | "optional" | "required" | "silent";
}

/**
 * Full WebAuthn authentication flow:
 * 1. Fetch authentication options from server
 * 2. Call navigator.credentials.get() with those options
 * 3. Send the assertion response back to the server
 * 4. Receive JWT tokens
 *
 * @param opts Optional settings (e.g. mediation: "conditional" for passkey autofill)
 * @returns Login tokens (same format as email+password login)
 * @throws Error with message "USER_CANCELLED" if user cancels the authenticator prompt
 */
export async function authenticateWithBiometric(opts?: AuthenticateOptions): Promise<LoginTokens> {
  // Step 1: Get authentication options from server
  const { data: optionsData } = await apiClient.post("/auth/webauthn/login/options");
  const options = JSON.parse(optionsData.optionsJson);
  const challengeId: string = optionsData.challengeId;

  // Convert base64url fields to ArrayBuffer for the browser API
  options.challenge = base64urlToArrayBuffer(options.challenge);
  if (options.allowCredentials) {
    options.allowCredentials = options.allowCredentials.map(
      (cred: { id: string; type: string; transports?: string[] }) => ({
        ...cred,
        id: base64urlToArrayBuffer(cred.id),
      })
    );
  }

  // Step 2: Call navigator.credentials.get()
  let credential: PublicKeyCredential;
  try {
    const getOptions: CredentialRequestOptions = { publicKey: options };
    if (opts?.mediation) {
      (getOptions as any).mediation = opts.mediation;
    }
    const result = await navigator.credentials.get(getOptions);
    if (!result) throw new Error("USER_CANCELLED");
    credential = result as PublicKeyCredential;
  } catch (err: any) {
    if (err.name === "NotAllowedError") {
      throw new Error("USER_CANCELLED");
    }
    throw err;
  }

  // Step 3: Send assertion response to server
  const assertionResponse = credential.response as AuthenticatorAssertionResponse;
  const assertionResponseJson = JSON.stringify({
    id: credential.id,
    rawId: arrayBufferToBase64url(credential.rawId),
    type: credential.type,
    response: {
      authenticatorData: arrayBufferToBase64url(assertionResponse.authenticatorData),
      clientDataJSON: arrayBufferToBase64url(assertionResponse.clientDataJSON),
      signature: arrayBufferToBase64url(assertionResponse.signature),
      userHandle: assertionResponse.userHandle
        ? arrayBufferToBase64url(assertionResponse.userHandle)
        : null,
    },
  });

  const { data } = await apiClient.post<LoginTokens>("/auth/webauthn/login/complete", {
    challengeId,
    assertionResponseJson,
  });

  return data;
}

// ─── Credential Management ───────────────────────────────────────────────────

/**
 * List all WebAuthn credentials for the current user.
 *
 * @param token Optional access token to use directly in the Authorization header,
 *              bypassing the apiClient interceptor. Use this when calling immediately
 *              after login to avoid a race condition where the interceptor may not
 *              yet have the fresh token available.
 */
export async function listCredentials(token?: string): Promise<WebAuthnCredential[]> {
  if (token) {
    const { data } = await apiClient.get<WebAuthnCredential[]>("/auth/webauthn/credentials", {
      headers: { Authorization: `Bearer ${token}` },
    });
    return data;
  }
  const { data } = await apiClient.get<WebAuthnCredential[]>("/auth/webauthn/credentials");
  return data;
}

/**
 * Delete a WebAuthn credential by ID.
 */
export async function deleteCredential(id: string): Promise<void> {
  await apiClient.delete(`/auth/webauthn/credentials/${id}`);
}

/**
 * Update the nickname of a WebAuthn credential.
 */
export async function updateCredentialNickname(id: string, nickname: string | null): Promise<void> {
  await apiClient.patch(`/auth/webauthn/credentials/${id}`, { nickname });
}
