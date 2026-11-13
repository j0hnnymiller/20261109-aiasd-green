---
ai_generated: true
model: "unknown/unknown@unknown"
operator: "johnmillerATcodemag-com"
chat_id: "c4961cf2-7fc3-457c-85c0-292912b61606"
prompt: "create a product requirements specification a task manager"
started: "2026-11-13T08:12:22.1498043-08:00"
ended: "2026-11-13T09:57:49.1515040-08:00"
task_durations:
  - task: "Repository and product-context review"
    duration: "00:00:35"
  - task: "Requirements drafting"
    duration: "00:00:46"
  - task: "Validation and documentation"
    duration: "00:02:50"
  - task: "Vertical-slice roadmap and README update"
    duration: "00:01:10"
  - task: "Validation and records"
    duration: "00:00:48"
  - task: "Final provenance reconciliation"
    duration: "00:00:41"
total_duration: "00:06:50"
ai_log: "ai-logs/2026/11/13/c4961cf2-7fc3-457c-85c0-292912b61606/conversation.md"
source: "user request"
revisions:
  - chat_id: "c4961cf2-7fc3-457c-85c0-292912b61606"
    date: "2026-11-13"
    change: "Linked the approved PRS v0.8 and requester-approved architecture specification."
    ai_log: "ai-logs/2026/11/13/c4961cf2-7fc3-457c-85c0-292912b61606/conversation.md"
  - chat_id: "c4961cf2-7fc3-457c-85c0-292912b61606"
    date: "2026-11-13"
    change: "Linked the vertical-slice implementation roadmap as a draft plan; it does not authorize implementation."
    ai_log: "ai-logs/2026/11/13/c4961cf2-7fc3-457c-85c0-292912b61606/conversation.md"
  - chat_id: "99477caf-5fba-46a3-a2f8-4813bccd3c7c"
    date: "2026-11-13"
    change: "Updated the S01 specification link to Ready for review and clarified that technical/security review and implementation authorization remain pending."
    ai_log: "ai-logs/2026/11/13/99477caf-5fba-46a3-a2f8-4813bccd3c7c/conversation.md"
  - chat_id: "99477caf-5fba-46a3-a2f8-4813bccd3c7c"
    date: "2026-11-13"
    change: "Updated the S01 specification link to Approved by the requester; implementation authorization remains separate."
    ai_log: "ai-logs/2026/11/13/99477caf-5fba-46a3-a2f8-4813bccd3c7c/conversation.md"
  - chat_id: "d45afcf0-3a5c-43c0-a5f9-91947ac159c3"
    date: "2026-11-13"
    change: "Added the S01 application scaffold, local verification instructions, implementation decisions, and security-review gates."
    ai_log: "ai-logs/2026/11/13/d45afcf0-3a5c-43c0-a5f9-91947ac159c3/conversation.md"
  - chat_id: "99477caf-5fba-46a3-a2f8-4813bccd3c7c"
    date: "2026-11-13"
    change: "Recorded explicit Copilot implementation authorization through GitHub issue #2; independent technical/security review and pilot gates remain."
    ai_log: "ai-logs/2026/11/13/99477caf-5fba-46a3-a2f8-4813bccd3c7c/conversation.md"
  - chat_id: "02051eeb-6a3e-4203-977d-fcff87be0ec9"
    date: "2026-11-13"
    change: "Linked the engineering instructions updated to address cross-tab provisioning, connection-pool compensation, and regression-test assertions."
    ai_log: "ai-logs/2026/11/13/02051eeb-6a3e-4203-977d-fcff87be0ec9/conversation.md"
---

# Task Manager

Welcome!

## Product

- [Task Manager Product Requirements Specification](task-manager-requirements.md) (approved baseline, v0.8)
- [Task Manager Architecture](task-manager-architecture.md) (approved by requester; implementation authorization is separate)
- [Task Manager Vertical-Slice Implementation Roadmap](task-manager-implementation-roadmap.md) (draft plan; implementation authorization is separate)
- [S01: Provision and Access a Browser Task Space](specs/task-manager/S01-provision-browser-task-space.spec.md) (approved by requester; technical/security review and implementation authorization remain separate)

## S01 implementation

The current application implements only the S01 browser task-space foundation. The [React/TypeScript client](src/client/), [ASP.NET Core API](src/TaskManager.Api/), [PostgreSQL migration](src/TaskManager.Api/Database/Migrations/001_create_workspace_tables.sql), and [xUnit tests](tests/TaskManager.Api.Tests/) cover this access foundation. Tasks, lists, accounts, pairing, recovery, offline access, and production deployment are not implemented.

The repository initially had no application or test project. The .NET solution/API/test projects and Vite React/TypeScript client were scaffolded to implement the approved architecture and make the required API, browser, and real-PostgreSQL checks executable.

### Selected tooling

