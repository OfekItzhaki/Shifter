# The Horizon Standard
## Universal Architecture & Excellence Blueprint

The Horizon Standard is a universal set of architectural principles and coding standards for software projects of any stack or platform. It is intended for both humans and AI agents to improve consistency, scalability, maintainability, and delivery quality.

## 1. Core Principles
- Prefer correctness, clarity, maintainability, and testability over cleverness.
- Favor small, safe, reviewable changes.
- Preserve existing architecture unless there is a clear reason to improve it.
- Reuse existing patterns before introducing new ones.
- Avoid unnecessary complexity and speculative refactors.
- Keep public interfaces stable unless change is required.
- Update nearby documentation when behavior or setup changes.

## 2. Single Source of Truth
- Use API contracts as the source of truth for client generation when applicable.
- Prefer OpenAPI, NSwag, or equivalent tooling for generating clients and schemas.
- Re-run generators whenever backend contracts change.
- Never manually define client models if generated contracts are available.
- Never hardcode dynamic content in code when a CMS or config source is appropriate.

## 3. Error Handling
- Use centralized error handling patterns.
- Avoid scattered try-catch blocks unless a local catch is required for specific logic.
- Return standardized error shapes where the stack supports it.
- Surface failures clearly in the frontend through a central notification or error system.
- Avoid silent failures.

## 4. Infrastructure and Setup
- Prefer container-first development for core services when the project uses containers.
- Use infrastructure-as-code tools and environment files for configuration.
- Local development should be as close to plug-and-play as practical.
- Automate repetitive setup, seeding, verification, and service startup tasks.
- Handle common environment conflicts automatically where possible.
- Clean up orphaned or zombie services when scripts are expected to do so.

## 5. Background Work and Real-Time
- Move slow operations out of the request-response cycle.
- Use queues or background workers for email, sync jobs, and heavy processing.
- Make jobs retriable and traceable.
- Use real-time tech only where it adds clear value.
- Keep REST fallbacks available for critical flows when relevant.

## 6. State, Caching, and Sessions
- Use robust data-fetching and caching patterns for client data.
- Keep caching strategies consistent across the frontend.
- Handle short-lived sessions gracefully.
- Renew tokens automatically when the stack supports refresh flows.
- Avoid disrupting users because a token expired.

## 7. Observability
- Use structured logging.
- Include context in logs.
- Use centralized log aggregation when the system is production-facing.
- Provide health endpoints where services need orchestration or monitoring.
- Use retries and circuit breakers for transient infrastructure failures.
- Persist infrastructure data appropriately across restarts.

## 8. Storage
- Access storage through abstractions rather than direct ad hoc calls.
- Support multiple storage providers when the product needs portability.
- Centralize path and URL resolution.
- Keep local development and production storage behavior predictable.

## 9. Git and Collaboration
- Keep commits atomic and logically scoped.
- Use conventional commits in the form `type(scope): description`.
- Prefer squash merges for feature branches unless the repo documents another strategy.
- Do not mix unrelated changes in one commit.
- Do not rewrite git history unless explicitly asked.
- Keep branches clear and purpose-driven.
- Review code constructively and focus on standards, correctness, and maintainability.

## 10. Security
- Never hardcode secrets, keys, tokens, passwords, or connection strings.
- Never commit secrets or sensitive data.
- Never log secrets or PII.
- Validate and sanitize inputs at boundaries.
- Use parameterized queries or safe ORM patterns.
- Use standard auth and authorization mechanisms.
- Apply security headers where relevant.
- Update dependencies regularly and scan for vulnerabilities.

## 11. Performance
- Optimize for the user-facing paths that matter.
- Use caching, indexing, pagination, and lazy loading where appropriate.
- Avoid obvious bottlenecks and N+1 patterns.
- Keep response times and startup behavior practical for the product.
- Measure before making major performance changes.

## 12. Documentation
- Every project should have a useful README.
- Document setup, usage, testing, deployment, and contribution basics.
- Use code comments only for non-obvious logic.
- Use API documentation tooling when applicable.
- Maintain architecture notes or ADRs for major decisions.

## 13. Naming and Style
- Use descriptive names.
- Avoid unnecessary abbreviations.
- Follow the existing style in the codebase.
- Use standard naming conventions for the stack.
- Keep TypeScript strongly typed and avoid `any` except where justified or generated.
- Keep C#, Python, and other languages strongly typed where possible.

## 14. Architecture Decision Guidance
- Choose technologies based on project fit, not novelty.
- Prefer PostgreSQL for transactional relational data when there is no stronger constraint.
- Use document, key-value, graph, or time-series stores only when the use case justifies it.
- Use REST for simple CRUD and public APIs.
- Use GraphQL, gRPC, or real-time protocols when the problem requires them.
- Use queues for decoupling and async work.
- Use object storage for files, media, and backups when appropriate.

## 15. Testing
- Aim for strong coverage of business logic.
- Write tests that describe behavior clearly.
- Keep tests isolated and readable.
- Use unit tests for logic, integration tests for boundary behavior, and E2E tests for critical journeys.
- Follow AAA structure where helpful.
- Add or update tests when behavior changes.

## 16. Definition of Done
A task is done only when the relevant checks are satisfied:
- Code follows project conventions and architecture.
- Validation and error handling are in place.
- Linting or static checks pass or are explicitly called out.
- Build or type-check passes or is explicitly called out.
- Relevant tests pass or are explicitly called out.
- Documentation is updated if needed.
- No secrets or unsafe debug code were introduced.
- Changes are summarized clearly.

## 17. Practical Workflow
Before starting work:
- Read the repository instructions.
- Review existing code and patterns.
- Identify the relevant stack and affected layers.
- Ask clarifying questions if the task is ambiguous.

During work:
- Start with the simplest correct solution.
- Make incremental changes.
- Verify each meaningful step.
- Keep changes scoped.

Before finishing:
- Run the narrowest meaningful checks.
- Confirm the implementation matches the request.
- Summarize what changed and what remains.

## 18. Project-Specific Additions
Each repository may add its own local instructions, scripts, commands, and exceptions in its root AGENTS.md or deeper scoped instruction files.