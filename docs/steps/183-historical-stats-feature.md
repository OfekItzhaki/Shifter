# 183 — Historical Stats Feature

## Phase
Phase 8 — Analytics & Insights

## Purpose
Adds a time-series statistics endpoint and a frontend stats page with sparkline charts, allowing space admins to visualize trends in assignments, solver runs, and burden distribution over time.

## What was built

### Backend
| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/Queries/GetHistoricalStatsQuery.cs` | Query record + DTOs (DailyStatPoint, WeeklyStatPoint, HistoricalStatsDto) |
| `apps/api/Jobuler.Application/Scheduling/Queries/GetHistoricalStatsHandler.cs` | MediatR handler — queries assignments per day, solver runs per day, burden trend per week, and totals |
| `apps/api/Jobuler.Api/Controllers/StatsController.cs` | Added `GET /spaces/{spaceId}/stats/historical?days=30` endpoint with permission check |

### Frontend
| File | Description |
|------|-------------|
| `apps/web/lib/api/stats.ts` | API client function + TypeScript interfaces for historical stats |
| `apps/web/components/stats/MiniChart.tsx` | Pure SVG/CSS sparkline component (bar + line modes, no external chart library) |
| `apps/web/app/stats/page.tsx` | Stats overview page with day-range selector, stat cards, and charts |
| `apps/web/components/shell/AppShell.tsx` | Added NavItem for `/stats` in sidebar after "My Groups" |
| `apps/web/messages/en.json` | Added `stats` section + `nav.stats` key |
| `apps/web/messages/he.json` | Added `stats` section + `nav.stats` key (Hebrew) |
| `apps/web/messages/ru.json` | Added `stats` section + `nav.stats` key (Russian) |

## Key decisions
- **No external chart library** — MiniChart uses pure SVG polyline (line mode) and CSS flex bars (bar mode) for minimal bundle impact.
- **Burden trend approximation** — Weekly burden is computed as average assignments per active person per week, which is a simple but meaningful proxy.
- **CreatedAt-based grouping** — Assignments are grouped by their `CreatedAt` timestamp (from the Entity base class) since they don't have a separate date field.
- **Permission gated** — Endpoint requires `SpaceView` permission, consistent with the existing burden stats endpoint.
- **Day range selector** — Frontend supports 7/14/30/90 day ranges, passed as a query parameter to the API.

## How it connects
- Extends the existing `StatsController` which already serves burden stats.
- Uses the same `AppDbContext` and entity model patterns as `GetBurdenStatsQuery`.
- The stats page is accessible from the main sidebar navigation.
- Reuses `useSpaceStore` for space context, consistent with other pages.

## How to run / verify
1. Build the API: `dotnet build apps/api/Jobuler.Api/Jobuler.Api.csproj`
2. Start the API and navigate to `GET /spaces/{spaceId}/stats/historical?days=30` with a valid token.
3. Start the frontend and navigate to `/stats` — verify the page loads with charts.
4. Toggle day ranges (7d/14d/30d/90d) and confirm the API is called with the correct parameter.

## What comes next
- Add caching for historical stats (they don't change frequently).
- Add more granular burden breakdown (per-group historical stats).
- Consider server-side rendering or React Query for better loading UX.

## Git commit
```bash
git add -A && git commit -m "feat(stats): historical stats time-series endpoint and frontend chart page"
```
