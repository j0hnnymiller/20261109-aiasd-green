---
ai_generated: true
model: "unknown/unknown@unknown"
operator: "jMM-at-CODETraining"
chat_id: "02051eeb-6a3e-4203-977d-fcff87be0ec9"
prompt: "create an implementation spec for slice 2"
started: "2026-11-13T12:47:00.774-08:00"
ended: "2026-11-13T12:51:46.071-08:00"
task_durations:
  - task: "Drafting and validating the S02 implementation specification"
    duration: "00:04:45"
total_duration: "00:04:45"
ai_log: "ai-logs/2026/11/13/02051eeb-6a3e-4203-977d-fcff87be0ec9/conversation.md"
source: "User request; approved task-manager PRS v0.8, architecture, and draft implementation roadmap"
---

# S02: Create and View Tasks

- Status: Draft
- Owner/reviewer: Product owner (requester); engineering reviewer to be assigned
- Sources:
  - [Task Manager PRS v0.8](../../task-manager-requirements.md): TM-FR-01, TM-FR-02, TM-NFR-01, TM-NFR-02, TM-NFR-03, CBR-01, CBR-03, and CBR-08
  - [Task Manager Architecture](../../task-manager-architecture.md): Task feature, proposed data model, API and consistency behavior
  - [Vertical-Slice Implementation Roadmap](../../task-manager-implementation-roadmap.md#s02-create-and-view-tasks): S02 scope, acceptance, and dependencies
  - [S01: Provision and Access a Browser Task Space](S01-provision-browser-task-space.spec.md)
  - [Business rules to vertical slices](../../.github/instructions/business-rules-to-vertical-slices.instructions.md)
  - [Implementation specification instructions](../../.github/instructions/implementation-specifications.instructions.md)
  - [Vertical-slice implementation instructions](../../.github/instructions/vertical-slice-implementation.instructions.md)
  - [ASP.NET Core instructions](../../.github/instructions/aspnet-core.instructions.md)
  - [React and TypeScript instructions](../../.github/instructions/react-typescript.instructions.md)
  - [PostgreSQL instructions](../../.github/instructions/postgresql.instructions.md)
  - [Test-driven development instructions](../../.github/instructions/test-driven-development.instructions.md)
  - [Implement vertical slice skill](../../.github/skills/implement-vertical-slice/SKILL.md)

## Goal

An individual can create a task in their browser task space and later see the saved task and its required details, including after a page refresh. The server persists the task under the workspace derived from the valid browser credential.

## Scope and exclusions

- In scope:
  - Create a task with a required title and optional description and date-only due date.
  - Persist the task as unassigned, with the approved Normal priority default; creation does not expose priority selection.
  - Read and display every task in the authenticated workspace, including its title and completion state and any supplied due date. Make a supplied description available to the user.
  - Explicit loading, empty, validation, save-failure, and read-failure behavior; preserve entered form values on failed creation.
  - Workspace-scoped API and PostgreSQL persistence, migration, and focused unit, API/contract, real-PostgreSQL integration, and browser checks.
  - Accessible keyboard operation and visible focus for the create and view flows.
- Out of scope:
  - Workspace provisioning, credential issuance/recovery, or changes to S01 access behavior.
  - Editing, completing/reopening, or deleting tasks (S03 and S08); named lists and assignment (S04); priority selection/display (S05); overdue calculation (S06); filters and search (S07).
  - Task ordering policy, priority sorting, reminders, recurring tasks, tags, import/export, offline writes, polling, or live updates.
  - Hosting-provider selection, production deployment, and numeric service or abuse limits.

## Context and dependencies

- Existing implementation and conventions: The current `main` branch tracks the requirements, architecture, roadmap, instructions, and S01 specification, but no application source, solution/project, package manifest, or test project. The stack direction is React/TypeScript, ASP.NET Core, and PostgreSQL. Exact versions, API style, data-access, migration, and test libraries are not selected.
- Prerequisites: S01 must be implemented and its protected workspace access available before S02 integration or browser checks. See [S01](S01-provision-browser-task-space.spec.md). S01 implementation authorization does not authorize S02; obtain separate implementation authorization after this specification is reviewed.
- Environment: Use isolated development/test configuration, disposable test data, and a real PostgreSQL integration environment for persistence and workspace-isolation checks. Never use pilot or production task data for tests.
- Verified constraints:
  - The browser credential is HttpOnly and server-side workspace scope is derived from it; a workspace ID supplied by the client is never authorization.
  - Tasks may be unassigned; no default list is created. Due dates are date-only values, not UTC timestamps.
  - New tasks default to Normal priority under TM-FR-09. Priority selection and display belong to S05.
  - Protected reads and writes return explicit, non-sensitive failures; a failed task read must not appear as a successful empty result.
- Assumptions:
  - A newly created task starts open because create does not accept a completion value and completion changes are assigned to S03. Product-owner confirmation is required.
  - A user can access the saved description from the task view, although TM-FR-02 does not prescribe where it appears.
- Open questions/blockers:
  - Product owner: confirm that newly created tasks start open.
  - Product owner: define title normalization and any title/description length limits. The requirements only reject empty or whitespace-only titles; do not invent a limit or silently rewrite a nonblank title.
  - Product owner: confirm whether an omitted or empty description is represented as absent, null, or empty text. This must not change the title validation rule.
  - Engineering reviewer: select API route/method and exact request/response and validation-error shapes, migration/data-access approach, and task-create retry/idempotency mechanism. Non-idempotent creates must not duplicate a task when a response is ambiguous and the browser retries.
  - Engineering reviewer: select compatible tool versions and test tooling; this repository currently has no executable application/test commands.
  - Roadmap: complete Phase 0 choices and the independent technical/security review required before merge/deployment. Numeric rate limits remain a pre-pilot gate.
- Use-case source: No separate use-case artifact exists. The user outcome and flows are traced to the approved PRS and roadmap above.

## Behavior and contracts

- Inputs and outputs:
  - Conceptual create input: required title; optional description; optional date-only due date in an agreed API representation. The exact property names, JSON omission/null behavior, route, and HTTP method are implementation decisions.
  - Do not accept a workspace identifier or browser credential in the request body. The browser sends the S01-managed cookie; the server validates it and derives the workspace.
  - On success, return or refresh the persisted task representation with a stable task identifier, title, saved description and due date when supplied, completion state, and the task version needed by S03. Do not return workspace credentials or use a task identifier alone as authorization.
  - The workspace task view includes all saved open and completed tasks. Show each task's title and completion state and any supplied due date; make a supplied description accessible. Do not add filter, search, or ordering promises in this slice.
- Validation, authorization, and errors:
  - Reject a missing, empty, or whitespace-only title at both the user interface and API boundary. Do not persist a task and provide actionable correction feedback.
  - For any other title/description length or normalization rule, follow only a decision recorded by the product owner; do not add an unapproved limit.
  - Every create and read requires the valid S01 browser credential and server-derived workspace scope. Missing, invalid, expired, or revoked credentials do not expose task data and do not silently create a replacement workspace.
  - Return distinct, non-sensitive validation, inaccessible-workspace, rate-limit, and service-failure outcomes. Do not include task content, credentials, or stack traces in logs or errors.
  - On failed create, preserve the entered values and allow correction or retry. Make retries safe against an ambiguous successful response so a single user action does not create duplicates.
  - On failed read, retain any previously confirmed task view and show an explicit stale/error state; never substitute an empty list for a failure.
- State and integrations:
  - Persist tasks in PostgreSQL under the authenticated workspace. Store due dates as date-only values. New tasks are unassigned, and the create flow does not create a default list.
  - Initialize the task version for later version-checked updates. Apply the approved Normal priority default without adding priority UI.
  - Emit only sanitized operational signals. Do not log task content, credentials, or sensitive request bodies/URLs.
  - Use the selected typed API boundary and persistence conventions once the implementation choices are approved. Parameterize database values and enforce workspace ownership in both application queries and database constraints.
- Compatibility:
  - Preserve S01 credential and workspace-isolation behavior. Never trust a client-supplied workspace scope.
  - Fetch current server state when the app opens, when the user returns, and on explicit refresh. Do not add polling, push updates, or hidden offline writes.
  - Keep due dates as date-only values; S06 owns overdue calculation and the unresolved interpretation of the user's current local date.
- Migration, observability, and rollback:
  - Add a reviewed, repeatable migration for task persistence and its workspace relationship; use database constraints consistent with the final approved title rules. Keep migration and application deployment compatibility explicit.
  - Creation is a single-record change; use a transaction where required by the selected persistence approach and ensure a failed request cannot leave a partial task.
  - Record sanitized create/read success and failure signals only as needed. Plan schema rollback separately; application rollback does not reverse a database migration. Exact schema and rollback procedure remain unselected.

## Acceptance criteria

- AC-001: Given a valid S01 task space, when the user opens the task creation flow, then a labeled title field and optional description and date-only due-date inputs are available and usable by keyboard.
- AC-002: Given a missing, empty, or whitespace-only title, when the user submits a task, then the UI and API reject it, no task is persisted, and actionable validation feedback is shown without discarding the entered form values.
- AC-003: Given a valid nonblank title and optional description/date, when the user submits the task, then one task is persisted in the credential-derived workspace with the submitted values, no list association, the approved Normal priority default, a stable identifier, an initialized version, and the product-owner-confirmed initial completion state.
- AC-004: Given open and completed saved tasks in the workspace, when the user views tasks, then each task displays its correct title and completion state and any saved due date; a saved description is available to the user. This slice does not provide completion controls.
- AC-005: Given a workspace with no saved tasks, when its task view loads successfully, then the user sees an empty state distinct from loading and error states.
- AC-006: Given a successfully created task, when the user refreshes or leaves and returns to the same task space, then the saved task and its values remain visible without creating a duplicate.
- AC-007: Given credentials for two workspaces, when each workspace reads or creates tasks, then each sees only its own task data; cross-workspace attempts return a non-sensitive denial and never reveal whether another workspace or task exists. Verify this with real PostgreSQL integration tests.
- AC-008: Given a create or read request that fails validation, authorization, rate limiting, or service processing, when the UI handles the result, then it shows an explicit failure state; create values and previously confirmed view data are preserved as applicable, and no failure is rendered as success or as an empty result.
- AC-009: Given a create request whose server outcome is successful but whose response is ambiguous to the browser, when the same user action is retried using the approved retry mechanism, then only one task is created.
- AC-010: Given a task with a date-only due date, when it is saved and later viewed, then its calendar date is unchanged and is not converted through UTC or marked overdue by this slice.
- AC-011: Given the create and view flows, when operated with a keyboard, then all controls are reachable and operable, focus is visible, and validation, loading, success, empty, and error states do not rely on color alone.

## Verification

No application or test project exists on the current `main` branch, so there is no repository-supported executable command or working directory yet. The checks below are planned, test names are provisional, and exact commands must be added after Phase 0 selects the project and test tooling. PostgreSQL integration checks require an isolated real PostgreSQL database; mocks alone do not prove persistence or workspace isolation.

| Criterion | Planned test/check | Command and working directory | Expected result |
|-----------|--------------------|-------------------------------|-----------------|
| AC-001, AC-011 | Proposed browser acceptance check for labeled fields, keyboard operation, and visible focus | Not available; no browser application or test runner exists | Inputs are labeled, reachable, operable, and focused visibly |
| AC-002 | Proposed UI validation and API/contract test for missing, empty, and whitespace-only titles | Not available; no application or test runner exists | Each invalid title is rejected; no task is stored; correction feedback and input values remain |
| AC-003 | Proposed create API and PostgreSQL integration tests | Not available; no solution, project, migration, or test runner exists | Exactly one workspace-scoped record is committed with optional values, unassigned relationship, Normal priority, stable ID, and initialized version |
| AC-004 | Proposed browser/API view checks with open and completed fixtures | Not available; no browser application or test runner exists | The view matches saved title, state, date, and description without exposing state-change controls |
| AC-005 | Proposed browser/API empty-result check | Not available; no browser application or test runner exists | Successful empty data has a distinct empty state and is not confused with loading or failure |
| AC-006 | Proposed PostgreSQL persistence and browser refresh/revisit check | Not available; no application or test runner exists | Created data is visible after refresh in the same workspace and is not duplicated |
| AC-007 | Proposed real-PostgreSQL cross-workspace read/create isolation tests using two S01 credentials | Not available; requires selected integration-test tooling and isolated PostgreSQL | Each credential can access only its server-derived workspace; cross-workspace access is denied without existence disclosure |
| AC-008 | Proposed API error-contract and browser failure-state checks | Not available; no application or test runner exists | Errors are explicit; failed reads do not become empty success; drafts and prior confirmed view are retained |
| AC-009 | Proposed PostgreSQL-backed ambiguous-response/retry integration test | Not available; retry/idempotency mechanism and test tooling are unselected | Retrying one ambiguous create operation leaves exactly one task |
| AC-010 | Proposed date-only serialization and round-trip test across API and persistence | Not available; no application or test runner exists | Saved and returned date values are identical calendar dates and no overdue calculation occurs |

## Showcase and completion

1. In an isolated environment, complete S01 provisioning and use a disposable task space.
2. Create a task with only a title, then create another with a description and date-only due date. Observe explicit save confirmation only after server confirmation.
3. Refresh the browser and revisit the task space. Verify both tasks and their saved values are still visible.
4. Repeat with a second isolated task space and verify neither workspace can see the other's tasks. Do not use production or pilot data.

- Completion evidence: Reviewed diff and migration; evidence mapped to every AC; focused unit/API, real-PostgreSQL integration, and browser checks with exact commands and observed results; keyboard-accessibility evidence; no task content or credential values in logs/errors.
- Execution evidence: Not run; specification only.
- Risks: S01 credential and workspace isolation must be available and validated first. Title normalization/length, description empty-value representation, new-task completion default, API/error shapes, retry mechanism, and test tooling remain unresolved.
- Next dependency/handoff: Review this draft with the product owner and engineering reviewer, resolve the listed blockers, and obtain separate implementation authorization. Then use the [implement vertical slice skill](../../.github/skills/implement-vertical-slice/SKILL.md) with this specification path.
