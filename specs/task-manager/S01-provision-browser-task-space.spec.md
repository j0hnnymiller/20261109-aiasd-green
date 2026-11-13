---
ai_generated: true
model: "unknown/unknown@unknown"
operator: "jMM-at-CODETraining"
chat_id: "c4961cf2-7fc3-457c-85c0-292912b61606"
prompt: "create an implementation spec for slice 1"
started: "2026-11-13T11:06:59.944-08:00"
ended: "2026-11-13T11:12:01.458-08:00"
task_durations:
  - task: "Source review, S01 specification drafting, and documentation"
    duration: "00:05:00"
total_duration: "00:05:00"
ai_log: "ai-logs/2026/11/13/c4961cf2-7fc3-457c-85c0-292912b61606/conversation.md"
source: "User request; approved task-manager PRS v0.8, architecture, and draft implementation roadmap"
revisions:
  - chat_id: "99477caf-5fba-46a3-a2f8-4813bccd3c7c"
    date: "2026-11-13"
    change: "Recorded the requester's explicit first-use flow and selected 30-day credential expiry, moved purge and numeric rate limits to pre-pilot gates, and made S01 ready for review while preserving security review and implementation-authorization gates."
    ai_log: "ai-logs/2026/11/13/99477caf-5fba-46a3-a2f8-4813bccd3c7c/conversation.md"
  - chat_id: "99477caf-5fba-46a3-a2f8-4813bccd3c7c"
    date: "2026-11-13"
    change: "Recorded requester approval of the S01 specification; implementation authorization and technical/security review remain separate."
    ai_log: "ai-logs/2026/11/13/99477caf-5fba-46a3-a2f8-4813bccd3c7c/conversation.md"
  - chat_id: "d45afcf0-3a5c-43c0-a5f9-91947ac159c3"
    date: "2026-11-13"
    change: "Implemented and verified S01 while preserving the independent technical/security review and pre-pilot gates."
    ai_log: "ai-logs/2026/11/13/d45afcf0-3a5c-43c0-a5f9-91947ac159c3/conversation.md"
---

# S01: Provision and Access a Browser Task Space

- Status: Approved
- Implementation evidence: Code and focused verification are recorded; independent technical/security review remains required before merge.
- Approval: Product owner (requester), 2026-11-13. Approval does not grant implementation authorization.
- Owner/reviewer: Product owner (requester); technical/security reviewer and privacy/operations owners to be assigned
- Sources:
  - [Task Manager PRS](../../task-manager-requirements.md): approved account-free access decision, TM-NFR-01, and CBR-08
  - [Task Manager Architecture](../../task-manager-architecture.md): workspace access, security, and first-visit flow
  - [Vertical-Slice Implementation Roadmap](../../task-manager-implementation-roadmap.md): Phase 0 and S01
  - [ASP.NET Core instructions](../../.github/instructions/aspnet-core.instructions.md)
  - [React/TypeScript instructions](../../.github/instructions/react-typescript.instructions.md)
  - [PostgreSQL instructions](../../.github/instructions/postgresql.instructions.md)
  - [Implementation specification instructions](../../.github/instructions/implementation-specifications.instructions.md)
  - [Implement vertical slice skill](../../.github/skills/implement-vertical-slice/SKILL.md)

## Goal

An individual can initialize and revisit one task space in a browser, while the server derives access only from that browser's valid opaque credential and prevents access to every other workspace. The approved MVP has no account, pairing, credential reset, or recovery.

## Scope and exclusions

- In scope:
  - First-use disclosure and browser workflow for initializing an account-free task space.
  - Atomic workspace and credential-verifier provisioning, opaque credential issuance, protected cookie handling, and credential validation.
  - Server-derived workspace scope for every protected request, with safe authorization failures and cross-workspace isolation.
  - Provisioning and protected-request abuse controls, origin/CSRF protection, sanitized operational signals, and accessible user feedback.
  - Focused unit, API/contract, real-PostgreSQL integration, security, and browser acceptance checks appropriate to this access foundation.
- Out of scope:
  - Task or list workflows, task data, account registration, pairing, credential reset, recovery, offline access, or client-side authorization.
  - Automated deletion or purge of inaccessible workspaces; ownership and timing remain an operations/privacy gate.
  - Backup restore and deleted-task ledger workflows, which belong to later slices and launch gates.
  - Hosting-provider selection, production deployment, or unapproved numeric service and abuse targets.

