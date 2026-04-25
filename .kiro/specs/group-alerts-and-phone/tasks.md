# Implementation Plan: Group Alerts and Phone

## Overview

Three coordinated additions: (1) expose phone numbers in `GroupMemberDto`, (2) add a Group Alerts tab with admin broadcast capability, and (3) implement a Forgot Password flow backed by `INotificationSender`. Each section builds on the previous — domain entities and migrations first, then application layer, then API endpoints, then frontend.

## Tasks

- [x] 1. Phone number in GroupMemberDto — backend
  - [x] 1.1 Add `PhoneNumber` to `GroupMemberDto` record and update `GetGroupMembersQueryHandler` projection
    - In `apps/api/Jobuler.Application/Groups/Queries/GetGroupsQuery.cs`, add `string? PhoneNumber` as the fifth parameter to `GroupMemberDto`
    - In `GetGroupMembersQueryHandler.Handle`, extend the `.Select` projection to include `p.PhoneNumber` — the `phone_number` column already exists from migration 010, no migration needed
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [x] 1.2 Write property test for phone number DTO fidelity
    - **Property 1: Phone number in DTO matches people table**
    - **Validates: Requirements 1.1, 1.2, 1.3**
    - Use FsCheck; generate random persons with null and non-null `PhoneNumber`; assert returned `GroupMemberDto.PhoneNumber` equals the seeded value

- [x] 2. Phone number in GroupMemberDto — frontend
  - [x] 2.1 Update `GroupMemberDto` TypeScript interface and member list UI
    - In `apps/web/lib/api/groups.ts`, add `phoneNumber: string | null` to the `GroupMemberDto` interface
    - In `apps/web/app/groups/[groupId]/page.tsx`, render `m.phoneNumber` as plain text after the display name in both read-only and admin-edit member rows; when `null`, render nothing (no "null" or "undefined" string)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 2.2 Write property test for phone number rendering
    - **Property 2: Phone number renders correctly for all members**
    - **Validates: Requirements 2.1, 2.2, 2.5**
    - Use fast-check; generate random `GroupMemberDto[]` arrays with mixed null/non-null `phoneNumber`; assert rendered output never contains the strings `"null"` or `"undefined"`

- [x] 3. Checkpoint — phone number
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Group Alerts — migrations and domain entity
  - [x] 4.1 Create migration 012 for `group_alerts` table
    - Create `infra/migrations/012_group_alerts.sql` with the SQL from the design doc: `group_alerts` table with `id`, `space_id`, `group_id`, `title`, `body`, `severity`, `created_at`, `created_by_person_id` columns; FK to `groups.id` and `people.id`; composite index on `(space_id, group_id, created_at DESC)`
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [x] 4.2 Create `GroupAlert` domain entity and `AlertSeverity` enum
    - Create `apps/api/Jobuler.Domain/Groups/GroupAlert.cs` implementing `ITenantScoped` with properties `Id`, `SpaceId`, `GroupId`, `Title`, `Body`, `Severity`, `CreatedAt`, `CreatedByPersonId`
    - Add static factory `GroupAlert.Create(spaceId, groupId, title, body, severity, createdByPersonId)` setting `CreatedAt = DateTime.UtcNow`
    - Add `AlertSeverity` enum (`Info`, `Warning`, `Critical`) in the same file
    - No data annotations — use Fluent API in Infrastructure
    - _Requirements: 3.5, 3.6_

  - [x] 4.3 Register `GroupAlert` in `AppDbContext` and add EF Fluent API configuration
    - In `apps/api/Jobuler.Application/Persistence/AppDbContext.cs`, add `public DbSet<GroupAlert> GroupAlerts => Set<GroupAlert>();` under the Groups section
    - Create `apps/api/Jobuler.Infrastructure/Persistence/Configurations/GroupAlertConfiguration.cs` mapping to `group_alerts` table with all column names, FK relationships, and the composite index — follow the pattern in `GroupMessageConfiguration.cs`
    - _Requirements: 3.7_

