# 397 — Recommendation Frontend TypeScript Types

## Phase
Feature: Double-Shift Recommendation — Frontend Layer

## Purpose
Define TypeScript types that mirror the backend recommendation DTOs, enabling type-safe API communication for the recommendation feature in the Next.js frontend.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/api/recommendations.ts` | TypeScript types for `Recommendation`, `RecommendationBanner`, `AcceptRecommendationResult`, and status/outcome enums |

## Key decisions

- **Types live in the API module** — following the existing project convention where interfaces are co-located with their API functions (e.g., `tasks.ts`, `schedule.ts`).
- **String union types for enums** — `RecommendationStatus` and `AcceptRecommendationOutcome` use TypeScript string literal unions rather than numeric enums, matching the backend's string-serialized enum pattern.
- **camelCase field names** — matching the JSON serialization from the .NET backend (System.Text.Json default).
- **Dates as strings** — `affectedDateStart`, `affectedDateEnd`, and `createdAt` are typed as `string` (ISO 8601), consistent with how other DTOs handle dates in this project.

## How it connects

- These types will be consumed by React Query hooks (`useRecommendations`, `useRecommendationsForRun`, etc.) in task 12.2.
- The `RecommendationBanner` type maps to the `GET /spaces/{spaceId}/runs/{runId}/recommendations` endpoint response.
- The `Recommendation` type maps to `RecommendationDto` from the backend.
- The `AcceptRecommendationResult` type maps to the accept endpoint response.

## How to run / verify

```bash
cd apps/web && npx tsc --noEmit
```

The file should compile without errors.

## What comes next

- Task 12.2: React Query hooks for recommendation API calls using these types.
- Task 13.1: `RecommendationBanner` component consuming `RecommendationBanner` type.
- Task 14.1: `TaskDoubleShiftSuggestion` component consuming `Recommendation` type.

## Git commit

```bash
git add -A && git commit -m "feat(recommendations): add frontend TypeScript types for recommendation DTOs"
```
