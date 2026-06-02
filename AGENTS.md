# AGENTS.md

This repository follows The Horizon Standard.

## Purpose
Build production-grade software with a bias toward correctness, clarity, maintainability, testability, and small safe iterations.

## Instruction priority
When rules conflict, follow this order:
1. Direct user request
2. This AGENTS.md
3. `./docs/The-Horizon-Standard.md`
4. Repository README, docs, ADRs, and local package-level instructions
5. Existing codebase patterns

## Canonical Horizon reference
- `./docs/The-Horizon-Standard.md` is the canonical reference for the full Horizon Standard.
- If a rule is detailed there, follow it.
- Use it for architecture, Git, testing, security, naming, and code quality patterns.

## Before starting work
- Read this file first.
- Read `./docs/The-Horizon-Standard.md` next.
- Review similar existing code before proposing or writing new patterns.
- Find commands from the repository itself: README, scripts, build configs, and CI workflows.
- For any non-trivial task, summarize understanding and propose a short plan before editing.
- If requirements are unclear, ask concise clarification questions before implementation.

## Working style
- Prefer small, reviewable, low-risk changes.
- Fix root causes when practical, not only symptoms.
- Do not change unrelated code.
- Preserve the existing architecture unless the task requires a change.
- Match naming, folder structure, and style already used in the codebase.
- Reuse existing services, utilities, hooks, components, DTOs, validators, and patterns before adding new ones.

## Git rules
- Keep commits atomic: one logical change per commit.
- Use conventional commits: `type(scope): description`.
- Allowed types: `feat`, `fix`, `refactor`, `docs`, `test`, `chore`, `perf`, `ci`, `build`, `style`.
- Prefer squash merge for feature branches unless the repo uses a different strategy.
- Do not mix unrelated changes in the same commit.
- Do not rewrite git history unless explicitly asked.

## Safety rules
- Never hardcode secrets, API keys, tokens, passwords, or connection strings.
- Never commit secrets or sensitive data.
- Never log sensitive data such as tokens, passwords, or PII.
- Flag risky operations before performing them, especially destructive shell commands, large deletions, migrations, or mass renames.
- Never delete large sections of code without a clear reason.
- Never overwrite user work or local configuration without confirmation.
- For schema or migration changes, explain impact briefly before editing.

## Code quality rules
- Prefer explicit, readable code over clever abstractions.
- Follow existing architectural patterns first.
- Keep classes, services, and components focused and small.
- Split files that become too large or mix concerns.
- Use descriptive names; avoid unnecessary abbreviations.
- Keep public interfaces stable unless change is required.
- Update nearby documentation when behavior or setup changes.

## Type safety rules
- Do not use `any` in TypeScript except in rare, justified cases or generated code.
- Use proper strong typing in all languages used by the project.
- Prefer explicit DTOs, request models, response models, and contracts.
- Keep backend and frontend contracts aligned.
- If API contracts change, update generated clients or dependent types where relevant.

## Architecture rules
- Keep boundaries clear between UI, application, domain, infrastructure, and data access concerns.
- Keep controllers and routes thin; move business logic into services or domain layers.
- Validate inputs at the boundary.
- Use dependency injection and existing composition patterns rather than manual service construction.
- Use centralized error handling patterns already adopted by the project.
- Avoid silent failures; propagate or handle errors explicitly.

## Testing and verification
Before marking a task complete, do the narrowest meaningful verification available.

Preferred order:
1. Lint or static analysis
2. Build or type-check
3. Unit tests
4. Integration tests
5. End-to-end or smoke tests when relevant

Rules:
- Never claim code works without verification.
- If tests or builds cannot be run, state that clearly.
- For new behavior, add or update tests when the repo already has a testing pattern for that area.
- Keep tests independent and readable.

## Pre-commit checklist
Before committing, complete the relevant subset of these checks:
- Run linting and fix issues.
- Ensure formatting is correct.
- Ensure the project builds successfully.
- Run the relevant tests.
- Confirm no secrets were added.
- Confirm naming and architecture follow project conventions.
- Stage only intended files.
- Use a conventional commit message.

## Definition of done
A task is done only when all relevant items below are satisfied:
- Code matches existing patterns and architecture.
- No obvious shortcuts in typing or validation.
- Error handling is implemented correctly.
- Linting passes or issues are explicitly called out.
- Build/type-check passes or inability is stated clearly.
- Relevant tests pass or inability is stated clearly.
- Documentation is updated if needed.
- No secrets or unsafe debug code were introduced.
- Changes are summarized clearly.

## What to avoid
- Large speculative rewrites
- Broad formatting-only churn across unrelated files
- Placeholder implementations presented as complete
- Silent breaking changes
- Unverified performance claims
- Ignoring existing patterns in favor of new personal preferences
- Hidden architectural changes without calling them out

## Monorepo rule
If this repository contains multiple apps or packages:
- Prefer the nearest package-level instructions when present.
- Keep changes isolated to the target app or package unless shared code must change.
- State cross-package impact explicitly.

## Repo-specific customization
This repository is intended to be plug and play. Do not add placeholder commands here.

Instead, discover commands from the repository itself using:
- README files
- package manifests
- build configs
- scripts
- CI workflows

If deeper `AGENTS.md` files exist in subdirectories, follow them for that scope.