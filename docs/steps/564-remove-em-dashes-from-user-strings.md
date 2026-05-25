# 564 — Remove Em Dashes from User-Facing Strings

## Phase

Maintenance — Content Quality

## Purpose

Em dashes (—) are a telltale sign of AI-generated content. This step replaces all em dashes in user-facing strings with natural alternatives (hyphens, periods, commas, colons) to make the copy feel more human-written while maintaining a professional tone.

## What was built

| File | Description |
|------|-------------|
| `apps/web/messages/en.json` | Replaced 22 em dashes in English i18n strings |
| `apps/web/messages/he.json` | Replaced 22 em dashes in Hebrew i18n strings |
| `apps/web/messages/ru.json` | Replaced 19 em dashes in Russian i18n strings |
| `apps/api/Jobuler.Infrastructure/Scheduling/SolverWorkerService.cs` | Replaced 6 em dashes in user-facing notification strings (preflight titles, MarkFailed messages, solver notification bodies) |

## Key decisions

- **Replacement rules applied consistently across all three languages:**
  - ` - ` (hyphen with spaces) for separating two related clauses
  - `.` (period) for separating two complete sentences
  - `,` (comma) for introducing a clarification that flows naturally
- **Code comments and logger messages left untouched** — these are developer-facing and don't affect users.
- **MarkFailed strings were modified** since they surface in the UI as error messages shown to space admins.

## How it connects

These strings are rendered by the Next.js frontend via `next-intl` and by the backend notification system (`NotifySpaceAdminsAsync`). No logic changes — purely cosmetic text updates.

## How to run / verify

1. `node -e "JSON.parse(require('fs').readFileSync('apps/web/messages/en.json','utf8'))"` — must exit 0
2. `node -e "JSON.parse(require('fs').readFileSync('apps/web/messages/he.json','utf8'))"` — must exit 0
3. `node -e "JSON.parse(require('fs').readFileSync('apps/web/messages/ru.json','utf8'))"` — must exit 0
4. Grep for `—` in the three JSON files — should return zero matches.
5. Build the API project to confirm no syntax errors in the C# file.

## What comes next

No downstream dependencies. This is a standalone content quality improvement.

## Git commit

```bash
git add -A && git commit -m "chore(i18n): replace em dashes with natural alternatives in user-facing strings"
```
