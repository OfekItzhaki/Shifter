# Migration Guide: VPS → AWS ECS

This guide covers migrating from a single VPS (Docker Compose) to AWS ECS Fargate.

## When to migrate

- 500+ registered users
- Solver queue depth consistently > 0
- Average solver run time > 30 seconds
- You need high availability (zero downtime)

## Prerequisites

- AWS account with billing enabled
- AWS CLI installed and configured (`aws configure`)
- Terraform installed
- Domain name pointing to the VPS (will be re-pointed to AWS)

## Step 1: Provision AWS infrastructure

```bash
cd infra/terraform
terraform init
terraform apply
```

This creates:
- VPC with public/private subnets
- ECS cluster (Fargate)
- 3 ECS services (API, Solver, Web)
- RDS PostgreSQL (db.t4g.micro)
- ElastiCache Redis (cache.t4g.micro)
- ALB with HTTPS certificate
- ECR repositories
- Security groups and IAM roles

## Step 2: Push Docker images to ECR

```bash
# Login to ECR
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin YOUR_ACCOUNT_ID.dkr.ecr.us-east-1.amazonaws.com

# Build and push
docker build -t YOUR_ACCOUNT_ID.dkr.ecr.us-east-1.amazonaws.com/jobuler-api:latest -f infra/docker/api.Dockerfile apps/api
docker push YOUR_ACCOUNT_ID.dkr.ecr.us-east-1.amazonaws.com/jobuler-api:latest

docker build -t YOUR_ACCOUNT_ID.dkr.ecr.us-east-1.amazonaws.com/jobuler-solver:latest -f infra/docker/solver.Dockerfile apps/solver
docker push YOUR_ACCOUNT_ID.dkr.ecr.us-east-1.amazonaws.com/jobuler-solver:latest

docker build -t YOUR_ACCOUNT_ID.dkr.ecr.us-east-1.amazonaws.com/jobuler-web:latest -f infra/docker/web.Dockerfile apps/web
docker push YOUR_ACCOUNT_ID.dkr.ecr.us-east-1.amazonaws.com/jobuler-web:latest
```

## Step 3: Migrate the database

```bash
# Dump from VPS PostgreSQL
ssh root@VPS_IP "docker exec compose-postgres-1 pg_dump -U jobuler -d jobuler" > backup.sql

# Restore to AWS RDS
psql -h RDS_ENDPOINT -U jobuler -d jobuler < backup.sql
```

## Step 4: Configure GitHub secrets

In your GitHub repo → Settings → Secrets:
- `AWS_ACCESS_KEY_ID`
- `AWS_SECRET_ACCESS_KEY`
- `AWS_ACCOUNT_ID`
- `ECS_SUBNET_ID`
- `ECS_SECURITY_GROUP_ID`
- `NEXT_PUBLIC_API_URL` (your domain)

## Step 5: Switch DNS

Update your domain's A record:
- **Before:** `yourdomain.com → VPS_IP`
- **After:** `yourdomain.com → ALB_DNS_NAME` (CNAME record)

DNS propagation takes 5-60 minutes. During this time, some users hit the old VPS (still running) and some hit the new ECS. Both work — no data loss.

## Step 6: Verify and decommission VPS

1. Verify the app works on the new infrastructure
2. Check the platform dashboard shows correct stats
3. Wait 24 hours for DNS to fully propagate
4. Shut down the VPS

## Rollback

If something goes wrong:
1. Point DNS back to VPS IP
2. VPS is still running with the old data
3. Fix the issue on ECS
4. Try again

## Cost comparison

| | VPS | ECS (idle) | ECS (active) |
|---|---|---|---|
| Monthly | $6 | $50 | $100-300 |
| Auto-scale | No | Yes | Yes |
| High availability | No | Yes | Yes |
| Managed DB | No | Yes (RDS) | Yes |
| Zero downtime deploy | No | Yes | Yes |
