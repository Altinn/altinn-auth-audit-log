# Design discussion: improved audit log (#332)

Date: 2026-09-14
Status: Design proposals for review; not a completed Spec Kit implementation plan.

See [the specification](spec.md), [its quality checklist](checklists/requirements.md), and
[issue #332](https://github.com/Altinn/altinn-auth-audit-log/issues/332).
No application, database, or deployment changes have been implemented on this branch.

## Confirmed inputs and constraints

- About 20 million PDP calls are logged per day. Peak traffic and payload sizes are unknown.
- Assume five years of searchable evidence while the legal team determines final retention.
  This is a planning assumption, not a legal conclusion or authorization to change retention.
- Typical investigations cover a user's activity on one day or over up to six months, users of
  an IP address during a period, and activity concerning messages, parties, signing, or submission.
- Non-repudiation means verifiable producer origin and detectable alteration or unauthorized
  deletion. End-user signatures are not required by this feature.
- Equivalent permission checks may be summarized with count and time range. Distinct actions
  and changed outcomes must remain distinguishable.
- Producers cannot be required to introduce a new event ID. Design must use existing input.
  The earlier suggestion of requiring producer event IDs or sequence numbers is superseded.
- Memory cache is a candidate optimization, not an approved durability mechanism.
- The specification's target of 95% of complete searches within 30 seconds is an assistant-proposed
  provisional target. The user supplied no numeric latency requirement; validate it during planning.

## Repository findings

- [The active table definitions](../../src/Altinn.Auth.AuditLog.Persistence/Migration/v0.02/01-setup-tables.sql)
  partition `authentication.eventlogv1` and `authz.eventlogv1` by `created`. These migrations create
  only a `created` index on each event table. Production may contain additional indexes installed
  outside this repository; inventory them before deciding on changes.
- [Partition maintenance](../../src/Altinn.Auth.AuditLog/Services/PartitionCreationHostedService.cs)
  creates monthly partitions. Its deletion scan considers only the last 24 months, so it cannot
  discover partitions expiring after five years merely by changing the retention setting.
- Earlier migrations create Timescale hypertables for the older `eventlog` tables. The current
  write path targets native range-partitioned `eventlogv1`; do not assume it already uses
  Timescale compression or chunk policies.
- [AuthorizationEvent](../../src/Altinn.Auth.AuditLog.Core/Models/AuthorizationEvent.cs) contains
  subject, session, resource, instance, operation, IP, timestamp, context, and decision. It does
  not expose a producer event ID, policy version, or sequence number.
- [AuthorizationEventsProcessor](../../src/Functions/Altinn.Auth.AuditLog.Functions/AuthorizationEventsProcessor.cs)
  forwards queue content to the API. Current processing does not preserve queue-message identity
  through the HTTP call. Using existing transport metadata would require internal changes.
- Permission granted is evidence of a decision, not proof of a completed read, signing,
  submission, or other action. Producer coverage for actual actions must be established.

## Capacity and indexing

At a constant 20 million events/day, before reduction and without growth:

| Period | Event count |
| --- | ---: |
| Second, 24-hour average | About 231 |
| 30-day month | 600 million |
| 180-day search period | 3.6 billion |
| Five 365-day years | 36.5 billion |

An illustrative 1 KiB per event gives about 37 TB over five years before indexes, replication,
other storage overhead, and compression. This is arithmetic, not a measurement of actual row size.

Indexes remain plausible at this ingestion rate, but the average does not establish peak capacity.
Each index adds write work and storage. Conversely, long scans compete with ingestion for resources.
Start by benchmarking `(subjectuserid, created)` and `(ipaddress, created)` for authorization,
with corresponding authentication indexes where required. Add resource/instance indexes only
when their measured benefit justifies the cost. Avoid broad indexing of the full context JSON
as an initial response. Read summary fields first and fetch large context only when needed.

A compact search projection separate from protected evidence is a candidate if the current table
layout remains expensive. A new database engine is not yet justified by measured evidence.

## Partition interval and index rollout

The initial discussion favored retaining months until measurements existed. The subsequently
reported volume of 600 million rows/month makes day and week intervals priority benchmark
candidates. Daily partitions are proposed, not yet selected or implemented.

| Interval | Approximate partitions per table over five years | Rows per full partition before reduction |
| --- | ---: | ---: |
| Month | 60 | 600 million for 30 days |
| Week | 261 | 140 million |
| Day | 1,826 | 20 million |

Smaller partitions make individual index builds and maintenance jobs more manageable. They do
not remove index maintenance on each insert or reduce the total number of historical rows that
must be indexed. Six-month searches still span the same period and need selective search indexes.

Proposed rollout:

1. Create future empty partitions with required indexes ahead of time. Index definitions on
   the partitioned parent apply to newly created partitions.
2. For an additional index on existing data, build child indexes in controlled batches using
   `CREATE INDEX CONCURRENTLY` when concurrent writes must continue, then attach them to the
   parent index. Concurrent index creation is not supported directly on the partitioned parent.
   Account for resource load, transaction waits, and retry/cleanup of failed builds.
3. Consider switching to daily intervals at a future month boundary while leaving historical
   month partitions in place. Non-overlapping partitions may have different interval lengths;
   repartitioning all history is not a prerequisite.
4. Update lifecycle management to discover actual partitions, cover the retained history, and
   handle delayed events and future partition creation. Exact expiry within a partition needs
   an explicit rule; never drop a partition that still contains unexpired evidence.

## Recognizing repetitions using existing input

Distinguish two concepts:

- Delivery identity: the same queue message being retried.
- Content equivalence: separately delivered permission checks with equivalent recorded inputs
  and outcomes. Equivalence does not establish that they are one real-world event.

Proposed versioned grouping key:

```text
hash(canonical representation of:
  trusted source scope and event type,
  subject and represented party,
  session and available authentication context,
  resource owner, resource and instance,
  operation, IP address, decision,
  relevant available decision context)
```

Use only available fields. Do not introduce a required policy-version or sequence field.
Canonicalization must preserve values, types, missing-versus-null distinctions where meaningful,
and array semantics. Exclude the occurrence timestamp from the equivalence key and use it for
window membership and first/last time. Do not discard arbitrary changing context fields to improve
hit rates. Missing context or uncertain equivalence means separate retention.

A short bounded interval, initially proposed as 60 seconds, limits aggregation. Fixed UTC minute
windows are a conservative candidate: two otherwise equivalent events straddling a minute boundary
stay separate. The interval and grouping rules need validation against investigation requirements.
There is no requirement that the window slide indefinitely on each cache hit.

Summaries retain count, first/last event time, common evidence, grouping-rule version, and precision
limitations. Distinct actual actions remain individual. A Permit/Deny/Permit sequence must not
be represented as uninterrupted permission: preserve changed outcomes and uncertain ordering.
Without producer sequences, do not claim that arrival order establishes the original decision
order. Conservative separate records are required where merging would obscure a transition.
A partially overlapping search must not present a full-window count as an exact sub-period count.

## Cache, concurrency, and durable processing

Use bounded per-process `IMemoryCache` as an optional lookup from grouping key/window to a known
summary reference. Cache hits do not authorize dropping events. The authoritative write path must
validate window/state and protect counts against concurrent instances and stale cache entries.
Cache misses or eviction use the normal durable path. Plain `GetOrCreate` is not sufficient to
make a multi-step aggregation operation atomic.

Two implementation candidates should be measured:

1. Durable per-message aggregation: update the summary and applicable retry tracking atomically,
   acknowledge only after commit. This reduces stored rows but still performs about one durable
   operation per input. Twenty million inserts becoming twenty million updates does not remove
   twenty million write operations; updates also incur storage and transaction-log work.
2. Short durable-backed batches: accumulate bounded groups from the existing queue, persist the
   batch with retry tracking, and acknowledge covered messages only after commit. On crash, pending
   messages must remain recoverable from the queue. Define visibility renewal, backpressure,
   bounded memory, concurrent batches, and partial acknowledgement recovery. A memory-only timer
   flush after acknowledging messages would lose counts and timestamps on restart and is rejected.

Existing Azure Queue message identity, scoped to the storage account and queue, can support
internal retry tracking without changing PEP/PDP payloads. Propagate it through internal hops;
commit retry registration and aggregation together. Tracking lifetime must cover the supported
queue/replay horizon, not just the 60-second summary window.

Independent producer submissions have different queue identities, even with identical content.
Without a producer identity for the event, we cannot always distinguish producer duplication from
two actual decisions. Counts therefore describe distinguishable accepted inputs after identifiable
retry removal, not an exact number of real-world actions. Content hashes, even with timestamps,
do not resolve this ambiguity. A queue-scoped message ID also does not prove producer origin.

## Evidence protection and search

A proposed architecture separates protected finalized evidence from a rebuildable search projection.
Open summaries are durable processing state; finalized summaries become immutable evidence.
Late arrivals create traceable additions instead of rewriting sealed evidence. A transactional
outbox or equivalent durable handoff can make finalization and publication recoverable.

Azure immutable Blob storage is one candidate for finalized evidence. Integrity design must also
cover authenticated producer origin, completeness, unauthorized deletion, approved aggregation,
authorized expiry, and the pre-finalization interval. A hash stored alongside freely editable
records is insufficient by itself. Independent verification anchors and access separation require
a threat-model decision during planning. Historical evidence cannot acquire guarantees retroactively.

Search projections must disclose indexing lag, incomplete coverage, and verification limitations.
This design is a proposal, not a demonstrated end-to-end non-repudiation guarantee.

## Boundary with PEP/PDP optimization

Audit processing happens after the decision. It can reduce retained repetitions but cannot undo
an already executed PDP call. Avoiding redundant PEP requests is separate work in the owning
component. Start by evaluating coalescing simultaneous equivalent calls within one operation.
Caching decisions over time requires explicit validity and invalidation rules for identity,
policy, delegation, resource state, and other decision inputs. An audit aggregation window
must never be reused as the validity period of a Permit decision.

Actual reads, signing, and submission must still be logged where actions are observed even when
a PEP reuses a decision. Audit changes must work independently of upstream optimization.

## Spec Kit next steps and review questions

This document captures the discussion. It is not `plan.md` or completion of `$speckit-plan`.

- `$speckit-plan`: compare alternatives in `research.md`, decide architecture in `plan.md`,
  define entities in `data-model.md`, describe internal metadata and compatibility in `contracts/`,
  and provide reproducible validation steps in `quickstart.md`.
- `$speckit-tasks`: derive implementation and test tasks after the design is settled.
- `$speckit-analyze`: verify spec/plan/tasks consistency before implementation.

Before selecting capacity and retention settings, obtain production index inventory and database
version, representative query plans, peak input rate, payload/row sizes, repetition distribution,
retained data size, result sizes, and concurrent investigator load. Benchmark day/week/month
partitions with minimal and targeted indexes while ingestion is active; measure ingest latency,
queue lag, search percentiles, CPU, disk I/O, transaction-log volume, index size, and recovery.

Acceptance exercises must include concurrent duplicate delivery, identical independent inputs,
restart before/after commit, cache loss, multiple instances, late/out-of-order events, changed
outcomes, window/retention boundaries, and evidence verification after aggregation or expiry.

## Technical references consulted

- [PostgreSQL partitioning, index inheritance, and concurrent child-index builds](https://www.postgresql.org/docs/current/ddl-partitioning.html)
- [PostgreSQL index costs](https://www.postgresql.org/docs/current/indexes-intro.html)
- [PostgreSQL multicolumn indexes](https://www.postgresql.org/docs/current/indexes-multicolumn.html)
- [PostgreSQL CREATE INDEX](https://www.postgresql.org/docs/current/sql-createindex.html)
- [PostgreSQL atomic conflict handling](https://www.postgresql.org/docs/18/sql-insert.html)
- [PostgreSQL EXPLAIN](https://www.postgresql.org/docs/current/using-explain.html)
- [ASP.NET Core memory cache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0)
- [Azure Functions queue trigger and message metadata](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-queue-trigger)
- [Azure immutable Blob storage](https://learn.microsoft.com/en-us/azure/storage/blobs/immutable-storage-overview)
- [Altinn authorization decision flow](https://docs.altinn.studio/en/authorization/reference/system/flows/authorization-decision/)
