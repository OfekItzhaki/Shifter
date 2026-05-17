# Implementation Plan: Cross-Group Conflict Detection

## Overview

Implement post-facto cross-group conflict detection that identifies overlapping assignments and insufficient rest gaps for users belonging to multiple groups. The system hooks into the existing publish and login flows via fire-and-forget `Task.Run`, uses a dedicated `ConflictDetectionDbContext` (no RLS) for cross-space queries, and produces personal notifications through the existing notification + push infrastructure. No new API endpoints, no solver changes, no frontend changes.

## Tasks

- [x] 1. Database migration and Notification entity extension
  - [x] 1.1 Add `deduplication_hash` column to notifications table
    - Create EF Core migration adding nullable `deduplication_hash VARCHAR(64)` column to `notifications` table
    - Add partial index: `ix_notifications_dedup` on `(user_id, space_id, event_type, deduplication_hash) WHERE is_read = FALSE`
    - _Requirements: 8.1, 8.2_
  - [x] 1.2 Extend `Notification` domain entity with `DeduplicationHash`
    - Add `public string? DeduplicationHash { get; private set; }` property
    - Add `CreateWithDedup` factory method accepting `deduplicationHash` parameter
    - Update EF Core configuration to map the new column
    - _Requirements: 8.1_

- [x] 2. Domain layer: ConflictDetector pure logic
  - [x] 2.1 Create domain models for conflict detection
    - Create `Jobuler.Domain/Conflicts/FlatAssignment.cs` record
    - Create `Jobuler.Domain/Conflicts/ConflictPair.cs` record
    - Create `Jobuler.Domain/Conflicts/ConflictResult.cs` record
    - Create `Jobuler.Domain/Conflicts/ConflictType.cs` enum (Overlap, RestViolation)
    - _Requirements: 3.1, 4.1_
  - [x] 2.2 Implement `ConflictDetector` static class with sort-then-sweep algorithm
    - Create `Jobuler.Domain/Conflicts/ConflictDetector.cs`
    - Implement `Detect(IReadOnlyList<FlatAssignment> assignments, Func<Guid, Guid, int> getMinRestHours)` method
    - Sort assignments by StartsAt, sweep with active set comparing only cross-group pairs
    - Classify overlaps (A.StartsAt < B.EndsAt AND B.StartsAt < A.EndsAt)
    - Classify rest violations (gap < max(restA, restB) hours, only when max > 0, only for non-overlapping pairs)
    - _Requirements: 3.1, 3.2, 3.3, 4.1, 4.2, 4.3, 4.4_
  - [ ]* 2.3 Write property test: Overlap detection is symmetric and complete
    - **Property 1: Overlap detection is symmetric and complete**
    - Generate random FlatAssignment sets, verify overlap classification matches interval intersection formula
    - **Validates: Requirements 1.2, 3.1**
  - [ ]* 2.4 Write property test: Rest violation uses the stricter threshold
    - **Property 2: Rest violation uses the stricter threshold**
    - Generate non-overlapping assignment pairs with varying MinRestBetweenShiftsHours, verify max() is used
    - **Validates: Requirements 4.1, 4.2, 4.4**
  - [ ]* 2.5 Write property test: Overlap and rest violation are mutually exclusive
    - **Property 3: Overlap and rest violation are mutually exclusive**
    - Generate assignment pairs, verify no pair is classified as both Overlap and RestViolation
    - **Validates: Requirements 4.3**
  - [ ]* 2.6 Write property test: Same-group assignments never produce conflicts
    - **Property 4: Same-group assignments never produce conflicts**
    - Generate assignments all sharing the same GroupId, verify zero conflicts returned
    - **Validates: Requirements 3.2**

- [x] 3. Checkpoint - Domain logic verification
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Application layer: IConflictDetectionService interface
  - [x] 4.1 Create `IConflictDetectionService` interface
    - Create `Jobuler.Application/Conflicts/IConflictDetectionService.cs`
    - Define `DetectOnPublishAsync(Guid spaceId, Guid versionId, CancellationToken ct)` method
    - Define `DetectOnLoginAsync(Guid userId, CancellationToken ct)` method
    - _Requirements: 1.1, 2.1_