## Context and dependencies

- Existing implementation and conventions: The repository began with documentation only. S01 now uses a .NET 10 ASP.NET Core minimal API, React 19.3.1/TypeScript 7.0.2/Vite 8.3.8, Npgsql 10.0.3, transactional SQL migration scripts applied with `psql`, xUnit, and Playwright 1.65.0. The hosting provider remains unselected.
- Prerequisites: No prior feature slice. Issue assignment on 2026-11-13 explicitly authorized this implementation only; independent technical/security review remains required before merge. The architecture source reference is aligned to PRS v0.8.
- Environment: Use isolated development/test configuration and disposable test data. Workspace-isolation checks require a real PostgreSQL integration environment; mocks alone do not satisfy them. Never use pilot or production data for development/test.
- Assumptions: The requester-approved account-free, single-browser, no-pairing/no-recovery model and the architecture's React/ASP.NET Core/PostgreSQL direction remain authoritative. The requester selected a persistent Secure, HttpOnly cookie with a 30-day absolute expiry and no user reset/recovery. Same-origin delivery is preferred by the architecture; the implemented SameSite, Origin, CSRF, activation, and retry choices require independent security review before merge.
- Recorded product decisions and operational gates:
  1. After showing the no-recovery disclosure, require a separate explicit "Create new task space" action. A missing, invalid, or expired credential never silently creates a replacement space.
  2. Use a persistent Secure, HttpOnly cookie with a 30-day absolute expiry. Reject expired credentials; there is no user reset/recovery. Revocation of an activated credential must use an approved operations procedure; duplicate/retry compensation applies only to unconfirmed initialization credentials and requires security review.
  3. S01 includes configurable provisioning/protected-request abuse controls and safe outcomes. Numeric rate-limit values are a pre-pilot gate, not a blocker to S01 specification review.
  4. Purge ownership and timing for inaccessible workspaces are a pre-pilot privacy/operations gate, not an S01 implementation behavior.
- Remaining review gates:
  1. Obtain independent technical/security approval for the 30-day credential lifecycle, SameSite `Strict`, exact-Origin/CSRF policy, and the UUIDv4 initialization-key/confirmation/cancellation behavior before merge.
  2. Confirm the operations procedure for revoking activated credentials before pilot. No user reset/recovery or application revocation endpoint exists.
  3. Approve numeric abuse limits and assign an owner/timing for inaccessible-workspace purge before pilot.
  4. Decide trusted-proxy/forwarded-IP handling and production TLS/origin configuration before deployment. No production deployment is authorized.

## Behavior and contracts

- Inputs and outputs:
  - Before creating a space, show that losing, clearing, or expiring the browser credential makes it inaccessible and that the MVP has no recovery. Require the user to explicitly choose "Create new task space"; do not add a separate consent checkbox.
  - Only that explicit action creates a workspace and issues an unpredictable opaque credential in a persistent Secure, HttpOnly cookie with a 30-day absolute expiry; server-side verifier expiry must match. A missing, invalid, expired, or revoked credential alone never initiates creation. Do not expose a workspace ID as authority.
  - Protected requests use the cookie credential only; the server validates it and derives workspace scope. No request-body, route, or query workspace ID may grant access.
- Validation, authorization, and errors:
  - Credential verification is enforced server-side for every protected request. Missing, invalid, expired, or revoked credentials do not expose workspace data or reveal whether another workspace exists. The server rejects credentials after the 30-day absolute expiry.
  - Authentication failure must not silently provision a replacement workspace. Creation without a valid credential is only through the explicit, user-initiated new-space flow after the disclosure. No user reset or recovery is provided; server-side revocation uses an approved operations procedure.
  - Return explicit, non-sensitive outcomes for inaccessible workspace, rate limiting, and service failure. Never include a raw credential, verifier, task content, or stack trace in responses or logs.
  - Do not put credentials in URLs, request bodies, JavaScript-readable storage, analytics, screenshots, traces, or logs. Keep the cookie inaccessible to application JavaScript.
  - Apply technical/security-approved cookie SameSite settings, origin checks, and CSRF protection to state-changing requests, including initialization. Abuse controls are configurable and return explicit safe outcomes; numeric limits are approved before pilot. HTTP/API shapes remain implementation decisions.
