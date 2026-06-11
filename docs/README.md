# Shifter Docs

Use this page as the starting menu for setup, deployment, security, AI, and
manual self-service scheduling decisions.

## Product And Operations

- [Manual self-service scheduling](MANUAL-SELF-SERVICE-SCHEDULING.md) - how to
  run groups where members pick, change, swap, and report shifts themselves.
- [Architecture](ARCHITECTURE.md) - current system architecture and service
  boundaries.
- [Data model](DATA-MODEL.md) - major entities and persistence structure.
- [Backlog](BACKLOG.md) - tracked future work and product gaps.

## Setup And Testing

- [Local setup](LOCAL-SETUP.md) - local development with or without Docker.
- [Testing](testing/) - test notes and verification material.
- [Implementation steps](steps/) - historical step docs for feature work.

## Deployment

- [Customer-hosted deployment](CUSTOMER-HOSTED-DEPLOYMENT.md) - first supported
  package for installs inside a customer's infrastructure.
- [AI deployment modes](AI-DEPLOYMENT-MODES.md) - hosted, customer-managed, and
  no-export local AI choices.
- [VPS to ECS migration](MIGRATION-VPS-TO-ECS.md) - cloud migration notes.
- [Cloudflare front door plan](../infra/CLOUDFLARE_FRONT_DOOR.md) - DNS, CDN,
  WAF, DDoS, and rate-limit plan for the public web domain.
- [Staging and previews](../infra/STAGING_AND_PREVIEWS.md) - staging and preview
  environment notes.

## Standards

- [The Horizon Standard](The-Horizon-Standard.md) - engineering and delivery
  expectations for the project.
