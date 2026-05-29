# 634 — Remove Hardcoded Hebrew from Backend and Frontend

## Phase
Phase 4 — Internationalization & Quality

## Purpose
The backend was returning Hebrew error messages directly to clients, and several frontend components had hardcoded Hebrew strings instead of using the next-intl translation system. This violates the i18n architecture where the backend should return language-neutral error codes/English messages and the frontend should handle all user-facing translations via the message files.

## What was built

### Backend (apps/api/)
All Hebrew strings in user-facing responses replaced with English equivalents:

- **Controllers**: `GroupOptOutController.cs`, `ImportController.cs`, `ScheduleRunsController.cs` — error messages now use English error codes/messages
- **Middleware**: `ExceptionHandlingMiddleware.cs` — all Hebrew error details replaced with English
- **Domain**: `HomeLeaveConfig.cs` — validation exception messages now in English
- **Application layer**:
  - `SmartImportCommand.cs` — error message in English
  - `RegisterCommandHandler.cs`, `ResendVerificationCommand.cs` — email subjects/bodies in English
  - `AddPersonByEmailCommand.cs`, `AddPersonByPhoneCommand.cs`, `AddPersonToGroupByIdCommand.cs` — notification text in English
  - `ConfirmOwnershipTransferCommand.cs`, `InitiateOwnershipTransferCommand.cs`, `RestoreGroupCommand.cs` — notification/email text in English
  - `PublishVersionCommand.cs`, `TriggerRegenerationCommand.cs` — notification text in English
  - `MigrateUserSpaceCommand.cs` — space name fallback in English
  - `UpsertHomeLeaveConfigValidator.cs` — validation messages in English
  - `PreviewHomeLeaveHandler.cs` — error message in English
- **Infrastructure layer**:
  - `PermissionService.cs` — permission error in English
  - `ConflictNotificationText.cs` — Hebrew locale now returns English
  - `EmailInvitationSender.cs`, `WhatsAppInvitationSender.cs` — invitation text in English
  - `RoutingNotificationSender.cs`, `TwilioWhatsAppSender.cs` — password reset text in English
  - `ScheduleNotificationSender.cs` — all Hebrew locale branches now return English
  - `SolverWorkerService.cs` — all Hebrew locale branches now return English
  - `OpenAiAssistant.cs` — system prompt in English

### Frontend (apps/web/)
- **TrialBanner.tsx** — fully migrated to `useTranslations("trialBanner")` with all strings in message files
- **OfflineBanner.tsx** — migrated to `useTranslations("offlineBanner")` with all strings in message files
- **Message files** — added `trialBanner` and `offlineBanner` sections to `en.json`, `he.json`, `ru.json`

### Tests updated
- `ConflictNotificationTextTests.cs` — assertions updated to match new English output
- `ExceptionHandlingMiddlewareTests.cs` — all Hebrew assertions updated to English
- `ExceptionHandlingMiddlewarePropertyTests.cs` — property test assertions updated

## Key decisions
- **Backend returns English, frontend translates**: The backend now consistently returns English messages. The frontend is responsible for displaying locale-appropriate text via next-intl.
- **DayOfWeekMapper and ImportColumnNames kept as-is**: These contain Hebrew as data-mapping values (recognizing Hebrew column headers and day names in user-uploaded files). This is legitimate business logic, not user-facing error messages.
- **Legal pages (terms/privacy) kept as-is**: These are intentionally Hebrew legal documents.
- **global-error.tsx kept as-is**: This renders outside the i18n provider as a root error boundary.
- **Test files with Hebrew data**: Tests that use Hebrew as test data (e.g., GroupTaskTests testing Hebrew task names) are kept — they test that the system handles Hebrew input correctly.

## How it connects
- Enables proper multi-language support where the frontend controls all user-facing text
- Backend error codes can now be mapped to any locale on the frontend
- Consistent with the next-intl architecture already in place

## How to run / verify
1. Run backend tests: `dotnet test` in `apps/api/`
2. Verify no Hebrew in backend responses: search for `[\u0590-\u05FF]` in non-test .cs files
3. Verify frontend renders correctly in all three locales

## What comes next
- Migrate remaining frontend components (stats charts, ImportModal, etc.) to use translation keys
- Add frontend error message mapping for backend error codes like `trial_expired`, `subscription_inactive`

## Git commit
```bash
git add -A && git commit -m "feat(i18n): remove hardcoded Hebrew from backend and migrate TrialBanner/OfflineBanner to next-intl"
```