- State and integrations:
  - Persist a workspace and verifier atomically; persist no raw credential. Do not create a default list or any task data as part of this slice.
  - Prevent credential verifier records or workspace scope from being used to access another workspace. Repeated or concurrent initialization requests for one flow must not create unintended duplicate or orphaned spaces. Review the server-side correlation/idempotency mechanism before implementation; it is not authorization.
  - Emit only sanitized operational signals needed to observe provisioning, rejection, and abuse-limit outcomes; never record credential material or user task content.
- Compatibility: No task/list behavior or public API exists in this documentation-only repository. Future protected feature operations must use the same validated, server-derived workspace boundary; no client-supplied workspace scope is trusted.
- Migration, observability, and rollback: A reviewed, repeatable migration is required for workspace and verifier state. Provisioning must be transactional. Plan schema compatibility and rollback separately; application rollback does not reverse a database migration. Operational signals must remain sanitized. Exact schema, migration tool, and rollback procedure are not selected.

## Acceptance criteria

- AC-001: Given a browser without a valid credential, when the user opens the task manager, then the no-account/no-recovery consequence and a separate explicit "Create new task space" action are shown, and no workspace is created until the user selects that action.
- AC-002: Given an approved first-use initialization, when provisioning succeeds, then exactly one workspace and its verifier are committed atomically, the server-side verifier and persistent Secure, HttpOnly cookie expire after 30 days, and cookie SameSite settings pass technical/security review.
- AC-003: Given a valid credential for workspace A, when a protected read or write is attempted for workspace A or B, then the server derives A from the credential and never allows access to B. Real-PostgreSQL integration tests exercise cross-workspace attempts.
- AC-004: Given a missing, invalid, expired, or revoked credential, when a protected request is made, then no workspace data is returned, the error is non-sensitive, and authentication failure does not silently provision a replacement. Credentials are rejected after 30 days; there is no user reset/recovery, and revocation follows the approved operations procedure.
- AC-005: Given concurrent first-use requests or an ambiguous retry for one initialization flow, when provisioning completes, then server-side correlation prevents unintended duplicate or orphaned active workspaces and never grants authority based on a client-supplied workspace identifier.
- AC-006: Given any initialization or protected request, when security checks inspect browser, HTTP, persistence, and operational surfaces, then raw credentials are absent from URLs, bodies, JavaScript-readable storage, analytics, screenshots, logs, traces, and persisted records; state-changing requests enforce reviewed origin/CSRF controls, and configurable abuse controls produce explicit safe outcomes. Numeric limits are set before pilot.
- AC-007: Given the browser workflow's loading, disclosure, and inaccessible/error states, when operated by keyboard, then controls are keyboard-operable, focus is visible, and status/error feedback is accessible without relying on color alone.

## Verification

The application and test projects were scaffolded for the approved stack. Cross-workspace and persistence checks use a real, isolated PostgreSQL database; mocks are not substituted.