- [x] 5. Group Alerts — application layer
  - [x] 5.1 Create `GroupAlertDto` and `CreateGroupAlertCommand` with handler and validator
    - Create `apps/api/Jobuler.Application/Groups/Commands/GroupAlertCommands.cs` containing:
      - `GroupAlertDto` record with fields: `Id`, `Title`, `Body`, `Severity`, `CreatedAt`, `CreatedByPersonId`, `CreatedByDisplayName`
      - `CreateGroupAlertCommand(SpaceId, GroupId, RequestingUserId, Title, Body, Severity)` with handler that calls `IPermissionService.RequirePermissionAsync` for `people.manage`, resolves the caller's `Person`, creates and persists a `GroupAlert`, returns the new `Id`
      - `CreateGroupAlertCommandValidator` using FluentValidation: `Title` 1–200 non-blank chars, `Body` 1–2000 non-blank chars, `Severity` must be `info`/`warning`/`critical` (case-insensitive)
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7_

  - [x] 5.2 Write property test for alert creation round-trip
    - **Property 3: Alert creation round-trip**
    - **Validates: Requirements 4.1, 5.1, 5.2**
    - Use FsCheck; generate random valid title/body/severity; assert `GetGroupAlertsQuery` returns an alert matching the inputs exactly

  - [x] 5.3 Write property test for alert creation rejects invalid inputs
    - **Property 4: Alert creation rejects invalid inputs**
    - **Validates: Requirements 4.3, 4.4, 4.5**
    - Use FsCheck; generate blank titles, titles > 200 chars, blank bodies, bodies > 2000 chars, invalid severity strings; assert `CreateGroupAlertCommand` throws and no row is persisted

  - [x] 5.4 Create `GetGroupAlertsQuery` with handler
    - Add `GetGroupAlertsQuery(SpaceId, GroupId, RequestingUserId)` and handler to `GroupAlertCommands.cs` (or a separate `GetGroupAlertsQuery.cs`)
    - Handler verifies caller is a group member via `GroupMemberships` join; throws `UnauthorizedAccessException` if not
    - Returns `List<GroupAlertDto>` ordered by `CreatedAt` descending, joining `People` for `CreatedByDisplayName` (prefer `DisplayName`, fall back to `FullName`)
    - Filters by both `space_id` and `group_id` for tenant isolation
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_

  - [x] 5.5 Write property test for alerts ordered newest-first
    - **Property 5: Alerts are ordered newest-first**
    - **Validates: Requirements 5.1**
    - Use FsCheck; seed random sets of alerts with random `CreatedAt` values; assert returned list is strictly descending by `CreatedAt`

  - [x] 5.6 Write property test for tenant isolation
    - **Property 6: Alerts respect tenant isolation**
    - **Validates: Requirements 5.4**
    - Use FsCheck; seed alerts in two spaces with overlapping `groupId`; assert `GetGroupAlertsQuery` with `spaceId = A` never returns alerts from space B

  - [x] 5.7 Write property test for non-members cannot read alerts
    - **Property 7: Non-members cannot read alerts**
    - **Validates: Requirements 5.3**
    - Use FsCheck; generate random users not in the group; assert `GetGroupAlertsQuery` throws `UnauthorizedAccessException`

  - [x] 5.8 Create `DeleteGroupAlertCommand` with handler
    - Add `DeleteGroupAlertCommand(SpaceId, GroupId, AlertId, RequestingUserId)` and handler to `GroupAlertCommands.cs`
    - Handler calls `IPermissionService.RequirePermissionAsync` for `people.manage`; loads the alert (throws `KeyNotFoundException` if not found); resolves caller's `Person`; throws `UnauthorizedAccessException` if `alert.CreatedByPersonId != callerPerson.Id`; removes the alert
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

  - [x] 5.9 Write property test for alert delete removes own alerts
    - **Property 8: Alert delete removes own alerts**
    - **Validates: Requirements 6.1**
    - Use FsCheck; create alert as person X; call `DeleteGroupAlertCommand` as person X; assert `GetGroupAlertsQuery` no longer includes the alert

  - [x] 5.10 Write property test for cross-owner deletion rejected
    - **Property 9: Alert delete rejects cross-owner deletion**
    - **Validates: Requirements 6.3**
    - Use FsCheck; create alert as person A; call `DeleteGroupAlertCommand` as person B (B ≠ A, both have `people.manage`); assert `UnauthorizedAccessException` is thrown

