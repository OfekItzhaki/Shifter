# Step 051 — People Search, Name-First Invitations, Wired Notifications

## Phase
Phase 10 — People Management and Notifications

## Purpose

Three coordinated improvements:
1. **People search** — search by name or phone in the members tab and schedule tab
2. **Name-first person creation** — admin adds a person by name only, then invites them via email or WhatsApp
3. **Wired notifications** — in-app notifications now fire on real events; bell moved to sidebar

## What was built

### Backend

**Domain**
- `Person.cs` — added `InvitationStatus` field (`"pending"` | `"accepted"`), `SetInvitationStatus()`, `LinkUser()`, `SetPhoneNumber()` methods
- `PendingInvitation.cs` — new entity with secure token generation (SHA-256 hashed), 7-day expiry, `Accept()` method

**Application**
- `IInvitationSender.cs` — new interface for sending invitations via email or WhatsApp
- `CreatePersonCommand.cs` — updated to check for duplicate names (case-insensitive), sets `InvitationStatus = "pending"` when no `LinkedUserId`
- `InvitePersonCommand.cs` — sends invitation via `IInvitationSender`, validates contact per channel
- `AcceptInvitationCommand.cs` — accepts invitation by token, links `Person` to `User`
- `SearchPeopleQuery.cs` — searches people by name, displayName, or phone (min 2 chars, max 20 results)
- `PublishVersionCommand.cs` — now creates `Notification` for all space members on publish
- `ConfirmOwnershipTransferCommand.cs` — now creates `Notification` for new owner on transfer

**Infrastructure**
- `NoOpInvitationSender.cs` — logs invite URL to console (dev)
- `EmailInvitationSender.cs` — sends HTML invitation email via `IEmailSender`
- `WhatsAppInvitationSender.cs` — sends invitation via `INotificationSender`
- `CompositeInvitationSender.cs` — routes by channel (`"email"` → email, `"whatsapp"` → WhatsApp)
- `PendingInvitationConfiguration.cs` — EF Fluent API for `pending_invitations` table
- `PeopleConfiguration.cs` — added `invitation_status` column mapping

**API**
- `PeopleController.cs` — added `GET /people/search?q=`, `POST /people/{id}/invite`
- `InvitationsController.cs` — added `POST /invitations/accept?token=`
- `Program.cs` — registered `NoOpInvitationSender`, `EmailInvitationSender`, `WhatsAppInvitationSender`, `CompositeInvitationSender`

**Database**
- `infra/migrations/015_pending_invitations.sql` — adds `invitation_status` to `people`, creates `pending_invitations` table

### Frontend

**`apps/web/lib/api/people.ts`** (new file)
- `getPeople`, `getPersonDetail`, `searchPeople`, `createPerson`, `invitePerson`
- `getSpaceRoles`, `assignRole`, `removeRole`, `addRestriction`
- All DTOs: `PersonDto`, `RoleDto`, `PersonDetailDto`, `PersonSearchResultDto`, etc.

**`apps/web/components/shell/AppShell.tsx`**
- Notification bell moved from topbar into sidebar as `SidebarNotificationBell`
- Compact inline design with unread count badge, dropdown panel, mark-all-read
- Polls every 30 seconds

**`apps/web/app/groups/[groupId]/page.tsx`**
- Members tab: search box (filters by name/phone), name-first person creation form, inline invite form (WhatsApp/email channel selector)
- Schedule tab: person filter search box above date navigation
- Both read-only and admin member views have search

## Key decisions

- **No SMS**: WhatsApp is the preferred mobile channel for Israel. SMS skipped.
- **Pending badge**: Pending members appear immediately in the list — no hidden-until-accepted flow.
- **Invite button on all members**: Any member can be invited (not just pending ones) — useful for re-sending invitations.
- **Notification bell in sidebar**: Takes less space than topbar, consistent with the sidebar-first navigation pattern.

## How to run / verify

```bash
# Apply migration
$env:PGPASSWORD = "Akame157157"
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -d jobuler -f "C:\Users\User\Desktop\Jobduler\infra\migrations\015_pending_invitations.sql"

# Restart API (required after code changes)
dotnet run --project apps/api/Jobuler.Api/Jobuler.Api.csproj

# Frontend
cd apps/web && npm run dev
```

## What comes next

- Wire real WhatsApp Business API (Twilio/360dialog) into `WhatsAppInvitationSender`
- Wire real email provider (SendGrid/SES) into `EmailInvitationSender`
- Add invitation accept page at `/invitations/accept`

## Git commit

```bash
git add -A && git commit -m "feat(v2.1): people search, name-first invitations (email+WhatsApp), wired notifications, sidebar bell"
```
