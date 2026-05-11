# 146 — Vitest Testing Infrastructure

## Phase

Custom Error Pages — Testing Foundation

## Purpose

Establish the unit and property-based testing infrastructure needed for the custom error pages feature. This provides Vitest (test runner), fast-check (property-based testing), @testing-library/react (component rendering), and axe-core (accessibility validation) as dev dependencies.

## What was built

| File | Description |
|------|-------------|
| `apps/web/package.json` | Added `vitest`, `@vitejs/plugin-react`, `jsdom`, `fast-check`, `@testing-library/react`, `@testing-library/jest-dom`, `axe-core`, `@axe-core/react` to devDependencies; added `"test": "vitest --run"` script |
| `apps/web/vitest.config.ts` | Vitest configuration with jsdom environment, React plugin, and `@/*` path alias matching tsconfig.json |
| `apps/web/vitest.setup.ts` | Setup file importing `@testing-library/jest-dom/vitest` for extended DOM matchers |

## Key decisions

- **Vitest over Jest**: Vitest shares Vite's transform pipeline, making it faster for a Vite/Next.js project and natively supporting TypeScript and ESM.
- **jsdom environment**: Required for rendering React components in tests without a real browser.
- **Path alias mirroring**: The `@/*` alias in vitest.config.ts matches the tsconfig.json `paths` so imports resolve identically in tests and production code.
- **Setup file**: Centralizes `@testing-library/jest-dom` matchers so every test file gets `.toBeInTheDocument()`, `.toHaveAttribute()`, etc. without explicit imports.

## How it connects

- All subsequent unit tests (tasks 1.3, 3.5, 5.2, 6.2) and property-based tests (tasks 6.3, 6.4, 8.1) depend on this infrastructure.
- The `npm test` script is the single entry point for running all Vitest tests.

## How to run / verify

```bash
cd apps/web
npm install
npm test
```

The `npm test` command should execute Vitest and report "no test files found" (since no tests exist yet). A successful exit confirms the infrastructure is wired correctly.

## What comes next

- Task 1.2: Create the shared `ErrorPageLayout` component
- Task 1.3: Write unit tests for `ErrorPageLayout` (first consumer of this test infrastructure)

## Git commit

```bash
git add -A && git commit -m "feat(error-pages): add vitest testing infrastructure"
```