- [x] 6. Group Alerts — API endpoints
  - [x] 6.1 Add alert endpoints to `GroupsController`
    - In `apps/api/Jobuler.Api/Controllers/GroupsController.cs`, add three endpoints under the `// ── Group Alerts` section:
      - `POST /spaces/{spaceId}/groups/{groupId}/alerts` → `CreateGroupAlertCommand`, returns `CreatedAtAction` with `{ id }`
      - `GET /spaces/{spaceId}/groups/{groupId}/alerts` → `GetGroupAlertsQuery`, returns `Ok(alerts)`
      - `DELETE /spaces/{spaceId}/groups/{groupId}/alerts/{alertId}` → `DeleteGroupAlertCommand`, returns `NoContent()`
    - Add `CreateAlertRequest(string Title, string Body, string Severity)` record at the bottom of the request records section
    - All three endpoints require `[Authorize]` (inherited from controller attribute)
    - _Requirements: 4.8, 5.1, 6.1_

- [x] 7. Group Alerts — frontend tab
  - [x] 7.1 Add `GroupAlertDto` interface and alert API functions to `lib/api/groups.ts`
    - Add `GroupAlertDto` interface with fields: `id`, `title`, `body`, `severity`, `createdAt`, `createdByPersonId`, `createdByDisplayName`
    - Add `getGroupAlerts(spaceId, groupId)`, `createGroupAlert(spaceId, groupId, payload)`, `deleteGroupAlert(spaceId, groupId, alertId)` functions using `apiClient`
    - _Requirements: 7.2, 8.3, 9.3_

  - [x] 7.2 Create severity badge utility
    - Create `apps/web/lib/utils/alertSeverity.ts` exporting `SEVERITY_BADGE` map: `info` → blue (`bg-blue-50 text-blue-700`), `warning` → amber (`bg-amber-50 text-amber-700`), `critical` → red (`bg-red-50 text-red-700`) with Hebrew labels
    - _Requirements: 7.8_

  - [x] 7.3 Write property test for severity badge color
    - **Property 10: Severity badge color is correct for all severity values**
    - **Validates: Requirements 7.8**
    - Use fast-check; enumerate all three severity values; assert correct CSS classes are returned from `SEVERITY_BADGE`

  - [x] 7.4 Add "התראות" tab to `app/groups/[groupId]/page.tsx`
    - Add `"alerts"` to the `ActiveTab` union type
    - Add a "התראות" tab button visible to ALL members (not gated by `adminGroupId`)
    - Add alerts state: `alerts`, `alertsLoading`, `alertsError`, `newAlertTitle`, `newAlertBody`, `newAlertSeverity`, `alertSubmitting`
    - When the "התראות" tab becomes active, fetch `getGroupAlerts(spaceId, groupId)` and store results
    - Render each alert as a card with severity badge (from `SEVERITY_BADGE`), `title`, `body`, `createdByDisplayName`, and `createdAt` formatted as Hebrew locale date/time
    - Empty state: "אין התראות לקבוצה זו"; error state: Hebrew error message
    - When `adminGroupId === groupId`, render a create-alert form at the top (title input, body textarea, severity `<select>`, submit button disabled while `alertSubmitting`); on success clear form and re-fetch alerts
    - When `adminGroupId === groupId`, render a delete button on each alert where `alert.createdByPersonId === currentPersonId`; on click call `deleteGroupAlert` and re-fetch
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7, 7.8, 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 9.1, 9.2, 9.3, 9.4, 9.5_

  - [x] 7.5 Write property test for delete buttons on own alerts only
    - **Property 11: Delete buttons appear only on own alerts**
    - **Validates: Requirements 9.1, 9.2**\\\
    - Use fast-check; generate random `GroupAlertDto[]` with mixed `createdByPersonId` values; assert delete buttons render only on alerts matching `currentPersonId`

