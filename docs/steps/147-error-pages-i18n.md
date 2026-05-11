# 147 — Error Pages i18n Namespace

## Phase
Feature — Custom Error Pages

## Purpose
Add the `errorPages` i18n namespace to all locale files (en, he, ru) so that the custom error pages (404, 401, 403, 500, client error boundary) can display translated strings.

## What was built

| File | Description |
|------|-------------|
| `apps/web/messages/en.json` | Added `errorPages` namespace with English strings for notFound, unauthorized, forbidden, serverError, clientError |
| `apps/web/messages/he.json` | Added `errorPages` namespace with Hebrew translations |
| `apps/web/messages/ru.json` | Added `errorPages` namespace with Russian translations |

## Key decisions
- Placed the `errorPages` namespace at the end of each locale file (after `platform`) for consistency
- Each error page type has its own sub-object with `heading`, `message`, and action button labels
- Hebrew translations use natural phrasing appropriate for RTL display
- Russian translations use formal "вы" form consistent with the rest of the app

## How it connects
- Error page components (task 2.2+) will use `useTranslations("errorPages")` to access these strings
- The ErrorBoundary fallback component will use the `clientError` sub-namespace
- The axios interceptor redirects to error pages that consume these translations

## How to run / verify
```powershell
# Validate JSON syntax
Get-Content "apps\web\messages\en.json" -Raw | ConvertFrom-Json | Out-Null
Get-Content "apps\web\messages\he.json" -Raw | ConvertFrom-Json | Out-Null
Get-Content "apps\web\messages\ru.json" -Raw | ConvertFrom-Json | Out-Null

# Check errorPages namespace exists
$en = Get-Content "apps\web\messages\en.json" -Raw | ConvertFrom-Json
$en.errorPages.notFound.heading  # "Page Not Found"
$en.errorPages.clientError.reload  # "Reload Page"
```

## What comes next
- Task 2.2: Create the `ErrorPageLayout` shared component
- Task 2.3: Implement individual error page routes (404, 401, 403, 500)
- Task 2.4: Enhance the ErrorBoundary with i18n support

## Git commit
```bash
git add -A && git commit -m "feat(error-pages): add errorPages i18n namespace to all locale files"
```
