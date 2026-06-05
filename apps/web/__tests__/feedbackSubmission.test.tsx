import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useFeedbackSubmission } from "@/hooks/useFeedbackSubmission";
import { apiClient } from "@/lib/api/client";

vi.mock("@/lib/api/client", () => ({
  apiClient: {
    post: vi.fn(),
  },
}));

const mockedPost = vi.mocked(apiClient.post);

describe("useFeedbackSubmission", () => {
  beforeEach(() => {
    mockedPost.mockReset();
  });

  it("surfaces ProblemDetails detail for validation failures", async () => {
    mockedPost.mockRejectedValueOnce({
      response: {
        status: 400,
        headers: {},
        data: {
          title: "Validation Failed",
          detail: "Type must be 'bug' or 'feedback'.",
        },
      },
    });

    const { result } = renderHook(() => useFeedbackSubmission());

    await act(async () => {
      await result.current.submit({ type: "feedback", description: "Great work" });
    });

    await waitFor(() => {
      expect(result.current.status).toBe("error");
    });
    expect(result.current.errorMessage).toBe("Type must be 'bug' or 'feedback'.");
  });
});