- [x] 8. Checkpoint — Group Alerts
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Forgot Password — domain entity and migration
  - [x] 9.1 Create migration 013 for `password_reset_tokens` table
    - Create `infra/migrations/013_password_reset_tokens.sql` with the SQL from the design doc: `password_reset_tokens` table with `id`, `user_id`, `token_hash`, `created_at`, `expires_at`, `used_at` columns; FK to `users.id`; indexes on `(user_id)` and `(token_hash)`
    - _Requirements: 12.5, 12.6_

  - [x] 9.2 Create `PasswordResetToken` domain entity
    - Create `apps/api/Jobuler.Domain/Identity/PasswordResetToken.cs` with properties `Id`, `UserId`, `TokenHash`, `CreatedAt`, `ExpiresAt`, `UsedAt`
    - Add computed properties `IsExpired`, `IsUsed`, `IsValid`
    - Add static factory `PasswordResetToken.Create(userId, tokenHash)` setting `ExpiresAt = DateTime.UtcNow.AddHours(1)`
    - Add `MarkUsed()` method setting `UsedAt = DateTime.UtcNow`
    - No data annotations
    - _Requirements: 12.2_

  - [x] 9.3 Add `SetPasswordHash` method to `User` domain entity
    - In `apps/api/Jobuler.Domain/Identity/User.cs`, add `public void SetPasswordHash(string hash) { PasswordHash = hash; Touch(); }` — this method does not currently exist and is required by `ResetPasswordCommandHandler`
    - _Requirements: 14.6_

  - [x] 9.4 Register `PasswordResetToken` in `AppDbContext` and add EF configuration
    - In `apps/api/Jobuler.Application/Persistence/AppDbContext.cs`, add `public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();` under the Identity section
    - Create `apps/api/Jobuler.Infrastructure/Persistence/Configurations/PasswordResetTokenConfiguration.cs` mapping to `password_reset_tokens` table with all column names and the unique constraint on `token_hash`
    - _Requirements: 12.5_

- [x] 10. Forgot Password — INotificationSender and NoOpNotificationSender
  - [x] 10.1 Create `INotificationSender` interface
    - Create `apps/api/Jobuler.Application/Common/INotificationSender.cs` with a single method `Task SendPasswordResetAsync(string to, string token, CancellationToken ct = default)`
    - Add XML doc comment explaining it is separate from `IEmailSender` and is for user-facing notifications
    - _Requirements: 13.1_

  - [x] 10.2 Create `NoOpNotificationSender` implementation
    - Create `apps/api/Jobuler.Infrastructure/Notifications/NoOpNotificationSender.cs` implementing `INotificationSender`
    - Log at Warning level: `"[NoOp] Password reset for {To}: token={Token}"` — never log the token at any other level
    - Return `Task.CompletedTask` immediately
    - _Requirements: 13.2, 13.5_

  - [x] 10.3 Register `INotificationSender` in DI
    - In `apps/api/Jobuler.Api/Program.cs`, add `services.AddScoped<INotificationSender, NoOpNotificationSender>();`
    - _Requirements: 13.2, 13.6_

