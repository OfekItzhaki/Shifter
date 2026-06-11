# Holiday and Special-Day Scheduling

## Current special-occasion flow

People can already submit special-leave requests. When an admin approves one, Shifter creates an `AtHome` presence window. If that leave overlaps the person's published assignment, Shifter queues a regeneration run against the affected group so the admin gets a minimal-change draft instead of changing the published schedule in place.

## Direction

Holiday support should be based on concrete reviewed dates, not sensitive organization labels. A country, calendar template, or workspace template can suggest dates later, but scheduling should consume `SpaceSpecialDay` records:

- `Holiday`: official or religious holidays.
- `Weekend`: recurring rest days that matter for home-leave fairness.
- `Custom`: local events such as weddings, memorial days, company events, or base-specific dates.

Each special day has a home-leave weight multiplier and a coverage flag. This lets admins say “this date matters more for going home” while still keeping mission coverage first.

## Signup and privacy

Do not ask users to classify themselves as army, IDF, restaurant, chain, or another sensitive customer type during signup. Optional country/calendar choices can be added as defaults because they help generate suggested dates, but they should not decide customer segmentation or portability by themselves.

## Staged implementation

1. Store and emit reviewed special days in the solver payload.
2. Add admin UI to review/add/remove special days.
3. Add optional country/calendar defaults during space setup or settings.
4. Teach the solver to prefer home leave that overlaps higher-weight special days while preserving coverage and hard constraints.
5. Add calendar generators only after choosing a maintained holiday data source.
