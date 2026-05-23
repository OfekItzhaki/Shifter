/**
 * Unit tests for scope isolation between login form and ReAuthDialog.
 *
 * Verifies:
 * - Login form password input retains `autocomplete="current-password"`
 * - Login form does NOT have `autocomplete="off"` on its form element
 * - Login form does NOT have `name="reauth-verify"` on any input
 * - ReAuthDialog is the only component with autofill-prevention attributes
 *
 * Requirements: 5.1, 5.2
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";

// ── Mocks for ReAuthDialog ────────────────────────────────────────────────────

const mockListCredentials = vi.fn();
const mockIsWebAuthnSupported = vi.fn();

vi.mock("@/lib/webauthn", () => ({
  listCredentials: (...args: any[]) => mockListCredentials(...args),
  isWebAuthnSupported: () => mockIsWebAuthnSupported(),
  isConditionalMediationAvailable: () => Promise.resolve(false),
  authenticateWithBiometric: vi.fn(),
}));

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => {
    const translations: Record<string, string> = {
      title: "Confirm Identity",
      description: "Please confirm your identity to continue.",
      passwordLabel: "Password",
      passwordPlaceholder: "Enter your password",
      confirm: "Confirm",
      verifying: "Verifying...",
      webAuthnButton: "Use Fingerprint",
      webAuthnLabel: "Authenticate with fingerprint",
      or: "or",
      cancel: "Cancel",
      close: "Close",
      loadingCredentials: "Loading...",
      noCredentials: "No credentials configured.",
      authFailed: "Authentication failed. Please try again.",
      rateLimited: "Too many attempts. Please try again later.",
      networkError: "Connection error. Please try again.",
      webAuthnCancelled: "Verification cancelled. Please try again.",
      // Login form translations
      login: "Login",
      emailOrPhone: "Email or Phone",
      password: "Password",
      loginButton: "Sign In",
      signingIn: "Signing in...",
      invalidCredentials: "Invalid credentials",
      forgotPassword: "Forgot password",
      noAccount: "Don't have an account?",
      registerButton: "Register",
      accountCreated: "Account created successfully!",
      passwordResetSuccess: "Password reset successfully!",
    };
    return translations[key] ?? key;
  },
  useLocale: () => "en",
}));

const mockApiGet = vi.fn();
const mockApiPost = vi.fn();

vi.mock("@/lib/api/client", () => ({
  apiClient: {
    get: (...args: any[]) => mockApiGet(...args),
    post: (...args: any[]) => mockApiPost(...args),
  },
}));

vi.mock("@/lib/store/authStore", () => ({
  useAuthStore: () => ({
    login: vi.fn(),
  }),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn(),
  }),
  useSearchParams: () => ({
    get: () => null,
  }),
}));

vi.mock("next/link", () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

vi.mock("@/components/shell/ShifterLogo", () => ({
  default: () => <div data-testid="shifter-logo" />,
}));

vi.mock("@/components/LanguageSwitcher", () => ({
  default: () => <div data-testid="language-switcher" />,
}));

vi.mock("@/lib/utils/detectLocale", () => ({
  detectBrowserLocale: () => "en",
}));

// ── Imports (after mocks) ─────────────────────────────────────────────────────

import ReAuthDialog from "../../components/admin/ReAuthDialog";
import LoginForm from "../../app/login/page";

// ── Test Suite ────────────────────────────────────────────────────────────────

describe("Scope Isolation — Login form vs ReAuthDialog (Task 6.2)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockIsWebAuthnSupported.mockReturnValue(false);
    mockListCredentials.mockResolvedValue([]);
    mockApiGet.mockResolvedValue({ data: [] });
    mockApiPost.mockResolvedValue({ data: {} });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // ── Login form retains password manager support (Req 5.1) ──────────────────

  describe("Login form retains password manager support (Req 5.1)", () => {
    it("login form password input has autocomplete='current-password'", () => {
      render(<LoginForm />);

      const passwordInput = screen.getByPlaceholderText("••••••••");
      expect(passwordInput).toHaveAttribute("autocomplete", "current-password");
    });

    it("login form does NOT have autocomplete='off' on its form element", () => {
      const { container } = render(<LoginForm />);

      const form = container.querySelector("form");
      expect(form).not.toBeNull();
      expect(form).not.toHaveAttribute("autocomplete", "off");
    });

    it("login form does NOT have name='reauth-verify' on any input", () => {
      const { container } = render(<LoginForm />);

      const inputs = container.querySelectorAll("input");
      inputs.forEach((input) => {
        expect(input).not.toHaveAttribute("name", "reauth-verify");
      });
    });

    it("login form does NOT have autocomplete='new-password' on any input", () => {
      const { container } = render(<LoginForm />);

      const inputs = container.querySelectorAll("input");
      inputs.forEach((input) => {
        expect(input).not.toHaveAttribute("autocomplete", "new-password");
      });
    });
  });

  // ── ReAuthDialog is the only component with autofill-prevention (Req 5.2) ──

  describe("ReAuthDialog has autofill-prevention attributes (Req 5.2)", () => {
    async function waitForReAuthLoaded() {
      await waitFor(() => {
        expect(screen.getByLabelText("Password")).toBeInTheDocument();
      });
    }

    it("ReAuthDialog password input has autocomplete='new-password'", async () => {
      render(
        <ReAuthDialog
          open={true}
          onSuccess={vi.fn()}
          onCancel={vi.fn()}
          mode="management"
        />
      );
      await waitForReAuthLoaded();

      const passwordInput = screen.getByLabelText("Password");
      expect(passwordInput).toHaveAttribute("autocomplete", "new-password");
    });

    it("ReAuthDialog password input has name='reauth-verify'", async () => {
      render(
        <ReAuthDialog
          open={true}
          onSuccess={vi.fn()}
          onCancel={vi.fn()}
          mode="management"
        />
      );
      await waitForReAuthLoaded();

      const passwordInput = screen.getByLabelText("Password");
      expect(passwordInput).toHaveAttribute("name", "reauth-verify");
    });

    it("ReAuthDialog form has autocomplete='off'", async () => {
      const { container } = render(
        <ReAuthDialog
          open={true}
          onSuccess={vi.fn()}
          onCancel={vi.fn()}
          mode="management"
        />
      );
      await waitForReAuthLoaded();

      const form = container.querySelector("form");
      expect(form).not.toBeNull();
      expect(form).toHaveAttribute("autocomplete", "off");
    });

    it("ReAuthDialog renders no DOM when open=false", () => {
      const { container } = render(
        <ReAuthDialog
          open={false}
          onSuccess={vi.fn()}
          onCancel={vi.fn()}
          mode="management"
        />
      );

      // Should render nothing
      expect(container.innerHTML).toBe("");
    });
  });
});