- [x] 11. Forgot Password — application layer commands
  - [x] 11.1 Create `ForgotPasswordCommand` with handler
    - Create `apps/api/Jobuler.Application/Auth/Commands/ForgotPasswordCommand.cs`
    - Handler: look up active user by email (case-insensitive); if not found, return silently (no exception, no token created) to prevent user enumeration
    - Invalidate any existing active token for the user by calling `MarkUsed()` on each
    - Generate raw token via `_jwt.GenerateRefreshTokenRaw()` and store only its SHA-256 hash via `_jwt.HashToken(rawToken)`
    - Create and persist `PasswordResetToken.Create(user.Id, tokenHash)`
    - Deliver via `INotificationSender.SendPasswordResetAsync` — prefer `user.PhoneNumber` if non-null, fall back to `user.Email`
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 13.3, 13.4_

  - [x]* 11.2 Write property test for user enumeration prevention
    - **Property 13: User enumeration prevention**
    - **Validates: Requirements 12.3**
    - Use FsCheck; generate random email strings not matching any user; assert `ForgotPasswordCommand` completes without exception and no `password_reset_tokens` row is created

  - [x]* 11.3 Write property test for at most one active token per user
    - **Property 14: At most one active reset token per user**
    - **Validates: Requirements 12.4**
    - Use FsCheck; call `ForgotPasswordCommand` twice for the same user; assert exactly one row has `used_at IS NULL` and the first token has `used_at` set

  - [x]* 11.4 Write property test for reset token hash integrity
    - **Property 12: Reset token hash is SHA-256 of raw token**
    - **Validates: Requirements 12.2**
    - Use FsCheck; capture the token delivered to `INotificationSender`; assert stored `token_hash == SHA256(rawToken)` and `expires_at` is within 1 second of `created_at + 1 hour`

  - [x] 11.5 Create `ResetPasswordCommand` with handler and validator
    - Create `apps/api/Jobuler.Application/Auth/Commands/ResetPasswordCommand.cs`
    - Handler: validate `newPassword.Length >= 8`; hash the provided raw token with SHA-256; look up `PasswordResetToken` by hash; throw `InvalidOperationException("Invalid or expired reset token.")` if not found or `!resetToken.IsValid`; load `User`; call `user.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12))`; call `resetToken.MarkUsed()`; revoke all active `RefreshToken`s for the user by calling `rt.Revoke()`; save all in a single `SaveChangesAsync` call
    - Add `ResetPasswordCommandValidator`: `Token` not empty, `NewPassword` minimum length 8
    - _Requirements: 14.1, 14.2, 14.3, 14.4, 14.5, 14.6, 14.7, 14.8_

  - [x]* 11.6 Write property test for invalid tokens are always rejected
    - **Property 15: Invalid tokens are always rejected**
    - **Validates: Requirements 14.3, 14.4, 14.5**
    - Use FsCheck; generate tokens in invalid states (wrong hash, past expiry, already used); assert `ResetPasswordCommand` throws `InvalidOperationException` and `user.PasswordHash` is unchanged

  - [x]* 11.7 Write property test for BCrypt work factor 12
    - **Property 16: Password reset produces valid BCrypt hash at work factor 12**
    - **Validates: Requirements 14.6**
    - Use FsCheck; generate random valid passwords (length ≥ 8); after `ResetPasswordCommand` succeeds, assert `BCrypt.Verify(newPassword, user.PasswordHash)` returns `true` and the hash uses work factor 12

  - [x]* 11.8 Write property test for refresh tokens revoked after reset
    - **Property 17: Successful reset invalidates all refresh tokens**
    - **Validates: Requirements 14.8**
    - Use FsCheck; seed N (1–20) active refresh tokens for the user; after `ResetPasswordCommand` succeeds, assert all N tokens have `revoked_at` set

  - [x]* 11.9 Write property test for short passwords rejected
    - **Property 18: Short passwords are rejected**
    - **Validates: Requirements 14.7**
    - Use FsCheck; generate random strings of length 0–7; assert `ResetPasswordCommand` throws and `user.PasswordHash` is unchanged

