# Self-Service Holiday Calendar Contract

Holiday calendars are part of scheduling, but they should not be treated as
manual self-service support until they affect the member and admin workflows in
a visible, tested way.

Use this contract when integrating `feat/holiday-calendars` after
`feat/manual-self-service-hardening`.

## Product Behavior

Manual self-service holiday support should cover three levels:

- Awareness: managers can mark holidays, closures, special demand days, or
  reduced-staffing days for a space.
- Policy: self-service groups can decide whether members may pick, cancel,
  change, swap, report absence, or request special leave on those days.
- Planning: generated cycles, templates, slots, and closeout summaries show
  holiday context where it changes staffing expectations.

Do not call the feature complete if holidays only exist in space settings.
Members and managers must see the effect inside self-service scheduling.

## Required Integration Points

When `SpaceSpecialDay` and `SpecialDaysCard` are merged, self-service should
gain explicit behavior for:

- cycle generation warnings when a cycle contains marked special days
- slot labels or badges for marked special days
- manager policy controls for whether special-day slots are open for picking
- special leave validation when a request overlaps a marked special day
- absence/reporting limits when a special day has stricter coverage rules
- closeout metrics that separate normal days from special days when relevant

The first version is conservative: marked special days are visible in member and
admin workflows, and `requiresCoverage=false` is treated as a no-coverage day
for member self-service. Members cannot pick those slots, join their waitlists,
or accept stale waitlist offers for them; admin override tools remain available
for explicit exceptions. Special leave requests overlapping no-coverage special
days are rejected, while requests overlapping coverage-required special days are
allowed and highlighted to admins. Other holiday-specific staffing policies
remain future scope. Silent behavior changes would be worse than no integration.

## Data Rules

Holiday/special-day records must be scoped to the same space boundary as
self-service defaults and groups.

- A special day in one space must not affect another space.
- A special day should not affect a group unless that group's cycle or slots
  overlap it.
- A solver-only holiday input must not change manual self-service groups unless
  manual self-service code explicitly reads the same policy.
- Imported/customer-hosted installations must include special-day records if the
  space uses them for self-service policy.

## Test Gate

Before claiming holiday calendars are supported in manual self-service, add
tests for at least:

1. A self-service cycle that overlaps a special day exposes a visible warning or
   label.
2. A normal self-service cycle without special days behaves exactly as before.
3. A no-coverage special day blocks member picks, waitlist joins, waitlist offer
   cascades, and stale waitlist offer acceptance.
4. Special leave requests reject no-coverage special-day overlap and surface
   coverage-required special-day context to admins.
5. Tenant/space isolation prevents a special day from leaking into another
   space's self-service schedule.
6. Closeout or operations status shows special-day impact when the group has
   holiday-aware behavior enabled.

## Merge Gate

Before merging `feat/holiday-calendars` after manual self-service:

1. Preserve all manual self-service workflows and browser coverage.
2. Re-run special leave approve/reject/cancel coverage because the holiday
   branch touches special leave code.
3. Confirm special-day solver inputs do not silently affect self-service groups.
4. Add at least one self-service holiday/special-day test in the same branch.
5. Update the manual QA checklist with the new holiday smoke path.
