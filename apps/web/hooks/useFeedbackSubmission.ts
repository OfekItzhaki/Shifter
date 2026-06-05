"use client";

import { useCallback, useState } from "react";
import { apiClient } from "@/lib/api/client";
import { AxiosError } from "axios";

export interface SubmitFeedbackPayload {
  type: "bug" | "feedback";
  description: string;
}

export interface UseFeedbackSubmissionReturn {
  submit: (payload: SubmitFeedbackPayload) => Promise<void>;
  status: "idle" | "loading" | "success" | "error";
  errorMessage: string | null;
  retryAfterSeconds: number | null;
  reset: () => void;
}

/**
 * Hook for submitting feedback or bug reports via the Feedback API.
 *
 * - Sends a POST to `/feedback` with a 10-second timeout.
 * - Parses the `Retry-After` header on 429 responses.
 * - Handles network errors and timeouts gracefully.
 *
 * Requirements: 4.1, 4.5, 7.3
 */
export function useFeedbackSubmission(): UseFeedbackSubmissionReturn {
  const [status, setStatus] = useState<UseFeedbackSubmissionReturn["status"]>("idle");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [retryAfterSeconds, setRetryAfterSeconds] = useState<number | null>(null);

  const submit = useCallback(async (payload: SubmitFeedbackPayload): Promise<void> => {
    setStatus("loading");
    setErrorMessage(null);
    setRetryAfterSeconds(null);

    try {
      await apiClient.post("/feedback", payload, {
        timeout: 10_000,
      });

      setStatus("success");
    } catch (err: unknown) {
      const axiosError = err as AxiosError;

      // Timeout: Axios sets code to "ECONNABORTED" or "ERR_CANCELED" for timeouts
      if (
        axiosError.code === "ECONNABORTED" ||
        (axiosError.code === "ERR_CANCELED" && axiosError.message?.includes("timeout"))
      ) {
        setStatus("error");
        setErrorMessage("Request timed out. Please try again.");
        return;
      }

      // No response at all — network error
      if (!axiosError.response) {
        setStatus("error");
        setErrorMessage("Network error. Please check your connection and try again.");
        return;
      }

      const { status: httpStatus, headers, data } = axiosError.response;

      // 429 — rate limited
      if (httpStatus === 429) {
        const retryAfter = headers?.["retry-after"];
        const seconds = retryAfter ? parseInt(retryAfter, 10) : null;
        setRetryAfterSeconds(Number.isFinite(seconds) ? seconds : null);
        setStatus("error");
        setErrorMessage(
          "You've reached the submission limit. Please try again later."
        );
        return;
      }

      // 400 — validation error
      if (httpStatus === 400) {
        const responseData = data as {
          errors?: Record<string, string[]>;
          message?: string;
          detail?: string;
          title?: string;
        };
        const firstError = responseData?.errors
          ? Object.values(responseData.errors).flat()[0]
          : responseData?.message ?? responseData?.detail ?? responseData?.title;
        setStatus("error");
        setErrorMessage(firstError ?? "Invalid submission. Please check your input.");
        return;
      }

      // Other server errors (500, etc.)
      const responseData = data as { message?: string; detail?: string; title?: string };
      setStatus("error");
      setErrorMessage(
        responseData?.message ?? responseData?.detail ?? responseData?.title ?? "Could not process your submission. Please try again."
      );
    }
  }, []);

  const reset = useCallback(() => {
    setStatus("idle");
    setErrorMessage(null);
    setRetryAfterSeconds(null);
  }, []);

  return { submit, status, errorMessage, retryAfterSeconds, reset };
}
