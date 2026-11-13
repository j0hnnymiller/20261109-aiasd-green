BEGIN;

CREATE TABLE IF NOT EXISTS task_workspaces (
    id uuid PRIMARY KEY,
    initialization_key_hash bytea NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    last_accessed_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT uq_task_workspaces_initialization_key_hash UNIQUE (initialization_key_hash),
    CONSTRAINT ck_task_workspaces_initialization_key_hash_length CHECK (octet_length(initialization_key_hash) = 32)
);

CREATE TABLE IF NOT EXISTS workspace_credentials (
    workspace_id uuid PRIMARY KEY REFERENCES task_workspaces (id) ON DELETE RESTRICT,
    verifier bytea NOT NULL UNIQUE,
    expires_at timestamptz NOT NULL,
    revoked_at timestamptz,
    activated_at timestamptz,
    CONSTRAINT ck_workspace_credentials_verifier_length CHECK (octet_length(verifier) = 32)
);

ALTER TABLE workspace_credentials
    ADD COLUMN IF NOT EXISTS activated_at timestamptz;

COMMIT;
