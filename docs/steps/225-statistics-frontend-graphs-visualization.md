# 225 — Statistics Frontend Graphs & Visualization

## Phase

Phase 4 — Frontend Graphs + Visualization (Statistics Overhaul)

## Purpose

Add graphical visualizations to the statistics view using Recharts. This gives space admins visual insight into assignment distribution, burden breakdown, trends over time, and fairness comparisons — all within the existing StatsTab.

## What was built

| File | Description |
|------|-------------|
| `apps/web/package.json` | Added `recharts` ^2.12.0 dependency |
| `apps/web/components/stats/AssignmentsBarChart.tsx` | Bar chart showing total assignments per person for a time window |
| `apps/web/components/stats/BurdenBreakdownChart.tsx` | Stacked bar chart with hard (red), normal (gray), easy (green) breakdown |
| `apps/web/components/stats/BurdenTrendChart.tsx` | Multi-line chart showing burden score trends over time per person |
| `apps/web/components/stats/FairnessComparisonChart.tsx` | Horizontal diverging bar chart showing deviation from group average |
| `apps/web/components/stats/BurdenBadge.tsx` | Colored pill/tag component for burden levels (red/gray/green) |
| `apps/web/components/stats/RotationProgressCard.tsx` | Per-person rotation progress with cycle number and progress bar |
| `apps/web/app/groups/[groupId]/tabs/StatsTab.tsx` | Updated to include time range selector, historical data fetching, all charts, and rotation progress |

## Key decisions

- **Recharts** chosen per design doc — lightweight, React-native, good TypeScript support
- Charts render in a 2-column grid on desktop, single column on mobile
- Time range selector uses simple buttons (7d, 14d, 30d, 90d) matching existing week navigation pattern
- Historical data is fetched from `GET /spaces/{spaceId}/stats/historical/persons` with date range params
- Graphs appear ABOVE existing leaderboards — existing functionality preserved
- RotationProgressCard self-hides when the API returns 404 (non-army-template groups)
- All components use `"use client"` directive and are RTL-friendly with Hebrew labels
- Charts use `dir="ltr"` wrapper since Recharts renders left-to-right, with Hebrew text labels using `dir="rtl"`
- Fairness chart computes deviation from group average burden score client-side from historical data

## How it connects

- Depends on Phase 2 (historical stats API endpoint) and Phase 3 (rotation API endpoint)
- Uses existing `apiClient` for data fetching
- Reuses existing `getBurdenStats` for summary/leaderboard data
- BurdenBadge can be used anywhere burden levels are displayed (tasks tab, schedule views)
- Charts consume the same data shape defined in the design doc's `HistoricalPersonStatsDto`

## How to run / verify

1. Run `npm install` in `apps/web` to install recharts
2. Navigate to a group's Stats tab
3. Verify time range buttons switch between 7d/14d/30d/90d
4. If historical data exists, graphs should render in a 2×2 grid
5. If no historical data, a placeholder message appears
6. Rotation progress card only appears for army-template groups with rotation data
7. Existing leaderboards and people table remain below the graphs

## What comes next

- Phase 5: Property-based tests (optional)
- Final checkpoint: end-to-end verification of the full statistics overhaul

## Git commit

```bash
git add -A && git commit -m "feat(stats): add Recharts graphs, burden badges, and rotation progress to StatsTab"
```
