# Implementation Plan: Qualification Templates & Unavailability Reasons

## Overview

This plan implements two extensions to the group template system: (1) seeding qualifications from templates via the existing CRUD endpoints, and (2) a new `UnavailabilityReason` entity with full CRUD, template seeding, and integration into the presence window workflow. The backend is C# (.NET), the frontend is TypeScript (Next.js).

## Tasks

- [x] 1. Database & Domain Layer
  - [x] 1.1 Create UnavailabilityReason domain entity
    - Create `Jobuler.Domain/Spaces/UnavailabilityReason.cs`
    - Implement `AuditableEntity, ITenantScoped` with properties: `SpaceId`, `DisplayName` (max 100), `SortOrder`, `IsActive`
    - Add `Create(spaceId, displayName, sortOrder)` factory, `Update(displayName, sortOrder)`, and `Deactivate()` methods
    - _Requirements: 4.1, 4.4_

  - [x] 1.2 Add UnavailabilityReasonId to PresenceWindow entity
    - In `Jobuler.Domain/People/PresenceWindow.cs`, add nullable `Guid? UnavailabilityReasonId` property
    - Modify the `CreateManual` factory method to accept an optional `Guid? unavailabilityReasonId` parameter
    - _Requirements: 7.1, 7.3_

  - [x] 1.3 Create EF Core migration for unavailability_reasons table and PresenceWindow FK
    - Create EF Core configuration `UnavailabilityReasonConfiguration.cs` in Infrastructure (table `unavailability_reasons`, index on `SpaceId + IsActive`)
    - Update `PresenceWindowConfiguration.cs` to add the optional FK with `OnDelete(DeleteBehavior.SetNull)`
    - Add `DbSet<UnavailabilityReason>` to `AppDbContext`
    - Generate and apply EF Core migration
    - _Requirements: 4.1, 4.5, 7.1_

- [x] 2. Application Layer — Unavailability Reason Commands & Queries
  - [x] 2.1 Create GetUnavailabilityReasonsQuery
    - Create `Jobuler.Application/Spaces/Queries/GetUnavailabilityReasonsQuery.cs`
    - Query filters by `SpaceId` and `IsActive == true`, ordered by `SortOrder`
    - Return list of DTOs with `Id`, `DisplayName`, `SortOrder`
    - _Requirements: 4.3, 4.5_

  - [x] 2.2 Create CreateUnavailabilityReasonCommand
    - Create `Jobuler.Application/Spaces/Commands/CreateUnavailabilityReasonCommand.cs`
    - Validate: `DisplayName` max 100 chars, space has fewer than 50 active reasons
    - Create entity via `UnavailabilityReason.Create(...)` and persist
    - _Requirements: 4.1, 4.2, 4.4_

  - [x] 2.3 Create UpdateUnavailabilityReasonCommand
    - Create `Jobuler.Application/Spaces/Commands/UpdateUnavailabilityReasonCommand.cs`
    - Validate reason exists in space, update `DisplayName` and `SortOrder`
    - _Requirements: 4.4_

  - [x] 2.4 Create DeactivateUnavailabilityReasonCommand
    - Create `Jobuler.Application/Spaces/Commands/DeactivateUnavailabilityReasonCommand.cs`
    - Validate reason exists in space, call `Deactivate()` (soft delete)
    - _Requirements: 4.4_

  - [x] 2.5 Create SeedUnavailabilityReasonsCommand
    - Create `Jobuler.Application/Spaces/Commands/SeedUnavailabilityReasonsCommand.cs`
    - Accept a list of reason display names
    - Check if space already has any reasons — if yes, no-op
    - If space has zero reasons, bulk-create all provided entries with sequential sort order
    - _Requirements: 5.1, 5.2, 5.3_

  - [x] 2.6 Extend AddPresenceWindowCommand to accept optional ReasonId
    - Modify existing `AddPresenceWindowCommand` to include `Guid? ReasonId`
    - In handler: if `ReasonId` is provided, validate it exists in the space and is active
    - If invalid, throw `KeyNotFoundException`
    - Pass `ReasonId` to `PresenceWindow.CreateManual(...)`
    - _Requirements: 7.1, 7.4_