- .NET 10 / ASP.NET Core minimal APIs; Npgsql 10.0.3 for parameterized PostgreSQL access.
- PostgreSQL SQL migration scripts executed with `psql`. This keeps the small, reviewed S01 schema change visible and transactional without introducing a migration framework.
- React 19.3.1, TypeScript 7.0.2, and Vite 8.3.8.
- xUnit with ASP.NET Core's test host for API and real-PostgreSQL tests; Playwright 1.65.0 for Chromium browser acceptance checks.

These are implementation selections, not production hosting or database-version approvals.

### Local development and verification

Use only a disposable local database. Do not connect these commands to pilot or production data.

1. Start an isolated test database:

   ```sh
   docker run --rm --name task-manager-test-db \
     -e POSTGRES_HOST_AUTH_METHOD=trust \
     -e POSTGRES_DB=task_manager_test \
     -p 127.0.0.1:54329:5432 \
     postgres:16-alpine
   ```

   Trust authentication is for this loopback-bound disposable container only. Never reuse this configuration for a shared or deployed database.

2. Restore and build:

   ```sh
   npm ci --prefix src/client
   dotnet restore TaskManager.slnx
   dotnet build TaskManager.slnx
   ```

3. Run unit and real-PostgreSQL API/integration tests. The test suite creates and drops isolated schemas within the disposable database:

   ```sh
   TASK_MANAGER_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=54329;Database=task_manager_test;Username=postgres' \
     dotnet test tests/TaskManager.Api.Tests/TaskManager.Api.Tests.csproj
   ```

4. Apply the migration to the disposable database's default schema before running the browser suite. The script is transactional and safe to reapply:

   ```sh
   psql 'postgresql://postgres@127.0.0.1:54329/task_manager_test' \
     -v ON_ERROR_STOP=1 \
     -f src/TaskManager.Api/Database/Migrations/001_create_workspace_tables.sql
   ```

5. Run frontend checks and Chromium browser acceptance tests:

   ```sh
   npm run lint --prefix src/client
   npm run build --prefix src/client
   TASK_MANAGER_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=54329;Database=task_manager_test;Username=postgres' \
     npm run test:e2e --prefix src/client
   ```

   Install the browser once if needed with `cd src/client && npx playwright install chromium`. Playwright traces, screenshots, and video capture are disabled so test artifacts cannot accidentally capture browser state.

The API requires `ConnectionStrings:TaskManager`, `Security:AllowedOrigin`, and positive values for both `AbuseControls:Provisioning:{PermitLimit,WindowSeconds}` and `AbuseControls:ProtectedRequests:{PermitLimit,WindowSeconds}`. Set these through environment or secret configuration. No rate-limit values are approved for pilot/production; the test harness uses fixture-only values. Production startup also requires an HTTPS allowed origin.

### Security decisions and review gates

- A 32-byte cryptographically random credential is issued only in the `__Host-taskspace` cookie. The cookie is Secure, HttpOnly, Path `/`, has no Domain attribute, uses SameSite `Strict`, and expires after 30 days. PostgreSQL stores only the SHA-256 verifier and its 30-day server-side expiry. SameSite `Strict` is an implementation choice, not a completed security review.
- Mutating API requests require an exact configured `Origin`; the browser cookie uses SameSite `Strict`, and CORS is not enabled. The combined Origin and cookie policy is the CSRF control; an independent reviewer must validate it before merge.
- The browser confirms cookie receipt with a separate Origin-checked `POST /api/workspace/confirm`; the workspace is not shown as ready until this succeeds. The browser creates a UUIDv4 initialization correlation key for an explicit create action and keeps it only in session storage until confirmation. The server stores its hash; it is not accepted as authorization. A retry carrying the valid cookie is idempotent and does not create another space. A duplicate retry without a valid cookie invalidates only an unconfirmed initialization credential and returns a non-sensitive conflict. An already confirmed credential is not revoked by a duplicate retry. Cancellation compensation runs independently of the request cancellation token. This compensation behavior still requires technical/security review.
- Provisioning and protected-request limits are separately configurable and partitioned by remote IP. Missing settings stop startup rather than silently disabling abuse controls. Forwarded-IP/trusted-proxy configuration and numeric pilot limits remain operational/security decisions.
- There is no user reset/recovery endpoint or revocation UI. The approved operational procedure for credential revocation and ownership/timing for purging inaccessible workspaces remain unresolved pre-pilot gates.
- This issue does not authorize production deployment. Request independent technical/security review before merge; automated tests and CodeQL do not replace that review.

The acceptance-criterion test mapping, execution evidence, and remaining review checks are recorded in the [S01 specification](specs/task-manager/S01-provision-browser-task-space.spec.md).
- [S01: Provision and Access a Browser Task Space](specs/task-manager/S01-provision-browser-task-space.spec.md) (approved by requester; Copilot implementation authorized through [issue #2](https://github.com/j0hnnymiller/20261109-aiasd-green/issues/2); independent technical/security review and pilot gates remain)

## Engineering Guidance

- [React and TypeScript Instructions](.github/instructions/react-typescript.instructions.md)
- [ASP.NET Core Instructions](.github/instructions/aspnet-core.instructions.md)
- [Test-Driven Development Instructions](.github/instructions/test-driven-development.instructions.md)
