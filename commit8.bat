@echo off
git add -A
git commit -m "fix(ef): ignore inherited CreatedAt in SpaceMembership, SpacePermissionGrant, PersonRoleAssignment, OwnershipTransferHistory"
git push origin main
