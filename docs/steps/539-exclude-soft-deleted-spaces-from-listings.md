# Step 539 — Exclude Soft-Deleted Spaces from Listing Queries

## Phase

Space Management — API Layer

## Purpose

Ensures that spaces with a non-null `DeletedAt` timestamp are excluded from all listing and lookup queries, making soft-deleted spaces invisible to users as specified in Requirement 1.3.

## What was built

| File | Change |
|------|--------|
| `Jobuler.Application/Spaces/Queries/GetMySpacesQuery.cs` | Added `s.DeletedAt == null` filter to the Spaces join clause |
| `Jobuler.Application/Spaces/Queries/GetSpaceDetailQuery.cs` | Added `s.DeletedAt == null` filter to the FirstOrDefaultAsync predicate |
| `Jobuler.Application/Spaces/Queries/GetSpaceQuery.cs` | Added `s.DeletedAt == null` filter to the FirstOrDefaultAsync predicate |
| `Jobuler.Application/Spaces/Commands/JoinSpaceByInviteCodeCommandHandler.cs` | Added `s.DeletedAt == null` filter so users cannot join a soft-deleted space via invite code |
| `Jobuler.Application/Platform/Queries/GetPlatformStatsQuery.cs` | Added `s.DeletedAt == null` filter to the space count so platform stats exclude archived spaces |

## Key decisions

- The `JoinSpaceByInviteCodeCommand` was also updated because joining a soft-deleted space should not be possible — the space is effectively archived.
- Platform stats (`GetPlatformStatsQuery`) were updated for consistency — soft-deleted spaces should not inflate active space counts.
- Commands that load a space by ID for mutation (e.g., `SoftDeleteSpaceCommand`, `RestoreSpaceCommand`, `TransferOwnershipCommand`) intentionally do NOT filter by `DeletedAt` because they need to operate on soft-deleted spaces.

## How it connects

- Implements Requirement 1.3: "WHILE a Space has a non-null `DeletedAt` timestamp, THE Space_Management_Service SHALL exclude that Space from all listing queries for its members"
- Works alongside the `SoftDeleteSpaceCommand` (task 5.1) which sets `DeletedAt`, and `RestoreSpaceCommand` (task 5.2) which clears it.

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
dotnet test --no-build
```

Verify that:
1. `GetMySpacesQuery` does not return spaces where `DeletedAt` is set
2. `GetSpaceDetailQuery` returns null for soft-deleted spaces
3. `GetSpaceQuery` returns null for soft-deleted spaces
4. `JoinSpaceByInviteCodeCommand` rejects invite codes for soft-deleted spaces

## What comes next

- Task 12: Backend complete checkpoint
- Frontend API client and UI components (tasks 13–18)

## Git commit

```bash
git add -A && git commit -m "feat(spaces): exclude soft-deleted spaces from listing queries"
```
