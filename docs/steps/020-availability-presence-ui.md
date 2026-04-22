# Step 020 — Availability & Presence Windows UI

## Phase
Post-MVP Completion

## Purpose
The availability and presence window API endpoints were built in step 018 but had no frontend. Admins had no way to record when a person is available to be scheduled (availability windows) or where they physically are (presence windows). This step adds both forms to the person detail page.

## What was built

### Frontend

| File | Description |
|---|---|
| `lib/api/availability.ts` | Added `getPresenceWindows()` — the GET for presence was missing from the client. All four functions now exist: get/add availability, get/add presence. |
| `app/admin/people/[personId]/page.tsx` | Full rewrite to add availability and presence sections alongside the existing roles/groups/restrictions sections. Includes collapsible add-forms, datetime-local inputs, and read-only list views. Also completes the role assignment UI wired up in step 019. |

## Key decisions

### Single page, all person data
All person management (roles, groups, qualifications, availability, presence, restrictions) lives on one page. No sub-routes. This keeps navigation simple for an MVP admin tool.

### datetime-local inputs
The API accepts `DateTime` (ISO 8601). `datetime-local` inputs produce the right format without extra parsing. The `fmt()` helper formats stored UTC datetimes for display using the browser locale.

### Presence state limited to at_home / free_in_base
`on_mission` is always derived from assignments and is never manually set — enforced in the domain. The UI only offers the two manually-settable states.

### Derived presence shown read-only
Presence windows with `isDerived: true` are shown with a "(derived)" label and no edit controls, making it clear they come from solver assignments.

## How it connects
- `GET/POST /spaces/{id}/people/{personId}/availability` — `AvailabilityController` (step 018)
- `GET/POST /spaces/{id}/people/{personId}/presence` — `AvailabilityController` (step 018)
- Solver reads `AvailabilityWindows` via `SolverPayloadNormalizer` to constrain scheduling
- Presence windows feed into the solver's stability objective (people already on mission stay on mission)

## How to run / verify

1. Start the stack: `docker compose -f infra/compose/docker-compose.yml up -d`
2. Login, go to Admin → People → click any person
3. "Availability Windows" section: click "+ Add", fill in start/end datetime, save → entry appears
4. "Presence Windows" section: click "+ Add", choose state (at_home / free_in_base), fill in times, save → entry appears with purple badge
5. Via Swagger: `GET /spaces/{id}/people/{personId}/availability` → returns the saved window

## What comes next
- Notification system when solver completes a run
- PDF export

## Git commit

```bash
git add -A && git commit -m "feat(people): availability and presence windows UI"
```
