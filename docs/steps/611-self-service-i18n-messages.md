# 611 — Self-Service i18n Messages

## Phase

Self-Service Scheduling UI — Foundation Layer

## Purpose

Add all Hebrew and English i18n message keys required by the self-service scheduling UI. These keys are consumed by the `next-intl` framework and cover every label, button, validation message, error message, and status text across all self-service components.

## What was built

| File | Description |
|------|-------------|
| `apps/web/messages/he.json` | Added `selfService` namespace with all Hebrew translations |
| `apps/web/messages/en.json` | Added `selfService` namespace with all English translations |

### Key namespaces added under `selfService`:

- **modeSelector** — Scheduling mode selection cards (auto vs self-service)
- **confirmDialog** — Irreversible mode confirmation dialog
- **tabs** — Tab labels for self-service group pages
- **slotBrowser** — Available slots browsing UI (request, waitlist, capacity, filters)
- **myShifts** — Member's shift list (status badges, cancel flow, shift count)
- **waitlist** — Waitlist management (accept/decline offers, countdown, leave)
- **swaps** — Shift swap proposals (propose, accept, decline, cancel, countdown)
- **templates** — Shift template management (CRUD, form labels, validation)
- **config** — Self-service configuration panel (all field labels, validation messages)
- **adminOverrides** — Admin override actions (assign, remove, permission messages)
- **errors** — Error code mappings to user-friendly messages
- **loading / error / retry** — Shared loading and error state strings

## Key decisions

- Followed the existing flat-namespace-per-feature pattern used throughout the app
- Hebrew is the primary language; English serves as fallback
- Validation error messages use descriptive keys (e.g., `config.validation.minGreaterThanMax`) for easy lookup
- Day names are provided as arrays in `slotBrowser.dayNames` matching JS `getDay()` index order
- Error messages are generic enough to cover API error codes while remaining user-friendly

## How it connects

- All self-service tab components will reference these keys via `useTranslations('selfService')`
- The error mapping utility (`selfServiceErrors.ts`) maps backend error slugs to keys under `selfService.errors`
- Validation utilities return `errorKey` strings that correspond to keys under `selfService.config.validation` and `selfService.templates.validation`

## How to run / verify

```bash
# Validate JSON syntax
node -e "JSON.parse(require('fs').readFileSync('apps/web/messages/he.json','utf8')); console.log('OK')"
node -e "JSON.parse(require('fs').readFileSync('apps/web/messages/en.json','utf8')); console.log('OK')"
```

## What comes next

- Error code mapping utility (task 3.2) will reference `selfService.errors.*` keys
- All tab components (tasks 5–12) will consume these message keys for rendering

## Git commit

```bash
git add -A && git commit -m "feat(self-service-ui): add Hebrew and English i18n messages for self-service scheduling"
```
