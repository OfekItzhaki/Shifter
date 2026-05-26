# 592 — Self-Service Scheduling DI Registration

## Phase

Phase 10 — Self-Service Scheduling (Integration Wiring)

## Purpose

Register all self-service scheduling services in the DI container so that controllers, MediatR handlers, and background jobs can resolve them at runtime. This is the final wiring step that connects all previously implemented components.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/Program.cs` | Added `using Jobuler.Application.Scheduling.SelfService;` import |
| `apps/api/Jobuler.Api/Program.cs` | Added 6 scoped service registrations for self-service scheduling |

## Key decisions

1. **Scoped lifetime** — All services are registered as `Scoped` to match the per-request DbContext lifetime and ensure transaction consistency (especially important for advisory locks).
2. **No separate extension method** — Followed the existing pattern of registering directly in Program.cs rather than introducing a new `IServiceCollection` extension.
3. **FluentValidation already covered** — The existing `AddValidatorsFromAssembly(typeof(LoginCommand).Assembly)` call auto-discovers all validators in the Application assembly, including all self-service validators.
4. **Background jobs already registered** — All 5 hosted services (GenerateCycleSlotsJob, ProcessExpiredWaitlistOffersJob, NotifyRequestWindowOpenJob, CheckUnderScheduledMembersJob, ExpireSwapRequestsJob) were already registered in previous steps.

## Services registered

| Interface | Implementation | Lifetime |
|-----------|---------------|----------|
| `ISlotGenerationService` | `SlotGenerationService` | Scoped |
| `IShiftRequestService` | `ShiftRequestService` | Scoped |
| `ISlotAvailabilityEngine` | `SlotAvailabilityEngine` | Scoped |
| `IWaitlistService` | `WaitlistService` | Scoped |
| `IShiftSwapService` | `ShiftSwapService` | Scoped |
| `ISlotLockService` | `PostgresAdvisoryLockService` | Scoped |

## How it connects

- Controllers (`ShiftRequestsController`, `ShiftSwapsController`, `WaitlistController`, `ShiftSlotsController`) inject these services directly.
- MediatR command handlers (`AdminAssignShiftCommand`, `JoinWaitlistCommand`, etc.) resolve services via DI.
- Background jobs (`GenerateCycleSlotsJob`, `ProcessExpiredWaitlistOffersJob`) create scopes and resolve `ISlotGenerationService` / `IWaitlistService`.

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build
```

Build should succeed with no errors. At runtime, all self-service endpoints will resolve their dependencies correctly.

## What comes next

- Task 19: Final checkpoint — full integration verification

## Git commit

```bash
git add -A && git commit -m "feat(self-service): register all self-service scheduling services in DI container"
```
