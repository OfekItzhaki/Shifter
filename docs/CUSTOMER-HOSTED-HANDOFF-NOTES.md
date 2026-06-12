# Customer-Hosted Handoff Notes

Use this template for every customer-hosted install before go-live. Do not paste
secret values, private keys, bearer tokens, connection strings, or customer
passwords into this document. Record where the customer stores those secrets and
who owns rotation.

## Customer And Environment

- Customer:
- Environment: production / staging / demo
- Customer technical owner:
- OfekLabs owner:
- Handoff date:
- Source branch:
- Source commit:

## Package Verification

- Package archive:
- Package generated at:
- Package SHA-256:
- Checksum verified by:
- Checksum verified at:
- Checksum command/result:

```bash
sha256sum -c shifter-customer-hosted-<version>.zip.sha256
```

## License

- Licensee:
- Deployment mode:
- License expiration:
- Signed license file path on target host:
- License public key path on target host:
- License private key owner/location: outside the customer package
- License validation result:

## Domains And Network

- Web URL:
- API URL:
- Public reverse proxy/WAF:
- DNS owner:
- TLS certificate owner:
- Public inbound ports:
- Private services confirmed private: PostgreSQL / Redis / MinIO / Seq
- Rate-limit/WAF rules confirmed for auth, billing, imports, solver, admin:

## Environment And Secrets

- Env file path on target host:
- Env/secrets owner:
- Secrets manager or storage location:
- Field encryption key owner:
- Secret rotation notes:
- Optional processors approved: billing / SMS / push / error tracking / analytics

## Provider Configuration

- AI mode: disabled / hosted / private OpenAI-compatible
- AI provider/base URL:
- AI model:
- AI no-export/customer-data policy approved:
- Email provider:
- SMS provider:
- Web push configured:
- Billing provider:
- Error tracking:
- Product analytics:
- Support chat/contact tool:

## Verification Evidence

- Customer env validation command/result:

```powershell
.\infra\scripts\validate-customer-env.ps1 -EnvFile .\infra\compose\.env
```

- Target install verification command/result:

```powershell
.\infra\scripts\verify-customer-hosted-install.ps1 `
  -EnvFile .\infra\compose\.env `
  -BaseUrl https://<customer-domain> `
  -ComposeProjectName shifter-<customer>
```

- `/ready` result:
- `/health/detailed` result:
- Provider health result:
- Browser smoke result:
- Mobile/PWA smoke result:
- Latest package preflight CI run:

## Backup And Restore

- Backup path:
- Off-host backup storage:
- Backup retention:
- Last backup command/result:
- Restore dry-run command/result:
- Next restore test due:

## Migration Or Import

- Migration type: whole install / tenant-by-tenant / new empty install
- Tenant import smoke required:
- Tenant import smoke command/result:
- Production migration window:
- Rollback owner and plan:

## Security Sign-Off

- HTTPS enforced:
- Admin endpoints protected:
- Customer data export policy approved:
- AI provider approved for this customer:
- Database access restricted:
- Object storage access restricted:
- Logs reviewed for secret leakage:

## Support And Escalation

- Customer admin contact:
- Customer incident contact:
- OfekLabs support contact:
- Escalation contact:
- Maintenance window:
- Support hours/SLA:

## Open Exceptions

| Item | Risk | Owner | Due Date | Accepted By |
| --- | --- | --- | --- | --- |
| | | | | |

## Go-Live Approval

- Customer technical approval:
- Customer business approval:
- OfekLabs approval:
- Go-live date/time:
- Notes:
