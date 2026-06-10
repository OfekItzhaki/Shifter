import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import ErrorRetry from "../../components/groups/selfService/ErrorRetry";

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => {
    const translations: Record<string, string> = {
      "errors.absenceRejected": "Absence report rejected",
      error: "Something went wrong",
      retry: "Retry",
    };
    return translations[key] ?? key;
  },
}));

describe("ErrorRetry", () => {
  it("translates self-service i18n keys before displaying them", () => {
    render(
      <ErrorRetry
        message="selfService.errors.absenceRejected"
        onRetry={vi.fn()}
      />
    );

    expect(screen.getByText("Absence report rejected")).toBeInTheDocument();
    expect(screen.queryByText("selfService.errors.absenceRejected")).not.toBeInTheDocument();
  });

  it("displays direct backend detail messages unchanged", () => {
    render(
      <ErrorRetry
        message="Choose a target shift before approving this change request."
        onRetry={vi.fn()}
      />
    );

    expect(screen.getByText("Choose a target shift before approving this change request.")).toBeInTheDocument();
  });
});
