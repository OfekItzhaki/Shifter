# Portable Space Isolation

## Goal

Shifter should be usable as a normal shared SaaS today while staying ready for
future dedicated deployments for large, sensitive, or regulated customers.

The core requirement is portability: a customer's operational data should be
movable from the shared production environment into an isolated database or
deployment without changing the product codebase or rebuilding the customer
model from scratch.

## Decision

Use one codebase and make `Space` data portable across deployments.

Do not classify sensitive customers by storing labels such as `army`, `IDF`,
`government`, or country-specific security categories in the shared production
database. Sensitive classification belongs in provisioning, deployment
configuration, contracts, and operator processes, not in every row of product
data.

## Boundaries

### Deployment

A deployment is a running Shifter environment with its own configuration,
secrets, database connection, storage, cache, workers, and domain.

Examples:

```text
commercial-prod
idf-dedicated-prod
country-x-defense-prod
restaurant-chain-a-prod
```

A deployment may host many organizations, one organization, or one space. For
defense or government customers, the preferred model is one dedicated deployment
with isolated infrastructure.

### Organization

An organization is the customer/account boundary above spaces.

Examples:

```text
Organization: Acme Restaurants
  Space: North Branch
  Space: South Branch

Organization: Dedicated Customer Deployment
  Space: Company A
  Space: Platoon B
```

For ordinary commercial customers, organizations can live in the shared SaaS
database. For sensitive customers, the organization should live only inside its
dedicated deployment database.

Organizations are also the portability boundary for future migrations. A space
can start under a default organization and later be moved or merged into an
official organization after ownership is verified.

Initial shared-SaaS onboarding should collect setup signals, not hard customer
classification:

- operating country/region for locale, timezone, billing, and later search
- setup template such as general shifts, restaurant/hospitality, security, or
  military-style duty roster
- optional organization or workplace name

If the name is blank, the product may generate a default from country +
template. This gives the operator useful discovery clues later without asking
new users for business numbers, legal affiliation, or sensitive customer type.

### Space

`Space` remains the operational tenant boundary used by the product. Groups,
people, schedules, tasks, constraints, billing state, and notifications are
scoped to a space.

Spaces must stay exportable and importable as complete units.

### Group

A group is not a customer classifier. A group belongs to a space. A group should
not decide where data is hosted.

## How The System Knows Where Data Belongs

The system should not guess from names like "IDF", "army", "restaurant", or
"Yokneam".

Placement is decided at provisioning time:

1. A user creates or joins an organization/account.
2. The current deployment decides whether that organization is allowed there.
3. Spaces are created under that organization.
4. If the organization later needs isolation, its spaces are exported and
   imported into a dedicated deployment.

For shared commercial SaaS, the organization can be a normal database entity.
For sensitive customers, the public/shared deployment should not need to know
the customer category. Routing should happen through deployment configuration,
customer-specific domains, or operator-controlled provisioning.

## Sensitive Customer Pattern

When a sensitive customer starts in shared SaaS and later moves to a dedicated
deployment:

1. Search candidate organizations/spaces using names, owner emails, verified
   domains, billing records, operating country, setup template, and invitations.
2. Have an authorized customer representative confirm the candidate list.
3. Move or merge the confirmed spaces into the official organization.
4. Freeze writes for the organization.
5. Export all organization/space-scoped data and identity links.
6. Create the dedicated deployment and database.
7. Import the exported data.
8. Re-bind users to the dedicated auth provider if required.
9. Verify schedules, files, memberships, permissions, and audit logs.
10. Mark the original shared-SaaS organization as relocated/disabled.
11. Keep the disabled copy for a rollback window, currently 90 days.
12. After the rollback window, mark the organization purge-pending.
13. Permanently delete or anonymize the old copy only after explicit platform
    admin confirmation and signed operational approval.

The dedicated deployment can contain names, countries, and internal org
structure because it is isolated to that customer. The shared deployment should
avoid retaining sensitive customer labels after migration.

## Future Implementation Phases

### Phase 1: Tenant Portability Audit

Verify every customer-owned table is scoped by `SpaceId` directly or through a
clear parent path. This includes:

- operational entities
- schedules and assignments
- constraints
- people and memberships
- notifications
- files and object storage paths
- cache keys
- background jobs
- audit logs
- billing records

### Phase 2: Organization Boundary

Introduce an organization/account boundary above spaces.

The initial implementation can be conservative:

- `Organization`
- `OrganizationMembership`
- `Space.OrganizationId`
- space creation attaches to an organization
- existing spaces are migrated into a default organization
- relocated organizations are disabled before deletion and track purge
  eligibility

Avoid sensitive classification fields in the first version.

### Phase 2b: Organization Billing

Keep current space subscriptions as the shared-SaaS/default path, but add an
organization-level billing path before enterprise migrations:

- ordinary shared SaaS can continue to bill by space
- verified multi-space customers can be moved to organization billing
- private online deployments should usually use annual subscription or contract
  billing because Shifter still runs and evolves as an online service
- offline/private-network deployments can use a setup/license fee, optionally
  with annual maintenance/support for updates, support, and security fixes
- one-time payment can remain an enterprise option, but should be priced as a
  license plus deployment/support scope rather than as normal SaaS

When an organization migrates to dedicated deployment, individual space
subscriptions should be canceled or marked as covered by the organization
contract before the shared copy is disabled.

The shared SaaS database can store neutral billing modes such as
`SharedSaaS`, `PrivateOnline`, `OfflineLicense`, and `EnterpriseInvoice`. These
describe how the account is paid for; they are not sensitive customer
classifications and should not be used to infer military, government, or
country-specific status.

### Phase 3: Deployment Awareness

Add environment-level deployment configuration.

Examples:

```text
SHIFTER_DEPLOYMENT_ID=commercial-prod
SHIFTER_DEPLOYMENT_MODE=shared
SHIFTER_ALLOWED_ORGANIZATION_IDS=optional allow-list
```

This lets one codebase run in shared SaaS, a dedicated customer deployment, or a
country/customer-specific deployment.

### Phase 4: Space Export/Import

Build a supported export/import pipeline for one or more spaces:

- deterministic export manifest
- dependency-ordered data extraction
- file/storage export
- import validation
- dry-run mode
- post-import integrity checks

### Phase 5: Dedicated Deployment Runbook

Document and automate the move from shared SaaS to dedicated deployment:

- write freeze
- export
- import
- smoke tests
- DNS/domain switch
- user/auth migration
- rollback plan

## Non-Goals For The First Pass

- No automatic detection of army/government/restaurant spaces.
- No sensitive customer labels in shared product tables.
- No separate codebase for defense customers.
- No cross-deployment database routing until a real customer need appears.

## Current Repo Fit

The current architecture already treats `Space` as the top-level tenant boundary
and uses `ITenantScoped` plus PostgreSQL RLS for space-scoped tables. That is a
good base.

The next architectural step is adding a customer/account boundary above `Space`
and ensuring every table, background job, cache key, file path, and export path
can be traced back to a space.