- [x] 5. Infrastructure layer: ConflictDetectionDbContext and service implementation
  - [x] 5.1 Create `ConflictDetectionDbContext` without RLS interceptor
    - Create `Jobuler.Infrastructure/Persistence/ConflictDetectionDbContext.cs`
    - Register with direct connection string (no RLS session variable interceptor)
    - Expose DbSets for: assignments, task_slots, people, group_memberships, groups, schedule_versions, notifications, spaces, push_subscriptions, tasks (GroupTask)
    - Read-only for cross-space queries, write-only for notifications
    - _Requirements: 6.1, 6.4, 7.5_
  - [x] 5.2 Implement `ConflictDetectionService` — publish trigger path
    - Create `Jobuler.Infrastructure/Conflicts/ConflictDetectionService.cs` implementing `IConflictDetectionService`
    - Implement `DetectOnPublishAsync`: identify persons with assignments in the published version, resolve LinkedUserId across spaces, load all published assignments per person, call `ConflictDetector.Detect()`, compute dedup hash, check for existing unread notifications, create notifications with localized text, send push (best-effort)
    - Skip persons without LinkedUserId (Req 1.5)
    - Only compare assignments from published versions (Req 3.3)
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 3.3, 5.1–5.8, 6.1–6.5, 8.1–8.5_
  - [x] 5.3 Implement `ConflictDetectionService` — login trigger path
    - Implement `DetectOnLoginAsync`: find all Person records via LinkedUserId, filter to future assignments only, call `ConflictDetector.Detect()`, create notifications per space
    - Skip if no Person records found (Req 2.2)
    - _Requirements: 2.1, 2.2, 2.3, 2.4_
  - [x] 5.4 Implement deduplication fingerprint computation
    - Implement SHA-256 hash of sorted assignment pair IDs (min:max format, pipe-separated)
    - Check existing unread notifications with same fingerprint before creating new ones
    - Allow re-notification if previous notification was marked as read
    - _Requirements: 8.1, 8.2, 8.3, 8.4_
  - [x] 5.5 Implement localized notification text helper
    - Create localization helper returning title/body for he/en/ru locales
    - he: "התנגשות שיבוצים" / "יש לך חפיפה בין שיבוצים — עדכן את המנהל"
    - en: "Schedule Conflict" / "You have overlapping assignments — notify your manager"
    - ru: "Конфликт смен" / "У вас пересечение смен — сообщите менеджеру"
    - Default to "en" for unknown locales
    - _Requirements: 5.2, 5.3, 5.4, 5.5_
  - [ ]* 5.6 Write property test: Deduplication fingerprint is order-independent
    - **Property 5: Deduplication fingerprint is order-independent**
    - Generate conflict pair sets in random order, verify hash is identical regardless of input order
    - **Validates: Requirements 8.1**

- [x] 6. Checkpoint - Infrastructure verification
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 7. Integration: Hook into existing command handlers and DI registration
  - [x] 7.1 Hook into `PublishVersionCommandHandler` (fire-and-forget)
    - Add `Task.Run` block after audit log in `PublishVersionCommandHandler.Handle()`
    - Create new DI scope, resolve `IConflictDetectionService`, call `DetectOnPublishAsync`
    - Wrap in try/catch logging errors without affecting publish response
    - _Requirements: 1.1, 1.6, 7.1, 7.4_
  - [x] 7.2 Hook into `LoginCommandHandler` (fire-and-forget)
    - Add `IServiceScopeFactory` dependency to `LoginCommandHandler`
    - Add `Task.Run` block after `SaveChangesAsync` in `LoginCommandHandler.Handle()`
    - Create new DI scope, resolve `IConflictDetectionService`, call `DetectOnLoginAsync`
    - Wrap in try/catch logging errors without affecting login response time
    - _Requirements: 2.1, 2.5, 7.4_
  - [x] 7.3 Register DI services in `Program.cs` or DI extension
    - Register `ConflictDetectionDbContext` with direct connection string (no RLS interceptor)
    - Register `IConflictDetectionService` → `ConflictDetectionService` as scoped
    - _Requirements: 1.1, 2.1_
  - [ ]* 7.4 Write unit tests for integration points
    - Test that `PublishVersionCommandHandler` completes without error when conflict service is registered
    - Test that `LoginCommandHandler` completes without error when conflict service is registered
    - Test error in conflict detection does not affect publish/login response
    - _Requirements: 7.4_

- [x] 8. Step documentation
  - [x] 8.1 Create `docs/steps/284-cross-group-conflict-detection.md`
    - Document the feature: title, phase, purpose, what was built, key decisions, how it connects, how to verify, what comes next
    - Include git commit command
    - _Requirements: all_

- [x] 9. Final checkpoint - Full integration verification
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The `ConflictDetectionDbContext` is the only component that bypasses RLS — it is explicitly scoped and auditable
- No new API endpoints are needed — notifications flow through the existing `NotificationsController`
- No frontend changes — the existing notification bell and push infrastructure handle display
- The fire-and-forget pattern matches the existing `SendExternalNotificationsAsync` pattern in `PublishVersionCommandHandler`

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1"] },
    { "id": 1, "tasks": ["1.2", "2.2"] },
    { "id": 2, "tasks": ["2.3", "2.4", "2.5", "2.6", "4.1"] },
    { "id": 3, "tasks": ["5.1", "5.5"] },
    { "id": 4, "tasks": ["5.2", "5.3", "5.4"] },
    { "id": 5, "tasks": ["5.6", "7.1", "7.2", "7.3"] },
    { "id": 6, "tasks": ["7.4", "8.1"] }
  ]
}
```
