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
---

# S01: Provision and Access a Browser Task Space

- Status: Blocked
- Owner/reviewer: Product owner; technical/security reviewer and privacy/operations owners to be assigned
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
- Prerequisites: Phase 0 of the [roadmap](../../task-manager-implementation-roadmap.md#phase-plan); no prior feature slice. Before implementation, record separate implementation authorization, reconcile the architecture's PRS v0.7 reference to the current PRS v0.8 baseline, and select compatible tooling and API/data/migration/test approaches.
- Environment: Use isolated development/test configuration and disposable test data. Workspace-isolation checks require a real PostgreSQL integration environment; mocks alone do not satisfy them. Never use pilot or production data for development/test.
- Assumptions: The requester-approved account-free, single-browser, no-pairing/no-recovery model and the architecture's React/ASP.NET Core/PostgreSQL direction remain authoritative. Same-origin delivery is preferred by the architecture, but exact hosting and cookie policy are not selected.
- Open questions/blockers:
  1. Technical/security owners must approve credential lifetime, expiry, revocation, and corresponding cookie behavior before implementation; do not invent values.
  2. Privacy/operations owners must assign an owner and timing for purging inaccessible workspaces before pilot.
  3. Security/operations owners must define provisioning and protected-request rate-limit behavior and values before pilot. No numeric limits are specified here.
  4. Product and security reviewers must approve the missing-cookie and first-use experience. A first-use browser and a browser whose credential was cleared are indistinguishable from the server; decide how the user explicitly starts a new space after seeing the no-recovery disclosure.
  5. Technical/security reviewers must approve how concurrent first-use requests and ambiguous retries are correlated so they cannot create unintended duplicate or orphaned workspaces without treating a client value as authorization.
  6. Select cookie SameSite/CSRF/origin policy and exact API, persistence, migration, and test approaches during Phase 0. Implementation authorization remains separate.

## Behavior and contracts

- Inputs and outputs:
  - A user must see, before relying on the task space, that losing, clearing, or expiring the browser credential makes the space inaccessible and that the MVP has no recovery. Do not prescribe an unapproved consent control.
  - A user-initiated new-space flow creates one workspace and issues one unpredictable opaque credential in a Secure, HttpOnly cookie with the approved SameSite policy. A credential failure alone never initiates creation. Do not expose a workspace ID as authority.
  - Protected requests use the cookie credential only; the server validates it and derives workspace scope. No request-body, route, or query workspace ID may grant access.
- Validation, authorization, and errors:
  - Credential verification is enforced server-side for every protected request. Missing, invalid, expired, or revoked credentials do not expose workspace data or reveal whether another workspace exists.
  - Authentication failure must not silently provision a replacement workspace. Creation without a valid credential is only through the reviewed, user-initiated new-space flow after the disclosure; invalid/expired/revoked credential handling must follow the approved lifecycle decision.
  - Return explicit, non-sensitive outcomes for inaccessible workspace, rate limiting, and service failure. Never include a raw credential, verifier, task content, or stack trace in responses or logs.
  - Do not put credentials in URLs, request bodies, JavaScript-readable storage, analytics, screenshots, traces, or logs. Keep the cookie inaccessible to application JavaScript.
  - Apply approved origin checks and CSRF protection to state-changing requests, including initialization. Exact cookie SameSite behavior, limits, and HTTP/API shapes remain implementation decisions.
- State and integrations:
  - Persist a workspace and verifier atomically; persist no raw credential. Do not create a default list or any task data as part of this slice.
  - Prevent credential verifier records or workspace scope from being used to access another workspace. Ensure concurrent initialization/retry behavior meets AC-005 after its correlation contract is approved.
  - Emit only sanitized operational signals needed to observe provisioning, rejection, and abuse-limit outcomes; never record credential material or user task content.
- Compatibility: No task/list behavior or public API exists in this documentation-only repository. Future protected feature operations must use the same validated, server-derived workspace boundary; no client-supplied workspace scope is trusted.
- Migration, observability, and rollback: A reviewed, repeatable migration is required for workspace and verifier state. Provisioning must be transactional. Plan schema compatibility and rollback separately; application rollback does not reverse a database migration. Operational signals must remain sanitized. Exact schema, migration tool, and rollback procedure are not selected.

## Acceptance criteria

- AC-001: Given a first-use browser, when the user reaches the task space, then the no-account/no-recovery consequence is disclosed before the user relies on it, and initialization follows the reviewed first-use flow.
- AC-002: Given an approved first-use initialization, when provisioning succeeds, then exactly one workspace and its verifier are committed atomically, and the raw opaque credential is issued only through a Secure, HttpOnly cookie with the approved SameSite and expiry policy.
- AC-003: Given a valid credential for workspace A, when a protected read or write is attempted for workspace A or B, then the server derives A from the credential and never allows access to B. Real-PostgreSQL integration tests exercise cross-workspace attempts.
- AC-004: Given a missing, invalid, expired, or revoked credential, when a protected request is made, then no workspace data is returned, the error is non-sensitive, and authentication failure does not silently provision a replacement. Expiry/revocation outcomes use the approved lifecycle policy.
- AC-005: Given concurrent first-use requests or an ambiguous retry for one initialization flow, when provisioning completes, then the approved correlation/idempotency behavior prevents unintended duplicate or orphaned active workspaces and never grants authority based on a client-supplied workspace identifier.
- AC-006: Given any initialization or protected request, when security checks inspect browser, HTTP, persistence, and operational surfaces, then raw credentials are absent from URLs, bodies, JavaScript-readable storage, analytics, screenshots, logs, traces, and persisted records; state-changing requests enforce the approved origin/CSRF controls, and approved rate limits produce explicit safe outcomes.
- AC-007: Given the browser workflow's loading, disclosure, and inaccessible/error states, when operated by keyboard, then controls are keyboard-operable, focus is visible, and status/error feedback is accessible without relying on color alone.

## Verification

No application or test project exists, so there is no repository-supported test command or working directory yet. The checks below are planned; test names are provisional, and commands must be added after Phase 0 selects the project/tooling. Cross-workspace and persistence checks require a real, isolated PostgreSQL test database.

| Criterion | Planned test/check | Command and working directory | Expected result |
|-----------|--------------------|-------------------------------|-----------------|
| AC-001 | Proposed browser acceptance check: first-use disclosure precedes reliance on the space | Not available; no browser application or test runner exists | Notice is visible and accurately states no account, pairing, or recovery before the task space is used |
| AC-002 | Proposed provisioning API and PostgreSQL integration tests | Not available; no solution, project, or test runner exists | One committed workspace/verifier pair; raw credential absent from storage; approved cookie attributes present |
| AC-003 | Proposed credential-validation and cross-workspace PostgreSQL integration tests | Not available; no solution, project, or test runner exists | Valid credential reaches only its associated workspace; cross-workspace reads and writes are denied |
| AC-004 | Proposed API tests for missing, invalid, expired, and revoked credentials and safe errors | Not available; no solution, project, or test runner exists | No protected data or workspace-existence disclosure; no implicit replacement workspace; lifecycle cases match the approved policy |
| AC-005 | Proposed PostgreSQL-backed concurrent initialization and ambiguous-retry integration tests | Not available; no solution, project, or test runner exists | Approved correlation behavior prevents unintended duplicate/orphan workspace state |
| AC-006 | Proposed security tests plus review of cookie, request, persistence, logging, origin/CSRF, and rate-limit behavior | Not available; no security/test tooling exists | No credential leakage; state-changing request protections and approved limits are enforced with safe outcomes |
| AC-007 | Proposed keyboard/browser acceptance check | Not available; no browser application or test runner exists | First-use and error workflows are keyboard operable with visible focus and accessible feedback |

## Showcase and completion

1. In an isolated local/test environment with disposable data, open the application in a fresh browser profile and verify the no-recovery disclosure appears before the user relies on the space.
2. Complete the reviewed initialization flow, reload the page, and verify the same space is reached without exposing a workspace identifier or credential to application JavaScript.
3. Use a second isolated profile to initialize a distinct space, then run the approved integration tests to verify neither profile can read or mutate the other's workspace.
4. Remove or invalidate a test credential and verify the protected flow is explicit and does not silently replace the inaccessible space. Do not use production credentials or data.

- Completion evidence: Reviewed Phase 0 decisions; focused tests and real-PostgreSQL isolation evidence mapped to AC-001 through AC-007; sanitized security review; exact test commands and working directory; recorded unresolved risks. No execution evidence exists yet.
- Execution evidence: Not run; this is a blocked specification and the repository has no application/test project.
- Risks: Credential loss is unrecoverable; incorrect first-use behavior can mislead users or create unintended workspaces; workspace isolation failure can expose another anonymous user's data; missing lifecycle, purge, and abuse decisions block pilot readiness.
- Next dependency/handoff: Resolve the blockers above and record implementation authorization separately. Then request the [implement-vertical-slice skill](../../.github/skills/implement-vertical-slice/SKILL.md) with this specification path; specification creation does not authorize implementation.
