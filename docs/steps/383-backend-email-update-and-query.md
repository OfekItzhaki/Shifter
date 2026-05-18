# 383 — Backend Email Update and Query Projection

## Phase

Bugfix — Redirect and Member Email Fix (Task 3.5)

## Purpose

The member email field was missing from the backend API entirely. The `UpdatePersonInfoRequest` did not accept an email, the `UpdatePersonInfoCommand` did not pass it through, and the `GetGroupMembers` query did not project it. This step adds email support end-to-end in the backend so the API returns email in member data and accepts email in update payloads.

## What was built

| File | Action | Description |
|---|---|---|
| `apps/api/Jobuler.Domain/People/Person.cs` | Modified | Added `Email` property; updated `UpdateFull` method to accept and set email |
| `apps/api/Jobuler.Application/People/Commands/UpdatePersonInfoCommand.cs` | Modified | Added `Email` parameter to command record; handler passes it to `UpdateFull` |
| `apps/api/Jobuler.Api/Controllers/PeopleController.cs` | Modified | Added `Email` to `UpdatePersonInfoRequest` record; passes it to command |
| `apps/api/Jobuler.Application/Groups/Queries/GetGroupsQuery.cs` | Modified | Added `Email` to `GroupMemberDto`; query handler projects `p.Email` |
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/PeopleConfiguration.cs` | Modified | Added EF Core column mapping for `email` |
| `infra/migrations/065_people_email.sql` | Created | Adds `email TEXT` column to `people` table |

## Key decisions

- Email is nullable (`string?`) since not all people records will have an email address.
- The `email` parameter in `UpdateFull` is optional with a default of `null` to maintain backward compatibility with existing callers (e.g., `AuthController`).
- The `Email` parameter in `GroupMemberDto` is appended at the end with a default of `null` so existing test code constructing the DTO with positional args continues to compile.

## How it connects

- The frontend `GroupMemberDto` (task 3.3) now receives email from this backend projection.
- The frontend `updatePersonInfo` call (task 3.4) now sends email to this backend endpoint.
- The migration must be run before the email column can be read/written.

## How to run / verify

```bash
# Run the migration
psql -f infra/migrations/065_people_email.sql

# Build and test
cd apps/api
dotnet build
dotnet test
```

All 1312 tests pass.

## What comes next

- Task 3.6: Verify bug condition exploration test now passes
- Task 3.7: Verify preservation tests still pass

## Git commit

```bash
git add -A && git commit -m "fix(api): add email to UpdatePersonInfoRequest and GetGroupMembers query projection"
```
