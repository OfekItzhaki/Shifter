# 291 — Sandbox Zustand Store

## Phase

Feature: Draft Simulation Sandbox (Task 4.1)

## Purpose

Creates the frontend state management layer for the simulation sandbox. The `useSandboxStore` Zustand store owns all sandbox state — session info, baseline solver input, user overrides (tasks, constraints, members, settings), and simulation results. This is the core of the "thick frontend" architecture where all sandbox modifications live in memory until explicitly published or discarded.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/store/sandboxStore.ts` | Zustand store with full `SandboxState` interface, solver DTO types, override types, and all store actions |

## Key decisions

1. **No localStorage persistence** — The store intentionally does NOT use Zustand's `persist` middleware. Sandbox state is ephemeral by design (Requirements 7.1, 7.4). Closing the tab discards all state.

2. **TypeScript solver types defined inline** — Since no frontend TypeScript types existed for `SolverInputDto` / `SolverOutputDto`, they were defined in the store file mirroring the backend C# DTOs. These use snake_case for JSON-serialized fields (matching the solver's Python API) and camelCase for .NET-originated fields.

3. **Map-based override tracking** — `taskOverrides` and `constraintOverrides` use `Map<string, Override>` for O(1) lookup by ID. Each override tracks its `action` (add/edit/remove), the `original` baseline entry (for edit/remove), and the `modified` data (for add/edit).

4. **Set-based member exclusions** — `memberExclusions` uses a `Set<string>` for O(1) toggle operations.

5. **Smart override merging** — When editing a task that was already added in the same session, the edit merges into the existing "add" override rather than creating a separate "edit" entry. Similarly, removing a session-added task simply deletes the override.

6. **Immutable state updates** — All actions create new Map/Set instances to ensure Zustand detects state changes and triggers re-renders.

## How it connects

- **Upstream**: The `enterSandbox` action receives the baseline `SolverInputDto` fetched from `GET /solver-baseline` (Task 1.2)
- **Downstream**: The `buildOverridePayload` function (Task 4.2) will read from this store to merge overrides with the baseline
- **UI consumers**: `SandboxSettingsPanel` (Task 6.2) writes overrides; `SandboxSchedulePreview` (Task 7.1) reads simulation results
- **Publish flow**: Task 8.1 reads all overrides to construct the `PublishSandboxRequest`

## How to run / verify

```bash
# TypeScript compilation check
cd apps/web && npx tsc --noEmit lib/store/sandboxStore.ts
```

The store has no runtime dependencies beyond Zustand (already installed). Full behavioral verification comes in Task 12.1 (unit tests) and Tasks 4.3–4.10 (property tests for the payload builder that consumes this store).

## What comes next

- Task 4.2: `buildOverridePayload` pure function that merges baseline + overrides into a complete `SolverInputDto`
- Task 12.1: Unit tests for all sandbox store actions

## Git commit

```bash
git add -A && git commit -m "feat(sandbox): create useSandboxStore Zustand store with solver types and override actions"
```
