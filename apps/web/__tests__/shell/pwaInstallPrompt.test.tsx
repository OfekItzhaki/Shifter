import { render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import PwaInstallPrompt from "@/components/shell/PwaInstallPrompt";

let coarsePointer = false;

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => ({
    title: "Install Shifter",
    description: "Install Shifter on this device for faster access and offline schedule viewing.",
    iosDescription: "On iPhone, tap Share, then Add to Home Screen to install Shifter.",
    install: "Install",
    notNow: "Not now",
    dismiss: "Dismiss",
  }[key] ?? key),
}));

function createInstallEvent() {
  const event = new Event("beforeinstallprompt", { cancelable: true }) as Event & {
    prompt: () => Promise<void>;
    userChoice: Promise<{ outcome: "accepted"; platform: string }>;
  };
  event.prompt = vi.fn(() => Promise.resolve());
  event.userChoice = Promise.resolve({ outcome: "accepted", platform: "web" });
  return event;
}

describe("PwaInstallPrompt", () => {
  beforeEach(() => {
    coarsePointer = false;
    localStorage.clear();
    Object.defineProperty(window, "matchMedia", {
      configurable: true,
      value: vi.fn((query: string) => ({
        matches: query === "(pointer: coarse)" ? coarsePointer : false,
        media: query,
        onchange: null,
        addListener: vi.fn(),
        removeListener: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn(),
      })),
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("shows the custom install prompt on desktop browsers when install is available", async () => {
    render(<PwaInstallPrompt />);

    window.dispatchEvent(createInstallEvent());

    expect(await screen.findByText("Install Shifter")).toBeInTheDocument();
  });

  it("shows the custom install prompt on mobile install surfaces", async () => {
    coarsePointer = true;
    render(<PwaInstallPrompt />);

    window.dispatchEvent(createInstallEvent());

    expect(await screen.findByText("Install Shifter")).toBeInTheDocument();
  });
});
