---
ai_generated: true
model: "unknown/unknown@unknown"
operator: "jMM-at-CODETraining"
chat_id: "e9752f6f-5119-4a18-af86-2254b152ab42"
prompt: "create instruction files for the tech stack"
started: "2026-11-12T21:03:43-08:00"
ended: "2026-11-12T21:08:49-08:00"
task_durations:
  - task: "Creating stack instruction files and integration records"
    duration: "00:05:06"
total_duration: "00:05:06"
ai_log: "ai-logs/2026/11/12/e9752f6f-5119-4a18-af86-2254b152ab42/conversation.md"
source: "user request"
name: "aspnet-core"
description: "Use when building or changing the task-manager C# and ASP.NET Core backend, including API endpoints, authorization, persistence boundaries, and errors."
applyTo: "**/*.cs"
version: "1.2.0"
author: "jMM-at-CODETraining"
tags: ["aspnet-core", "csharp", "backend", "task-manager"]
owner: "Development Team"
reviewedDate: "2026-11-13"
nextReview: "2027-02-13"
revisions:
  - chat_id: "c4961cf2-7fc3-457c-85c0-292912b61606"
    date: "2026-11-13"
    change: "Aligned access guidance with account-free single-browser/no-recovery; removed pairing/recovery requirements and the required default list."
    ai_log: "ai-logs/2026/11/13/c4961cf2-7fc3-457c-85c0-292912b61606/conversation.md"
  - chat_id: "02051eeb-6a3e-4203-977d-fcff87be0ec9"
    date: "2026-11-13"
    change: "Required releasing failed-initialization database resources before compensation and testing cleanup with a single-connection pool."
    ai_log: "ai-logs/2026/11/13/02051eeb-6a3e-4203-977d-fcff87be0ec9/conversation.md"
---

# ASP.NET Core Instructions

## Overview

Apply these rules to the task-manager backend. The approved stack is C# and ASP.NET Core in a single modular web application/API. Endpoint style, exact versions, data-access, migration, and test libraries remain unselected. Follow the [architecture specification](../../task-manager-architecture.md); these instructions do not authorize implementation.

## Table of Contents

- [Application Structure and Contracts](#application-structure-and-contracts)
- [Access and Security](#access-and-security)
- [Persistence and Concurrency](#persistence-and-concurrency)
- [Operations and Verification](#operations-and-verification)
- [Verification Checklist](#verification-checklist)
- [Summary](#summary)

## Application Structure and Contracts

- Organize backend code around the approved modular-monolith boundaries and end-to-end feature slices: workspace access, tasks, lists, and persistence.
- Keep endpoint handlers, validation, application behavior, and persistence responsibilities clear. Reuse shared authorization, validation, persistence, and error-handling mechanisms rather than duplicating them.
- Use typed request/response contracts and explicit validation. Return consistent, non-sensitive errors for validation failures, unauthenticated access, unavailable resources, version conflicts, rate limits, and service failures.
- Choose controllers or minimal APIs only after implementation planning; do not introduce a new architectural pattern or library by assumption.
- Keep workspace scope derived from the verified browser credential. A route, query parameter, or request body workspace ID is never authorization.

## Access and Security

- The approved MVP is account-free and bound to one browser. Issue one distinct, opaque browser credential for its task space; do not implement accounts, device pairing, credential reset, or user recovery unless the product baseline changes.
- Store only a secure credential verifier server-side; never log or return the raw credential after cookie issuance. Use the approved Secure, HttpOnly cookie policy and appropriate SameSite behavior.
- Enforce credential validity and workspace scoping on every protected request. Define expiry and revocation behavior before pilot and explain that losing or expiring the credential makes the task space inaccessible; do not silently create a replacement workspace after an authentication failure.
- Apply CSRF defenses and origin checks for state-changing requests. Rate-limit workspace creation and task read/write endpoints independently; numeric limits are open decisions and must be documented and tested before pilot launch.
- Never log task content, browser credentials, or sensitive request bodies/URLs. Do not expose stack traces, secret values, or cross-workspace existence in client errors.
- Keep deployment secrets outside source control. Use explicit environment configuration and least-privilege database access.

## Persistence and Concurrency

- Treat PostgreSQL as the authoritative store. Use workspace-scoped reads and writes and enforce same-workspace task/list ownership in application logic and database constraints.
- Commit state changes and version increments atomically. Use conditional version checks for updates and removals; report stale writes as explicit conflicts rather than overwriting newer data.
- Make creates and other non-idempotent operations safe against ambiguous responses and retries using stable operation identifiers or an approved equivalent.
- Ensure workspace creation and any required initial records succeed or fail together. The current product baseline does not require a default task list.
- On a duplicate or failed initialization that requires compensation, roll back and release/dispose the transaction and pooled connection before starting compensation that may need another connection. Do not retain scarce database resources while awaiting work that depends on the same pool.
- Keep date-only due dates distinct from UTC instants. Apply the product-approved local-date rule; do not silently substitute the server date. Resolve browser-versus-workspace time zone before implementing overdue behavior.
- Keep the deletion ledger independent from the restored application database. Do not expose restored data until ledger reapplication and access-state reconciliation succeed; define consistency and failure behavior before implementing restore.

## Operations and Verification

- Provide health/readiness behavior without disclosing secrets or task data. Emit sanitized structured logs and useful operational signals.
- Use transactions and migrations appropriate to the selected provider and library. Make deployment and rollback compatible with schema evolution; never assume application rollback reverses a database migration.
- Add unit and integration coverage for validation, workspace isolation, credential expiry/revocation, version conflicts, retry behavior, and explicit error responses. Do not add pairing/recovery tests unless those capabilities are approved in a future product change.
- Test duplicate initialization and compensation with `Maximum Pool Size=1`, and test concurrent duplicates against a real PostgreSQL instance or equivalent provider-level integration environment. Verify cleanup completes and the duplicate receives the specified conflict outcome without leaking usable credentials.
- Test restore and deletion-ledger failure paths before pilot readiness; keep the service unavailable if required safeguards cannot be applied.

## Verification Checklist

- [ ] Protected requests enforce credential validity and workspace scoping on every request.
- [ ] Concurrent stale updates cannot overwrite or recreate newer/deleted records.
- [ ] Failed-initialization compensation starts only after the transaction and pooled connection are released; a single-connection-pool regression test completes without waiting on its own connection.
- [ ] Browser-credential and task failures return explicit safe outcomes without silently provisioning a replacement workspace.
- [ ] Logs and errors exclude credentials and task content.
- [ ] Database writes preserve all required workspace/list/task invariants.
- [ ] Restore cannot serve data before deletion-ledger and access-state checks complete.
- [ ] No unselected library, hosting provider, or numeric operational target is assumed.

## Summary

Keep ASP.NET Core as a secure modular monolith: enforce server-side browser-credential authorization and workspace isolation, commit versioned mutations atomically, and make all failure states explicit and testable.

**Related Instructions**: [React/TypeScript](react-typescript.instructions.md), [PostgreSQL](postgresql.instructions.md)
**Document Version**: 1.2.0
**Last Updated**: 2026-11-13
**Maintainer**: Development Team