- [x] 12. Forgot Password — API endpoints
  - [x] 12.1 Add `ForgotPassword` and `ResetPassword` endpoints to `AuthController`
    - In `apps/api/Jobuler.Api/Controllers/AuthController.cs`, add:
      - `POST /auth/forgot-password` — `[AllowAnonymous]`, dispatches `ForgotPasswordCommand`, returns `Ok()`
      - `POST /auth/reset-password` — `[AllowAnonymous]`, dispatches `ResetPasswordCommand`, returns `NoContent()`
    - Add `ForgotPasswordRequest(string Email)` and `ResetPasswordRequest(string Token, string NewPassword)` records
    - _Requirements: 12.1, 14.1_

- [x] 13. Forgot Password — frontend pages and login updates
  - [x] 13.1 Add `forgotPassword` and `resetPassword` functions to `lib/api/auth.ts`
    - Add `forgotPassword(email: string): Promise<void>` — POST to `/auth/forgot-password`, always resolves (never throws on 200)
    - Add `resetPassword(token: string, newPassword: string): Promise<void>` — POST to `/auth/reset-password`, throws with Hebrew error message on non-2xx
    - _Requirements: 15.3, 15.6_

  - [x] 13.2 Create `/forgot-password` page
    - Create `apps/web/app/forgot-password/page.tsx` as a public (no auth) page
    - Single email input, submit button labelled "שלח קישור לאיפוס"
    - On submit: call `forgotPassword(email)`, always show success message "אם הכתובת רשומה במערכת, תקבל הודעה בקרוב." regardless of outcome — never expose an error to the user
    - Include a link back to `/login`
    - _Requirements: 15.2, 15.3_

  - [x] 13.3 Create `/reset-password` page
    - Create `apps/web/app/reset-password/page.tsx` as a public (no auth) page
    - Read `?token=` from `useSearchParams()`
    - New password input, confirm password input (client-side match validation only), submit button labelled "אפס סיסמה"
    - On success: `router.push("/login?reset=1")`
    - On error: display Hebrew error message inline
    - _Requirements: 15.4, 15.5, 15.6, 15.7_

  - [x] 13.4 Update login page with forgot-password link and reset success banner
    - In `apps/web/app/login/page.tsx`, add a "שכחת סיסמה?" link below the password field navigating to `/forgot-password` (the link does not currently exist — verify before adding)
    - Add a success banner displayed when `searchParams.get("reset") === "1"`: "הסיסמה אופסה בהצלחה! התחבר עם הסיסמה החדשה." styled like the existing `justRegistered` banner
    - _Requirements: 15.1, 15.8_

- [x] 14. Checkpoint — Forgot Password
  - Ensure all tests pass, ask the user if questions arise.

- [x] 15. Close-out — group-ownership step documentation
  - [x] 15.1 Create `docs/steps/029-group-ownership.md`
    - Title, phase, purpose, files created/modified (migration 009, `PendingOwnershipTransfer` entity, `InitiateOwnershipTransferCommand`, `ConfirmOwnershipTransferCommand`, `CancelOwnershipTransferCommand`, `GetGroupMembersQuery` ownership flag, `GroupsController` transfer endpoints, frontend transfer UI), key decisions (email confirmation flow, no-op `IEmailSender` default, token-based confirmation link), how it connects, how to verify, what comes next, and a git commit command
    - _Requirements: 10.1, 10.2, 10.3_

- [x] 16. Final checkpoint — all tests pass
  - Run all backend tests in `apps/api/Jobuler.Tests` and all frontend tests in `apps/web/__tests__`; surface any failures before closing the spec
  - _Requirements: 11.1, 11.2, 11.3_

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Migration numbering: 012 = `group_alerts`, 013 = `password_reset_tokens` — do not reuse or renumber
- `User.SetPasswordHash()` must be added before `ResetPasswordCommand` can compile (task 9.3)
- The login page currently has a register link but no forgot-password link — task 13.4 adds both the link and the reset banner
- `INotificationSender` is separate from `IEmailSender`; `IEmailSender` handles system emails (ownership transfer), `INotificationSender` handles user-facing notifications (password reset)
- All permission checks happen in the Application layer via `IPermissionService`, never in controllers
- Property tests use FsCheck (backend) and fast-check (frontend); minimum 100 iterations each
