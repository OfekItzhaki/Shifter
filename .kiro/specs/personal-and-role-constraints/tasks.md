# Implementation Plan: Personal and Role Constraints

## Overview

This plan closes the gap between the existing backend domain (which already supports `ScopeType.Person` and `ScopeType.Role`) and the frontend UI. The work is almost entirely frontend — fixing a filter bug in `page.tsx`, loading roles when the constraints tab activates, adding a delete confirmation dialog, and showing read-only scope labels in the edit modal. The backend `ExceptionHandlingMiddleware` already maps `DomainValidationException` to HTTP 422, so no backend changes are needed there.

## Tasks

- [x] 1. Fix the `handleCreateConstraint` filter bug in `page.tsx`
  - In `handleCreateConstraint`, change `setConstraints(updated.filter(c => c.scopeId === groupId))` to `setConstraints(updated)` so personal and role constraints are not stripped from state after creation via the legacy modal path.
  - _Requirements: 1.1, 2.9, 3.9_

- [x] 2. Load `groupRoles` when the constraints tab activates
  - Extend the constraints `useEffect` in `page.tsx` to also call `getGroupRoles(currentSpaceId, groupId)` when `groupRoles.length === 0`, using `Promise.all` alongside `getConstraints`.
  - This ensures the role selector in `SectionCreateForm` and the `roleMap` in `ConstraintsTab` are populated before the user interacts with the tab.
  - _Requirements: 1.7, 3.3_

- [x] 3. Add delete confirmation dialog to `ConstraintRow`
  - Add a two-step confirmation state inside `ConstraintRow`: first click shows a Hebrew confirmation prompt ("האם למחוק אילוץ זה?") with Confirm/Cancel buttons; second click calls `onDeleteConstraint`.
  - _Requirements: 5.2_

- [x] 4. Show read-only scope label in the edit modal
  - In the edit modal inside `ConstraintsTab`, add a read-only display row for `scope_type` and `scope_id` when the constraint is a personal or role constraint.
  - Use `roleMap` and `memberMap` (already built in the component) to resolve the human-readable name.
  - Display as a static `<p>` or `<span>` — not an editable field.
  - _Requirements: 4.2, 4.3_

- [x] 5. Isolate per-section create errors in `ConstraintsTab`
  - Replace the shared `sectionSaving` / `sectionError` state in `handleSectionCreate` with per-section state managed inside each `SectionCreateForm` instance.
  - `SectionCreateForm` already manages its own `saving`/`error` props internally — remove the shared state from the parent and pass the error from the API response (reading `error.response?.data?.error` with a Hebrew fallback) into the form's own error display.
  - _Requirements: 2.8, 3.8_

- [x] 6. Wire API error messages through `onCreateWithScope` in `page.tsx`
  - Update the `onCreateWithScope` callback in `page.tsx` to extract the error message from the Axios response (`error.response?.data?.error`) and re-throw with that message so `SectionCreateForm` can display it.
  - _Requirements: 2.8, 3.8, 6.1, 6.2, 6.3, 6.4, 8.2, 8.4_

- [x] 7. Checkpoint — verify all three sections render correctly
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Write backend unit tests for `CreateConstraintCommandHandler` scope validation
  - [x] 8.1 Test: person scope with `linked_user_id = null` → throws `DomainValidationException`
    - _Requirements: 8.2, 8.3_
  - [x] 8.2 Test: person scope with `invitation_status = "pending"` → throws `DomainValidationException`
    - _Requirements: 8.4_
  - [x] 8.3 Test: person scope with non-existent person → throws `KeyNotFoundException`
    - _Requirements: 6.1, 6.2_
  - [x] 8.4 Test: role scope with inactive role → throws `KeyNotFoundException`
    - _Requirements: 6.3, 6.4_
  - [x] 8.5 Test: role scope with non-existent role → throws `KeyNotFoundException`
    - _Requirements: 6.3, 6.4_
  - [x] 8.6 Test: group scope → succeeds without person/role checks
    - _Requirements: 6.6_
  - [ ]* 8.7 Write property test for Property 7: unregistered person always rejected
    - **Property 7: Unregistered person is rejected with HTTP 422**
    - **Validates: Requirements 8.2, 8.4**
    - `// Feature: personal-and-role-constraints, Property 7: unregistered person rejected with 422`
  - [ ]* 8.8 Write property test for Property 8: non-existent or inactive role always rejected
    - **Property 8: Non-existent or inactive role is rejected with HTTP 404**
    - **Validates: Requirements 3.7, 6.3, 6.4**
    - `// Feature: personal-and-role-constraints, Property 8: non-existent or inactive role rejected with 404`
  - [ ]* 8.9 Write property test for Property 9: non-existent person always rejected
    - **Property 9: Non-existent person is rejected with HTTP 404**
    - **Validates: Requirements 2.7, 6.1, 6.2**
    - `// Feature: personal-and-role-constraints, Property 9: non-existent person rejected with 404`

- [x] 9. Write frontend logic tests for constraint filtering and name resolution
  - [x] 9.1 Test: person selector contains only registered members (Property 4)
    - **Property 4: Person selector contains only registered members**
    - **Validates: Requirements 2.3, 8.1**
    - `// Feature: personal-and-role-constraints, Property 4: person selector contains only registered members`
  - [x] 9.2 Test: role selector contains only active roles (Property 5)
    - **Property 5: Role selector contains only active roles**
    - **Validates: Requirements 3.3**
    - `// Feature: personal-and-role-constraints, Property 5: role selector contains only active roles`
  - [x] 9.3 Test: person name resolution uses displayName with fullName fallback (Property 3)
    - **Property 3: Person name resolution uses displayName with fullName fallback**
    - **Validates: Requirements 1.6**
    - `// Feature: personal-and-role-constraints, Property 3: person name resolution uses displayName with fullName fallback`
  - [x] 9.4 Test: constraints partitioned correctly by scopeType
    - Verify group/person/role partitioning logic
    - _Requirements: 1.1_

- [x] 10. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- The backend `ExceptionHandlingMiddleware` already maps `DomainValidationException` → HTTP 422 — no backend change needed
- The `CreateConstraintCommandHandler` already contains the full person/role validation chain — no new commands needed
- Each task references specific requirements for traceability
- Property tests validate universal correctness properties across many inputs
