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
---

# S01: Provision and Access a Browser Task Space

- Status: Approved
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

- Existing implementation and conventions: The repository currently contains product/architecture/roadmap documentation, instructions, and chat records; no application source, solution/project, package manifest, or test project was found. Approved stack direction is React/TypeScript, ASP.NET Core, and PostgreSQL. Exact versions, API style, data-access, migration, test, and hosting tools are unselected.
- Prerequisites: Phase 0 of the [roadmap](../../task-manager-implementation-roadmap.md#phase-plan); no prior feature slice. Before implementation, complete technical/security review of the decisions below, record separate implementation authorization, and select compatible tooling and API/data/migration/test approaches. The architecture source reference is aligned to PRS v0.8.
- Environment: Use isolated development/test configuration and disposable test data. Workspace-isolation checks require a real PostgreSQL integration environment; mocks alone do not satisfy them. Never use pilot or production data for development/test.
- Assumptions: The requester-approved account-free, single-browser, no-pairing/no-recovery model and the architecture's React/ASP.NET Core/PostgreSQL direction remain authoritative. The requester selected a persistent Secure, HttpOnly cookie with a 30-day absolute expiry and no user reset/recovery; technical/security review is still required. Same-origin delivery is preferred by the architecture; SameSite, origin, and CSRF controls require security review.
- Recorded product decisions and operational gates:
  1. After showing the no-recovery disclosure, require a separate explicit "Create new task space" action. A missing, invalid, or expired credential never silently creates a replacement space.
  2. Use a persistent Secure, HttpOnly cookie with a 30-day absolute expiry. Reject expired credentials; there is no user reset/recovery. Server-side revocation must use an approved operations procedure. These choices require technical/security validation before implementation.
  3. S01 includes configurable provisioning/protected-request abuse controls and safe outcomes. Numeric rate-limit values are a pre-pilot gate, not a blocker to S01 specification review.
  4. Purge ownership and timing for inaccessible workspaces are a pre-pilot privacy/operations gate, not an S01 implementation behavior.
- Open questions/blockers before implementation:
  1. Assign technical/security reviewers to validate credential expiry/revocation, cookie SameSite, origin/CSRF controls, and the initialization/retry design.
  2. Approve a server-side idempotency/correlation design for concurrent first-use requests and ambiguous retries. The mechanism must not use a client-supplied workspace identifier or treat a correlation value as authorization.
  3. Select compatible versions and exact API, persistence, migration, and test approaches. The repository has no application or test project, so executable commands remain unavailable until this is done.
  4. Record implementation authorization separately. This specification being ready for review does not authorize code work.

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

No application or test project exists, so there is no repository-supported test command or working directory yet. The checks below are planned; test names are provisional, and commands must be added after Phase 0 selects the project/tooling. Cross-workspace and persistence checks require a real, isolated PostgreSQL test database.

| Criterion | Planned test/check | Command and working directory | Expected result |
|-----------|--------------------|-------------------------------|-----------------|
| AC-001 | Proposed browser acceptance check: explicit first-use action follows disclosure | Not available; no browser application or test runner exists | No-recovery disclosure and create action appear; no workspace is created until selection |
| AC-002 | Proposed provisioning API and PostgreSQL integration tests | Not available; no solution, project, or test runner exists | One committed workspace/verifier pair; raw credential absent from storage; cookie and server verifier expire after 30 days |
| AC-003 | Proposed credential-validation and cross-workspace PostgreSQL integration tests | Not available; no solution, project, or test runner exists | Valid credential reaches only its associated workspace; cross-workspace reads and writes are denied |
| AC-004 | Proposed API tests for missing, invalid, expired, and revoked credentials and safe errors | Not available; no solution, project, or test runner exists | No protected data or workspace-existence disclosure; no implicit replacement workspace; credentials are rejected at/after expiry |
| AC-005 | Proposed PostgreSQL-backed concurrent initialization and ambiguous-retry integration tests | Not available; no solution, project, or test runner exists | Server-side correlation prevents unintended duplicate/orphan workspace state without acting as authorization |
| AC-006 | Proposed security tests plus review of cookie, request, persistence, logging, origin/CSRF, and abuse-control behavior | Not available; no security/test tooling exists | No credential leakage; reviewed request protections and configurable controls produce safe outcomes; numeric limits are approved before pilot |
| AC-007 | Proposed keyboard/browser acceptance check | Not available; no browser application or test runner exists | First-use and error workflows are keyboard operable with visible focus and accessible feedback |

## Showcase and completion

1. In an isolated local/test environment with disposable data, open the application in a fresh browser profile and verify the no-recovery disclosure appears with a separate "Create new task space" action.
2. Explicitly create the space, reload the page, and verify the same space is reached without exposing a workspace identifier or credential to application JavaScript.
3. Use a second isolated profile to initialize a distinct space, then run the approved integration tests to verify neither profile can read or mutate the other's workspace.
4. Remove or invalidate a test credential and verify the protected flow is explicit and does not silently replace the inaccessible space. Do not use production credentials or data.

- Completion evidence: Technical/security review of the credential, cookie, CSRF/origin, and correlation decisions; focused tests and real-PostgreSQL isolation evidence mapped to AC-001 through AC-007; sanitized security review; exact test commands and working directory. Before pilot, record numeric abuse limits and inaccessible-workspace purge owner/timing. No execution evidence exists yet.
- Execution evidence: Not run; this is a specification awaiting review, and the repository has no application/test project.
- Risks: Credential loss or expiry is unrecoverable; incorrect first-use behavior can mislead users or create unintended workspaces; workspace isolation failure can expose another anonymous user's data. The 30-day lifetime is the requester's selected policy pending technical/security validation.
- Next dependency/handoff: Assign reviewers and complete technical/security review, select compatible tooling and test approaches, and record implementation authorization separately. Then request the [implement-vertical-slice skill](../../.github/skills/implement-vertical-slice/SKILL.md) with this specification path; specification review does not authorize implementation.