- [x] 3. API Layer — UnavailabilityReasonsController
  - [x] 3.1 Create UnavailabilityReasonsController with CRUD endpoints
    - Create `Jobuler.Api/Controllers/UnavailabilityReasonsController.cs`
    - Route: `spaces/{spaceId:guid}/unavailability-reasons`
    - Endpoints: `GET /` (list), `POST /` (create), `PUT /{reasonId}` (update), `DELETE /{reasonId}` (deactivate), `POST /seed` (seed from template)
    - All endpoints require `[Authorize]` and permission checks via `IPermissionService`
    - _Requirements: 4.4, 5.2_

  - [x] 3.2 Extend AvailabilityController to accept ReasonId in presence requests
    - Modify `AddPresenceRequest` record to include `Guid? ReasonId`
    - Pass `ReasonId` through to `AddPresenceWindowCommand`
    - Update FluentValidation: if `ReasonId` is provided, it must be a valid GUID
    - _Requirements: 7.1, 7.2, 7.4_

  - [x] 3.3 Extend presence window GET response to include reason data
    - Update the presence window DTO/response to include `ReasonId` and `ReasonDisplayName`
    - When returning presence windows, join with `UnavailabilityReason` to include display name (or null if no reason)
    - _Requirements: 7.2_

- [x] 4. Checkpoint — Backend complete
  - Ensure all backend code compiles and existing tests pass
  - Verify the new migration applies cleanly
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Frontend — Template Data Extension
  - [x] 5.1 Extend GroupTemplate interface and add qualification/reason data
    - In `apps/web/lib/utils/groupTemplates.ts`:
    - Add `qualifications: Array<{ name: string; description?: string }>` to `GroupTemplate` interface
    - Add `unavailabilityReasons: string[]` to `GroupTemplate` interface
    - Populate Army template: `["Combat Medic", "Radio Operator", "Driver", "Commander", "Sharpshooter"]`
    - Populate Restaurant template: `["Bartender", "Waiter", "Cook", "Shift Manager", "Barista"]`
    - Populate Hospital template: `["Nurse", "Doctor", "Paramedic", "Lab Technician", "Receptionist"]`
    - Populate Security template: `["Armed Guard", "CCTV Operator", "Patrol", "Shift Supervisor", "First Aid Certified"]`
    - Populate Custom template: empty arrays for both
    - All templates get `unavailabilityReasons: ["חופשה", "מחלה", "אישי", "לימודים"]` (except Custom which gets `[]`)
    - _Requirements: 1.1, 2.1, 2.2, 2.3, 2.4, 1.5, 5.4_

  - [x] 5.2 Update GroupTemplatePicker to create qualifications on template apply
    - In the template application flow (where tasks/constraints are created):
    - After existing template application logic, iterate over `template.qualifications`
    - For each qualification, call `POST /spaces/{spaceId}/groups/{groupId}/qualifications` with `{ name, description }`
    - Skip any that return 409 (already exists) — log and continue
    - _Requirements: 1.2, 1.3, 1.4_

  - [x] 5.3 Update GroupTemplatePicker to seed unavailability reasons on template apply
    - After qualification creation, call `POST /spaces/{spaceId}/unavailability-reasons/seed` with the template's reason list
    - The backend handles the "already has reasons" check — frontend just sends the request
    - _Requirements: 5.2, 5.3_

- [x] 6. Frontend — Unavailability Reason Settings Panel
  - [x] 6.1 Create API client functions for unavailability reasons
    - Create `apps/web/lib/api/unavailabilityReasons.ts`
    - Functions: `getReasons(spaceId)`, `createReason(spaceId, data)`, `updateReason(spaceId, reasonId, data)`, `deleteReason(spaceId, reasonId)`, `seedReasons(spaceId, reasons)`
    - _Requirements: 4.4_

  - [x] 6.2 Create Unavailability Reasons settings panel component
    - Create a settings panel (likely under space settings) for managing unavailability reasons
    - Display list of active reasons sorted by `sortOrder`
    - Allow add (with display name input, max 100 chars), edit (inline rename), reorder (drag or arrows), and deactivate (delete button)
    - Show count indicator (X/50)
    - _Requirements: 4.1, 4.2, 4.4_

