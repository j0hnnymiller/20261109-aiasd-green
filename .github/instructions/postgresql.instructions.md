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
name: "postgresql"
description: "Use when designing or changing task-manager PostgreSQL schemas, queries, constraints, transactions, indexes, or database migrations."
applyTo: "**/*.sql"
version: "1.1.0"
author: "jMM-at-CODETraining"
tags: ["postgresql", "sql", "database", "task-manager"]
owner: "Development Team"
reviewedDate: "2026-11-13"
nextReview: "2027-02-13"
revisions:
  - chat_id: "c4961cf2-7fc3-457c-85c0-292912b61606"
    date: "2026-11-13"
    change: "Aligned task/list relationships with unassigned tasks and no default list; approved 30-day maximum backup retention with restore-time deletion ledger."
    ai_log: "ai-logs/2026/11/13/c4961cf2-7fc3-457c-85c0-292912b61606/conversation.md"
---

# PostgreSQL Instructions

## Overview

Apply these rules to the task-manager relational schema and SQL. PostgreSQL is the selected authoritative database. The provider, version, data-access library, and migration tooling remain unselected; verify their supported behavior during implementation planning. Follow the [architecture specification](../../task-manager-architecture.md); do not infer schema approval or implementation authorization from this guide.

## Table of Contents

- [Workspace Isolation and Data Integrity](#workspace-isolation-and-data-integrity)
- [Types, Queries, and Transactions](#types-queries-and-transactions)
- [Deletion, Backups, and Migrations](#deletion-backups-and-migrations)
- [Verification Checklist](#verification-checklist)
- [Summary](#summary)

## Workspace Isolation and Data Integrity

- Include workspace ownership in task and named-list records. Scope every application query and mutation to the authenticated workspace; never rely on an opaque resource ID alone for authorization.
- When a task has a list, enforce that the list belongs to the same workspace using appropriate relational constraints, such as a composite foreign key, in addition to application checks.
- Tasks may be unassigned; do not require or create a default `Tasks` list. When deleting a list, set its tasks to unassigned in the same transaction so the approved behavior is preserved.
- Use database constraints for required values, valid relationships, and uniqueness where the approved product rules require them. Keep database and application validation consistent.
- Treat schema details as provisional until implementation specifications confirm column names and exact representations.

## Types, Queries, and Transactions

- Store due dates as date-only values (`DATE`); never encode them as midnight UTC. Store operational instants in UTC-aware timestamp values. Do not assume a configured workspace time zone for overdue calculations until the product's browser-versus-workspace local-date rule is resolved.
- Store task version state for conditional updates. Perform the version predicate and update atomically; a stale version must not silently overwrite a newer row or restore a removed task.
- Use transactions for related changes that must succeed together, including workspace provisioning and task/list mutations with their version increments.
- Parameterize all values. Do not build SQL by concatenating user input. Implement title search as a literal, case-insensitive substring match; escape pattern metacharacters when using pattern operators so user input remains literal.
- Avoid premature indexes or query complexity. Add indexes based on approved access patterns and measured workload; document the query they support and verify query plans when data volume is representative.
- Use foreign keys and restrictive delete behavior that preserve required records. Do not add whole-workspace deletion, soft-delete/trash, or automatic inactivity cleanup without an approved retention policy.

## Deletion, Backups, and Migrations

- Remove user-deleted tasks from the active store immediately. Protect backups and expire them within the approved maximum 30-day window; shorter retention is allowed if operations and recovery needs support it.
- Keep the deletion ledger independent of database restoration and limited to opaque workspace/task identifiers and deletion times; never include task content or credentials. Before a restored database serves traffic, apply the ledger; if it is unavailable or cannot be applied, keep the restored service unavailable. Retain ledger entries until every backup containing the deleted record has expired.
- Use reviewed, repeatable migrations and preserve compatibility for deployment and rollback. Validate constraints and data transformations against representative data; do not treat an application rollback as a database rollback.
- Never use development or test data in pilot/production databases. Apply least-privilege roles and protect transport, storage, and backup keys.

## Verification Checklist

- [ ] Queries and mutations are parameterized and workspace-scoped.
- [ ] Nullable task/list relationships prevent cross-workspace references and allow unassigned tasks.
- [ ] Deleting a list leaves its tasks intact and unassigned in the same transaction.
- [ ] Date-only due dates and UTC instants use distinct representations.
- [ ] Version-checked mutations are atomic under concurrent requests.
- [ ] Required multi-record changes use transactions.
- [ ] Literal title search handles wildcard characters as literal input.
- [ ] Deleted records do not reappear after restore; backups expire within 30 days and restore applies the independent deletion ledger before serving data.
- [ ] Migrations and restore behavior are tested; no unspecified provider/tool is assumed.

## Summary

Use PostgreSQL to enforce relational integrity and atomic versioned changes while preserving strict workspace isolation, date-only semantics, and restore-safe deletion handling.

**Related Instructions**: [React/TypeScript](react-typescript.instructions.md), [ASP.NET Core](aspnet-core.instructions.md)
**Document Version**: 1.1.0
**Last Updated**: 2026-11-13
**Maintainer**: Development Team
