# Staging Manual Smoke Evidence

Use this template for the final staging sign-off before opening or merging a
`develop` to `main` PR. Do not paste passwords, tokens, private URLs with
embedded credentials, reset links, or user PII into this document.

After filling a copy of this file, validate it:

```powershell
.\infra\scripts\check-staging-smoke-evidence.ps1 `
  -EvidencePath <completed-staging-smoke-evidence.md>
```

## Environment

- Test date:
- Tester:
- Source branch: `develop`
- Source commit:
- Staging web URL:
- Staging API URL:
- GitHub `Deploy Staging` run:
- Customer-hosted preflight run:
- Broad CI run:
- Release readiness audit result:
- Hosted smoke command/result:

```powershell
.\infra\scripts\check-release-readiness.ps1 `
  -WebBaseUrl <staging-web-url> `
  -ApiBaseUrl <staging-api-url>
```

## Accounts And Data

- Admin test account:
- Member test account:
- Space:
- Group:
- Self-service cycle:
- Special-day test data present: yes / no
- Email inbox or provider used for reset/invite testing:
- Browser/device matrix:

## Public And Auth Flows

| Flow | Result | Evidence | Notes |
| --- | --- | --- | --- |
| Landing page loads | pending | | |
| Login as admin | pending | | |
| Login as member | pending | | |
| Registration or invite acceptance | pending | | |
| Forgot-password request | pending | | |
| Reset password | pending | | |
| Change password | pending | | |
| End old session after password change | pending | | |

## PWA Flows

| Flow | Result | Evidence | Notes |
| --- | --- | --- | --- |
| Manifest loads | pending | | |
| Service worker registers | pending | | |
| Desktop or mobile install prompt appears | pending | | |
| Installed app opens expected route | pending | | |
| Reconnect refreshes self-service data | pending | | |

## Member Self-Service Flows

Run member flows from `/pick` when possible.

| Flow | Result | Evidence | Notes |
| --- | --- | --- | --- |
| Select self-service group | pending | | |
| Pick an open shift | pending | | |
| Join a waitlist for a full shift | pending | | |
| Leave a waitlist | pending | | |
| Cancel an owned future shift | pending | | |
| Report cannot attend | pending | | |
| Request a shift change | pending | | |
| Propose a shift swap | pending | | |
| Accept a shift swap | pending | | |
| Decline a shift swap | pending | | |
| Cancel a proposed swap | pending | | |
| Request special leave | pending | | |
| Cancel special leave | pending | | |
| No-coverage special-day slot blocks member pick/waitlist | pending | | |

## Admin Self-Service Flows

| Flow | Result | Evidence | Notes |
| --- | --- | --- | --- |
| Operations page shows pending counts | pending | | |
| Approve absence report | pending | | |
| Reject absence report | pending | | |
| Approve shift-change request | pending | | |
| Reject shift-change request | pending | | |
| Approve special leave | pending | | |
| Reject special leave | pending | | |
| Manual assignment succeeds | pending | | |
| Manual removal triggers expected slot/waitlist state | pending | | |
| Attendance can be marked present/no-show/excused | pending | | |
| Closeout metrics include coverage and unresolved work | pending | | |
| Closeout metrics include special-day impact | pending | | |
| Closeout CSV exports | pending | | |
| Closeout PDF exports | pending | | |
| Closeout PDF fingerprint is visible and matches expected CSV evidence | pending | | |

## Operational Checks

| Check | Result | Evidence | Notes |
| --- | --- | --- | --- |
| API `/ready` healthy | pending | | |
| API `/health` healthy | pending | | |
| Provider health acceptable for staging | pending | | |
| Email provider can deliver reset/invite messages | pending | | |
| Backups run | pending | | |
| Restore dry-run completed or scheduled with owner | pending | | |
| Cloudflare or reverse-proxy rules reviewed | pending | | |
| Logs checked for obvious errors and secret leakage | pending | | |

## Open Issues

| Item | Severity | Owner | Decision Before Main |
| --- | --- | --- | --- |
| | | | |

## Sign-Off

- Staging accepted for `develop` to `main` PR: yes / no
- Accepted by:
- Accepted at:
- Follow-up owner:
- Notes:
