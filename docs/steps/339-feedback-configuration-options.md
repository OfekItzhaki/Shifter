# 339 — Feedback Configuration Options

## Phase
Feature: Feedback & Bug Report FAB

## Purpose
Provide a strongly-typed configuration class for the feedback feature so that the developer email and rate-limit threshold are configurable via `appsettings.json` rather than hardcoded.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/appsettings.json` | Added `"Feedback"` section with `DeveloperEmail` and `MaxSubmissionsPerHour` |
| `apps/api/Jobuler.Application/Feedback/FeedbackOptions.cs` | Strongly-typed options class bound to the `Feedback` config section |
| `apps/api/Jobuler.Api/Program.cs` | Registered `FeedbackOptions` in DI via `services.Configure<FeedbackOptions>(...)` |

## Key decisions

- Placed `FeedbackOptions` in the **Application** layer (`Jobuler.Application/Feedback/`) because the command handler (also in Application) will consume it via `IOptions<FeedbackOptions>`. This avoids a dependency on Infrastructure for configuration.
- Used the standard ASP.NET Core Options pattern (`IOptions<T>`) for consistency with the rest of the project.
- Default values in the class match the appsettings values so the app works even if the section is missing.

## How it connects

- The `SubmitFeedbackCommandHandler` (task 1.2) will inject `IOptions<FeedbackOptions>` to read `DeveloperEmail` for the email recipient and `MaxSubmissionsPerHour` for rate-limit enforcement.
- The `FeedbackController` (task 1.3) does not directly use these options — it delegates to the handler.

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

Build should succeed with zero errors.

## What comes next

- Task 1.2: `SubmitFeedbackCommandHandler` injects `IOptions<FeedbackOptions>` to use `DeveloperEmail` and `MaxSubmissionsPerHour`.
- Task 1.3: `FeedbackController` dispatches the command.

## Git commit

```bash
git add -A && git commit -m "feat(feedback): add Feedback configuration section and FeedbackOptions class"
```
