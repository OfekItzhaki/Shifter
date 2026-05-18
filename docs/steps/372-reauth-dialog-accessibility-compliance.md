# 372 — ReAuth Dialog Accessibility Compliance

## Phase

Admin Re-Authentication Gate — Frontend Accessibility

## Purpose

Verify and fix the ReAuthDialog component's accessibility compliance, ensuring it meets WCAG requirements for modal dialogs: proper ARIA attributes, focus trapping, initial focus placement, keyboard dismissal, and locale-aware layout direction.

## What was built

### Modified files

- `apps/web/components/admin/ReAuthDialog.tsx`
  - Added `useLocale` import from `next-intl` to determine current locale
  - Changed hardcoded `direction: "rtl"` to conditional `direction: isRtl ? "rtl" : "ltr"` based on locale
  - Fixed initial focus to land on password input (was incorrectly focusing submit button)
  - Added `webAuthnButtonRef` for WebAuthn-only focus fallback
  - Updated focus priority: password input → WebAuthn button → submit button

### Created files

- `apps/web/__tests__/admin/reAuthDialogAccessibility.test.tsx`
  - 16 unit tests covering all accessibility requirements (6.1–6.5)
  - Tests ARIA attributes: `role="dialog"`, `aria-modal="true"`, `aria-labelledby`, `aria-describedby`
  - Tests focus trap: Tab wraps last→first, Shift+Tab wraps first→last
  - Tests initial focus: password input gets focus priority
  - Tests Escape key: calls `onCancel`
  - Tests RTL layout: Hebrew → RTL, English/Russian → LTR
  - Tests modal overlay: prevents background interaction

## Key decisions

- RTL direction is determined by `useLocale() === "he"` — consistent with the pattern used throughout the codebase (CantMakeItModal, ImportModal, QualificationsTab, etc.)
- Initial focus lands on password input because all registered users have a password (system invariant), making it the primary action element
- WebAuthn button focus is a fallback for the theoretical case where password is unavailable
- Tests use real timers (not `vi.useFakeTimers()`) because the component's async credential fetching conflicts with fake timers

## How it connects

- Validates Requirements 6.1, 6.2, 6.3, 6.4, 6.5 from the admin-reauth-gate spec
- Builds on the existing ReAuthDialog component (step 316)
- Complements the credential availability tests (step 371)
- Supports the localization work done in step 369 (Russian locale)

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/admin/reAuthDialogAccessibility.test.tsx --reporter=verbose
```

All 16 tests should pass. Also verify existing credential tests still pass:

```bash
npx vitest run __tests__/admin/reAuthDialogCredentials.test.tsx --reporter=verbose
```

## What comes next

- Task 3.5: Verify loading and submission state handling
- Task 5.3: Property-based test for focus trap containment (Property 6)

## Git commit

```bash
git add -A && git commit -m "feat(admin-reauth): verify and fix ReAuthDialog accessibility compliance"
```