| Criterion | Planned test/check | Command and working directory | Expected result |
|-----------|--------------------|-------------------------------|-----------------|
| AC-001 | `PostgresIntegrationTests.First_visit_requires_explicit_creation_and_issues_only_a_protected_cookie`; browser first-use test | `TASK_MANAGER_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=54329;Database=task_manager_test;Username=postgres' dotnet test tests/TaskManager.Api.Tests/TaskManager.Api.Tests.csproj`; `npm run test:e2e --prefix src/client` (repository root) | Passed: initial protected read creates nothing; browser sends no provisioning request before explicit selection |
| AC-002 | PostgreSQL API test above verifies one workspace/verifier pair and 30-day server expiry; cookie assertions check Secure, HttpOnly, Path, no Domain, and SameSite; browser confirms receipt before displaying ready; independent review remains open | Same commands as AC-001 | Passed for persistence, expiry, and attributes; SameSite `Strict` is selected but technical/security approval is pending |
| AC-003 | `PostgresIntegrationTests.Protected_write_uses_credential_workspace_and_ignores_client_workspace_ids`; code review of the credential-derived read boundary | PostgreSQL `dotnet test` command above (repository root) | Passed: real-PostgreSQL write with workspace B in query/body changes only workspace A derived from its credential; S01 contains no task/list data API |
| AC-004 | `PostgresIntegrationTests.Missing_invalid_expired_and_revoked_credentials_never_provision_replacements` | PostgreSQL `dotnet test` command above (repository root) | Passed: missing, malformed, expired, and revoked credentials receive 401; no replacement workspace is created |
| AC-005 | `PostgresIntegrationTests.Duplicate_initialization_key_revokes_the_unconfirmed_workspace_without_creating_another`, `Concurrent_initialization_requests_do_not_leave_duplicate_active_workspaces`, `Repeated_initialization_with_the_issued_cookie_is_idempotent`, `Duplicate_initialization_without_a_cookie_does_not_revoke_an_activated_credential`, `Cancelled_duplicate_initialization_cleans_up_the_unconfirmed_attempt`, and `Concurrent_confirmation_requests_are_idempotent` | PostgreSQL `dotnet test` command above (repository root) | Passed: concurrent and ambiguous unauthenticated retries create no duplicate active workspace; valid-cookie retry and confirmation are idempotent, cancellation compensation completes, and duplicate requests cannot revoke a confirmed credential |
| AC-006 | PostgreSQL tests for Origin rejection and configurable provisioning/protected-request 429 outcomes; browser test verifies empty initialization body and JavaScript cannot read the credential; source review of URL/storage/logging surfaces | PostgreSQL `dotnet test` and `npm run test:e2e --prefix src/client` (repository root) | Passed for automated checks. Independent review of SameSite/Origin/CSRF and duplicate-attempt compensation remains required; numeric pilot limits remain unapproved |
| AC-007 | Playwright keyboard/focus/progress test and unavailable-service feedback test | `npm run test:e2e --prefix src/client` (repository root; Chromium installed) | Passed: create flow is keyboard-operable, focus is visible, and status/error feedback is announced |

### Execution evidence

Prerequisites: .NET 10 SDK, Node 22/npm, Docker, Chromium (`npx playwright install chromium`), and a disposable PostgreSQL 16 database. The test database was loopback-bound and used only for this verification.

- `dotnet test tests/TaskManager.Api.Tests/TaskManager.Api.Tests.csproj` with `TASK_MANAGER_TEST_CONNECTION_STRING=Host=127.0.0.1;Port=54329;Database=task_manager_test;Username=postgres` - passed 14 tests, 0 skipped, against PostgreSQL.
- `npm run lint --prefix src/client` - passed.
- `npm run build --prefix src/client` - passed.
- `npm run test:e2e --prefix src/client` with the same test connection string and the migration applied to the disposable database - passed 4 Chromium tests.
- Applied `001_create_workspace_tables.sql` twice to a disposable schema with `psql -v ON_ERROR_STOP=1` - both runs completed successfully.

The exact setup commands and implementation choices are also documented in the root [README](../../README.md). These automated results do not complete the required independent technical/security review. Numeric abuse limits and inaccessible-workspace purge owner/timing remain pre-pilot gates.

## Showcase and completion

1. In an isolated local/test environment with disposable data, open the application in a fresh browser profile and verify the no-recovery disclosure appears with a separate "Create new task space" action.
2. Explicitly create the space, reload the page, and verify the same space is reached without exposing a workspace identifier or credential to application JavaScript.
3. Use a second isolated profile to initialize a distinct space, then run the approved integration tests to verify neither profile can read or mutate the other's workspace.
4. Remove or invalidate a test credential and verify the protected flow is explicit and does not silently replace the inaccessible space. Do not use production credentials or data.

- Completion evidence: Focused API, browser, and real-PostgreSQL evidence mapped to AC-001 through AC-007 is recorded above. Independent technical/security review of SameSite, Origin/CSRF, correlation/retry compensation, and sanitized operational signals remains required before merge. Before pilot, record numeric abuse limits and inaccessible-workspace purge owner/timing.
- Execution evidence: See the command and result records above.
- Risks: Credential loss or expiry is unrecoverable; incorrect first-use behavior can mislead users or create unintended workspaces; workspace isolation failure can expose another anonymous user's data. The 30-day lifetime is the requester's selected policy pending technical/security validation.
- Next dependency/handoff: Request independent technical/security review before merge. Resolve forwarded-IP handling, numeric abuse limits, and inaccessible-workspace purge ownership/timing before pilot. No production deployment is authorized.
