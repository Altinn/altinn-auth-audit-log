# Altinn Auth Audit Log Constitution

## Core Principles

### I. Preserve Audit Evidence

Authentication and authorization events are audit evidence. Changes MUST preserve the meaning
of event timestamps, actor and resource identifiers, operations, decisions, and context through
queue decoding, API handling, and persistence. The API MUST report successful ingestion only
after persistence succeeds. Downstream failures MUST remain visible to the queue processor so
its retry or failure handling can operate. New or changed ingestion paths MUST define and test
malformed-message, unsupported-version, and duplicate-delivery behavior; they MUST NOT silently
discard events or assume exactly-once delivery. These rules protect the reliability of the record.

### II. Maintain Producer and Storage Compatibility

Changes MUST preserve existing API routes, JSON field names and enum representations, queue
names, and supported message encodings unless a reviewed migration explicitly replaces them.
Authorization ingestion currently supports legacy JSON/base64 and versioned compressed messages;
changes to decoding MUST cover the affected formats with tests. Breaking contract changes MUST
identify affected producers, a transition strategy, and deployment order. Database changes MUST
use new versioned Yuniql migrations; previously applied migrations MUST NOT be rewritten.
Migration reviews MUST address existing records, permissions, partitions, and recovery from a
failed rollout. Producers and storage evolve independently, so compatibility is a release concern.

### III. Protect Sensitive Audit Data

New or changed code MUST limit collected and stored fields to those required by the documented
audit purpose. Credentials, access tokens, connection secrets, and complete audit payloads MUST
NOT be added to diagnostic logs, exception messages, test fixtures, or specifications. Operational
telemetry MUST use the minimum metadata needed to diagnose a failure, with sensitive values
redacted. Production secrets MUST come from the existing managed configuration and Key Vault
mechanisms. Database values MUST be parameterized; dynamic SQL identifiers MUST come from
validated, controlled inputs. Changes to database grants or service access MUST justify the
permissions they introduce. Durable audit records and diagnostic telemetry have different purposes.

### IV. Keep Responsibilities at Existing Boundaries

Queue transport and decoding belong in the Functions project; HTTP validation and response
handling belong in the API; domain models and service/repository contracts belong in Core;
SQL, migrations, and database access belong in Persistence. Core MUST remain independent of
API hosting, Azure queue transport, and concrete database implementations. Cross-boundary
collaboration MUST use the existing interfaces and dependency injection patterns. New services,
libraries, or abstractions MUST solve a documented requirement and explain why an existing
component is insufficient. This keeps the ingestion path understandable and independently testable.

### V. Verify Behavior at the Affected Boundaries

Behavior changes MUST include tests for their acceptance criteria; bug fixes MUST include a
regression test when the failure can be reproduced automatically. Tests MUST cover relevant
failure paths as well as successful ingestion. Contract changes MUST verify serialization and
producer compatibility. SQL, migration, or partition changes MUST include integration coverage
against PostgreSQL/TimescaleDB through the existing Testcontainers fixtures where applicable;
mock-only coverage is insufficient for database behavior. Time-dependent retention logic MUST
use controllable time and exercise cutoff and calendar boundaries. Documentation-only changes
require document validation rather than unrelated application tests.

## Operational and Data Lifecycle Constraints

- The implementation baseline is C#/.NET 10, Azure Functions V4 with the isolated worker,
  Azure Storage Queue, the container-hosted ASP.NET Core API, and PostgreSQL/TimescaleDB.
  Runtime or infrastructure changes MUST document compatibility and deployment implications
  and keep project files, CI, deployment configuration, and setup documentation consistent.
- Audit insertion MUST NOT acquire record-update or record-deletion behavior incidentally.
  Any correction or removal feature MUST explicitly specify its purpose and affected evidence.
- Automatic partition deletion MUST remain disabled unless explicitly enabled by environment
  configuration. Changes to `EnableOldPartitionDeletion` or `RetentionMonths` MUST document
  the affected time range and data-loss implications. Deletion MUST target only partitions
  outside the configured retention window; tests MUST protect retained partitions and the
  disabled-deletion case. This constitution does not establish a new retention duration.
- Partition maintenance MUST preserve ingestion for the required time ranges. Changes to
  scheduled maintenance MUST account for cancellation, concurrent execution, and failures.
- Changed failure paths MUST expose actionable, structured diagnostics without sensitive payloads.
  Health endpoints MUST remain available for operational monitoring. Claims of improved throughput
  or reduced memory use MUST include measurements under a stated workload.

## Development and Review Workflow

1. For work using Spec Kit, write a bounded specification with acceptance criteria, then a plan
   and actionable tasks. Plans MUST check each principle and identify contract, persistence,
   retention, and operational impacts, or state why an impact does not apply.
2. Implement the smallest coherent change. Follow `.editorconfig` and neighboring code conventions.
   Record material design decisions and update documentation when setup or behavior changes.
3. Validate executable changes with the relevant automated tests. Before merging application
   changes, `dotnet build Altinn.Auth.AuditLog.sln -v m` and
   `dotnet test Altinn.Auth.AuditLog.sln -v m` MUST pass in an environment with the required SDK
   and container runtime. Report unavailable prerequisites and skipped checks explicitly;
   an unavailable check is not a passing check.
4. Pull requests MUST explain the resulting behavior, verification evidence, and any compatibility
   or operational risks. Review MUST check constitution compliance and applicable CI analysis
   results. A workflow skipped by its trigger conditions is not evidence that its checks passed.

## Governance

This is the initial constitution, adopted on 2026-09-14 from the repository's documented purpose,
implementation boundaries, and test and CI practices. It governs new and changed work; it does
not certify that all existing code already satisfies every principle. Relevant pre-existing gaps
MUST be identified during planning and review, with follow-up work recorded when outside scope.

Amendments MUST be submitted as reviewable changes stating the reason, affected principles, and
impact on ongoing work. Repository maintainers approve amendments through the normal pull-request
review process. Update the version and Last Amended date with each amendment; preserve the original
Ratified date. MAJOR versions remove or incompatibly redefine principles, MINOR versions add or
materially expand obligations, and PATCH versions clarify wording without changing obligations.
Version 1.0.0 establishes the first project-specific governance baseline.

This constitution takes precedence over conflicting workflow templates or local development
conventions. A necessary exception MUST name the affected rule, rationale, risks, and scope in
the plan or pull request and receive maintainer review before merge. Recurring exceptions MUST
be resolved by an amendment rather than silently becoming the default. Reviewers MUST assess
compliance for every change, using the README, project files, and CI workflows as implementation
references. Remove the temporary Sync Impact Report before committing an amendment.

**Version**: 1.0.0 | **Ratified**: 2026-09-14 | **Last Amended**: 2026-09-14
