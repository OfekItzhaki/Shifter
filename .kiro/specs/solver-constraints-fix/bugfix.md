# Bugfix Requirements Document

## Introduction

שלושה באגים קשורים במנוע התזמון (solver) עבור קבוצות בסיס סגור (closed-base groups):

1. **הפרת אילוץ מנוחה מינימלית** — חייל מקבל שתי משמרות עם פער של שעתיים בלבד (03:00–05:00) במקום 8 שעות מנוחה מינימלית, למרות שהאילוץ אמור להיות HARD לחלוטין בקבוצות בסיס סגור.
2. **חוסר איזון בחופשות בית** — 11 מתוך ~20 אנשים בבית בו-זמנית, בעוד רק 4 במשימה ו-5 פנויים בבסיס. הסולבר מתייחס לשליחה הביתה כחשובה באותה מידה ככיסוי משימות (משקל 1000 = 1000).
3. **חוסר התראת אי-אפשרות** — כאשר האילוצים לא ניתנים לסיפוק (אנשים לא מספיקים, הפרות מנוחה), הסולבר מייצר טיוטה שבורה במקום להודיע למנהל שהסידור בלתי אפשרי.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a closed-base group has home_leave_config enabled AND two non-long shifts (< 24h each) at different locations have a gap smaller than min_rest_hours THEN the system assigns the same person to both shifts, violating the hard rest constraint

1.2 WHEN a closed-base group has home_leave_config.min_rest_hours set to 0 (meaning "use default 8h") AND the group has no explicit "min_rest_hours" hard constraint configured THEN the system may apply an incorrect rest threshold because the fallback logic depends on a hard constraint rule that doesn't exist for the group

1.3 WHEN the home-leave eligibility preference weight is calculated with the default balance_value of 50 THEN the system produces ELIGIBILITY_WEIGHT = 1000 (50 × 20), which equals the coverage_weight (1000), causing the solver to treat sending people home as equally important as covering missions

1.4 WHEN the solver produces a feasible result but with rest constraint violations visible in the output (person assigned to shifts with insufficient gap) THEN the system creates a draft version and presents it to the admin as a valid schedule

1.5 WHEN more than half the group members are assigned to home-leave simultaneously THEN the system does not enforce a meaningful upper bound relative to mission coverage needs, resulting in understaffed missions

### Expected Behavior (Correct)

2.1 WHEN a closed-base group has home_leave_config enabled AND two non-long shifts have a gap smaller than min_rest_hours THEN the system SHALL enforce a hard constraint preventing the same person from being assigned to both shifts, regardless of shift location or task type

2.2 WHEN a closed-base group has home_leave_config.min_rest_hours set to 0 THEN the system SHALL fall back to the group's configured min_rest_hours hard constraint value, and if none exists, SHALL use the default of 8 hours — ensuring the rest constraint is always applied correctly

2.3 WHEN the home-leave eligibility preference weight is calculated for NEW home-leave assignments THEN the system SHALL use a weight that is strictly lower than the coverage_weight (1000), ensuring mission coverage always takes priority over sending people home — EXCEPT when a person is already on home-leave (presence_window state = "at_home") and no emergency freeze has recalled them, in which case the existing home-leave assignment SHALL be preserved with highest priority

2.4 WHEN the solver produces a result that violates any hard constraint — including min_rest_hours violations, minimum headcount requirements from group settings, qualification/role mismatches, or availability conflicts THEN the system SHALL treat this as an infeasible result, NOT create a draft version, and SHALL notify the admin with a detailed explanation of which constraints were violated and actionable guidance

2.5 WHEN the number of people simultaneously on home-leave would cause mission slots to be uncovered or understaffed THEN the system SHALL prioritize mission coverage over home-leave assignments, reducing the number of concurrent home-leave slots to maintain adequate staffing

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a closed-base group has home_leave_config enabled AND shifts are long (≥ 24h) AND soft_penalties is not None THEN the system SHALL CONTINUE TO apply rest as a soft constraint with penalty for those long shifts (this path is only active for non-closed-base groups)

3.2 WHEN a non-closed-base group (home_leave_config disabled or absent) has min_rest_hours configured THEN the system SHALL CONTINUE TO apply rest constraints with the existing soft/hard logic based on long_shift_threshold

3.3 WHEN the solver finds a fully feasible solution with all shifts covered and no rest violations THEN the system SHALL CONTINUE TO create a draft version and notify the admin as before

3.4 WHEN a person has accumulated enough base time to meet the eligibility threshold THEN the system SHALL CONTINUE TO prefer sending them home (soft preference), just at a lower priority than mission coverage

3.5 WHEN emergency bypass constraints are active for a person or slot THEN the system SHALL CONTINUE TO skip rest, availability, and overlap constraints for those bypassed entities

3.6 WHEN the solver times out but produces partial valid assignments with no hard constraint violations THEN the system SHALL CONTINUE TO create a draft version with the partial result and mark the run as timed_out

3.7 WHEN a person is currently on home-leave (presence_window state = "at_home") AND no emergency freeze has been activated to recall them THEN the system SHALL CONTINUE TO treat their home-leave as the highest priority assignment that cannot be overridden by new mission coverage needs