- [x] 7. Frontend — Unavailability Form Update
  - [x] 7.1 Update unavailability form to show reason picker
    - In the unavailability/presence form (where admin marks someone as unavailable):
    - Fetch reasons via `GET /spaces/{spaceId}/unavailability-reasons`
    - Display a dropdown/select with predefined reasons + "Custom" option at the end
    - When "Custom" is selected, show a free-text input (max 200 chars)
    - _Requirements: 6.1, 6.2, 6.3_

  - [x] 7.2 Wire reason selection to presence window creation
    - When submitting the unavailability form:
    - If a predefined reason is selected, send `reasonId` in the POST body
    - If "Custom" is selected, send `customReason` text (maps to existing `Note` field)
    - If no reason selected, send neither (backward compatible)
    - _Requirements: 6.3, 6.4, 7.1, 7.3_

  - [x] 7.3 Display reason in presence window views
    - Where presence windows are displayed (calendar, list views):
    - Show the reason display name (from predefined) or the custom note text
    - Handle null reason gracefully (existing windows without reasons)
    - _Requirements: 7.2_

- [x] 8. Solver Compatibility Verification
  - [x] 8.1 Verify SolverPayloadNormalizer ignores UnavailabilityReasonId
    - Confirm that `SolverPayloadNormalizer` does not include `UnavailabilityReasonId` in the solver payload DTO
    - If the normalizer maps `PresenceWindow` to a DTO, ensure the new field is not mapped
    - Add a unit test: two presence windows identical except for `UnavailabilityReasonId` produce identical solver DTOs
    - _Requirements: 8.1, 8.2, 8.3_

- [x] 9. Checkpoint — Full integration
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 10. Property-Based Tests (Optional)
  - [ ]* 10.1 Write property test for template qualification creation
    - **Property 1: Template qualification creation preserves all entries with correct context**
    - **Validates: Requirements 1.2, 1.3**

  - [ ]* 10.2 Write property test for duplicate qualification skipping
    - **Property 2: Template application skips duplicate qualification names**
    - **Validates: Requirements 1.4**

  - [ ]* 10.3 Write property test for unavailability reason tenant isolation
    - **Property 3: Unavailability reason tenant isolation**
    - **Validates: Requirements 4.5**

  - [ ]* 10.4 Write property test for seed idempotence
    - **Property 4: Unavailability reason seed idempotence**
    - **Validates: Requirements 5.2, 5.3**

  - [ ]* 10.5 Write property test for presence window reason round-trip
    - **Property 5: Presence window reason round-trip**
    - **Validates: Requirements 6.3, 6.4, 7.1, 7.2**

  - [ ]* 10.6 Write property test for invalid reason id rejection
    - **Property 6: Invalid reason id rejection**
    - **Validates: Requirements 7.4**

  - [ ]* 10.7 Write property test for solver normalizer reason-invariance
    - **Property 7: Solver normalizer reason-invariance**
    - **Validates: Requirements 8.1, 8.2, 8.3**

- [x] 11. Final Checkpoint
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- The qualification template part (tasks 5.1–5.2) is frontend-only — it calls existing backend CRUD endpoints
- The unavailability reasons part (tasks 1–3) requires new backend infrastructure
- Property tests use FsCheck with xUnit (existing test project pattern)
- Solver compatibility (task 8) is critical — the solver must never see reason data

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3"] },
    { "id": 2, "tasks": ["2.1", "2.2", "2.3", "2.4", "2.5", "2.6"] },
    { "id": 3, "tasks": ["3.1", "3.2", "3.3"] },
    { "id": 4, "tasks": ["5.1", "6.1", "8.1"] },
    { "id": 5, "tasks": ["5.2", "5.3", "6.2"] },
    { "id": 6, "tasks": ["7.1"] },
    { "id": 7, "tasks": ["7.2", "7.3"] },
    { "id": 8, "tasks": ["10.1", "10.2", "10.3", "10.4", "10.5", "10.6", "10.7"] }
  ]
}
```
