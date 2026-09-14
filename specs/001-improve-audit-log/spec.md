# Feature Specification: Improved Audit Log (Forbedret auditlog)

**Feature Branch**: `codex/install-spec-kit`

**Feature Directory**: `specs/001-improve-audit-log`

**Created**: 2026-09-14

**Status**: Ready for planning - provisional retention and response-time assumptions

**Input**: User description: "issue 332"

**Source**: [Altinn/altinn-auth-audit-log#332 - Forbedret auditlog](https://github.com/Altinn/altinn-auth-audit-log/issues/332)

Issue #332 reports searches taking up to 15 minutes and large volumes of repeated permission
checks. It calls for explicit logging, retention, search, and response requirements; robust and
non-repudiable evidence; reduced unnecessary logging; partitioning; and documentation.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Answer an Investigation Reliably (Priority: P1)

As an authorized investigator, I need to find relevant activity for a person, organization,
message, form, or network address over a specified period of a day through six months, so I can investigate incidents and
answer claims of identity theft without waiting many minutes or misinterpreting the evidence.

**Why this priority**: These are the five concrete investigation needs in the issue, and slow
retrieval prevents timely case handling.

**Independent Test**: Use a representative, synthetic evidence collection with known results
for all five query families. Verify correctness, completeness status, and measured search time.

**Acceptance Scenarios**:

1. **Given** authentication activity for several users and periods, **When** the investigator
   searches for one user and period, **Then** the results identify the matching login times,
   network addresses, authentication methods, and outcomes, without including other users.
2. **Given** evidence concerning signing or submission of one or more forms, **When** those
   forms are investigated, **Then** results identify the actor, time, network address, and
   associated authentication method where supported by evidence; unavailable links are explicit.
3. **Given** activity involving one message or all messages for selected parties, **When** the
   investigator searches that scope, **Then** matching actors, network addresses, times, and
   recorded actions are returned, distinguishing actual reads from decisions permitting reads.
4. **Given** a network address associated with a case, **When** the investigator searches for
   other users of that address in the selected period, **Then** matching recorded users and
   activity are returned without asserting that a shared address establishes shared identity.
5. **Given** an identity-theft claim, **When** the investigator searches a user's activity over
   a period, **Then** the available authentication and authorization evidence is presented in
   event-time order with repetitions, failed actions, and known coverage gaps identified.
6. **Given** absent, expired, incomplete, or unverifiable evidence, **When** a search completes,
   **Then** its result distinguishes these states from a complete search with no matches.

---

### User Story 2 - Rely on Authentic, Complete Evidence (Priority: P1)

As an investigator and service owner, I need retained evidence to have verifiable origin and
integrity and ingestion failures to remain recoverable, so an incident account is trustworthy.

**Why this priority**: Faster searches are insufficient if records can be lost or changed
without detection, or attributed more strongly than the available evidence supports.

**Independent Test**: Submit a known collection of events, interrupt delivery and storage,
recover service, and verify the resulting collection. Exercise the integrity guarantees chosen
in FR-009 with controlled tampering, unauthorized deletion, and origin-verification tests.

**Acceptance Scenarios**:

1. **Given** required events awaiting ingestion, **When** delivery or storage fails temporarily,
   **Then** events are not reported as successfully retained before durable acceptance, and
   failure and recovery status are visible to operators.
2. **Given** recovery after a failure, **When** pending events are replayed, **Then** every
   required logical occurrence remains represented according to the approved repetition policy.
3. **Given** retained evidence with integrity and origin information, **When** verification is
   performed after a controlled alteration, unauthorized deletion, or origin substitution, **Then** the result identifies
   failure and does not present affected evidence as verified.
4. **Given** older evidence predating the new guarantees, **When** it is investigated, **Then**
   it remains distinguishable from evidence covered by the new guarantees.

---

### User Story 3 - Reduce Redundancy Without Losing Distinct Activity (Priority: P2)

As a service owner, I need to retain necessary evidence without repeatedly storing equivalent
permission checks, so volume decreases while investigators can still reconstruct relevant activity.

**Why this priority**: Repeated checks are an explicit source of volume in the issue, but reducing
volume must not merge distinct users, actions, resources, or changed decisions.

**Independent Test**: Use a labeled collection containing redelivered events, repeated equivalent
checks, and similar-looking but distinct activity. Compare retained evidence to the approved
classification and reduction rules.

**Acceptance Scenarios**:

1. **Given** redelivery of an event whose identity can be established, **When** it is processed
   again, **Then** it does not appear as an additional real-world occurrence.
2. **Given** repeated equivalent permission checks, **When** retention rules are applied,
   **Then** the representation follows the summary policy in FR-006 and retains the
   facts required for all five investigation families.
3. **Given** different actors, resources, sessions, network addresses, materially relevant
   context, outcomes, or independently performed actions, **When** records are classified,
   **Then** distinct activity is not discarded merely because other fields match.
4. **Given** insufficient information to prove equivalence, **When** reduction is considered,
   **Then** evidence is retained separately and the limitation is measurable.

---

### User Story 4 - Apply Documented Retention and Operating Rules (Priority: P2)

As a service owner, I need documented rules for necessary evidence, retention, retrieval, and
failure handling, so operations can demonstrate predictable behavior throughout the data lifecycle.

**Why this priority**: The issue explicitly requires clear requirements and documentation;
retention decisions directly affect which investigations can be answered.

**Independent Test**: Exercise retention boundaries and disabled cleanup with synthetic evidence,
then have an operator follow the documented search, verification, and recovery procedures.

**Acceptance Scenarios**:

1. **Given** an event category and its documented policy, **When** it is ingested, **Then** only
   the approved evidence fields are retained and its retention rule can be identified.
2. **Given** automatic removal is disabled, **When** lifecycle maintenance runs, **Then** it
   does not remove evidence; when enabled, evidence within its retention window is preserved.
3. **Given** evidence at a retention cutoff, **When** expiry is evaluated, **Then** the documented
   boundary rule is applied consistently and authorized expiry is distinguishable from unexpected loss.
4. **Given** a period partly outside available retention, **When** an investigator searches it,
   **Then** results identify the unavailable interval and do not claim complete coverage.

### Edge Cases

- Delayed or out-of-order events; timestamps crossing time zones, month ends, or year boundaries.
- A changed permission decision within an otherwise repetitive sequence, including denials and errors.
- Missing session, event identity, network address, authentication method, or resource linkage.
- Different people behind the same network address; one person using multiple addresses or sessions.
- A permission decision without evidence that the permitted read, signing, or submission occurred.
- Concurrent delivery, restart during ingestion, replay after an ambiguous response, or malformed input.
- A repeated sequence spanning a query or retention boundary; any summary must expose its limits.
- Large result sets, interrupted searches, or sources unavailable during an investigation.
- Authorized expiry versus unauthorized removal; legacy records without new integrity assurances.
- Reduced records whose underlying detail would be needed to answer one of the required queries.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The service MUST provide authorized investigators with the five investigation
  capabilities in User Story 1, scoped by a stated time range and the relevant subjects/resources.
- **FR-002**: Results MUST expose the recorded actor, action or decision, event time, target,
  outcome, network address, and authentication context where available, and distinguish unknown
  information from known negative results. Permission granted MUST NOT be presented as proof
  of an action completed. Authentication associations MUST be evidence-based, not inferred solely
  from a shared address or proximity in time.
- **FR-003**: Searches MUST state their covered period and completion status, expose available
  matching results without silent truncation, and identify known gaps, expired intervals, and
  verification limitations. Interrupted or failed searches MUST NOT report complete success.
- **FR-004**: A versioned logging policy MUST classify authentication, session lifecycle,
  authorization decisions, and observed actions as individually retained, reducible, or excluded.
  Each category MUST state its purpose, minimum fields, reduction rule, and retention rule.
  Exclusions MUST be justified against all five investigation capabilities.
- **FR-005**: Redelivery of an identifiable event MUST NOT inflate the count of real-world
  occurrences. Missing identity or uncertain equivalence MUST NOT cause silent evidence removal.
- **FR-006**: Repeated equivalent permission checks MUST be summarized with their occurrence
  count and first/last event times. Distinct user actions and changed outcomes MUST remain
  separate. Summaries MUST NOT cross actors, sessions, resources, operations, network addresses,
  or materially different context. The documented grouping rule MUST define a bounded interval
  and preserve the facts required by the five investigation families. Redelivery MUST NOT
  increase occurrence counts when that redelivery is identifiable. A whole-summary count MUST NOT be presented as an exact count
  for a shorter search period; partial overlap and any loss of time precision MUST be explicit.
- **FR-007**: Required evidence MUST remain recoverable through temporary delivery and storage
  failures. Durable acceptance, pending work, and rejected input MUST be distinguishable.
  Invalid or unsupported input MUST have a visible failure disposition rather than silent loss.
- **FR-008**: Operators MUST be able to reconcile accepted logical occurrences, redeliveries,
  reduced or excluded records, pending work, and failures without copying sensitive payloads
  into operational telemetry. Recovery MUST preserve the distinctions in FR-005 and FR-006.
- **FR-009**: Evidence MUST support verification of its producer origin and detection of
  alteration and unauthorized deletion. Verification failures and uncovered historical periods
  MUST be visible. Approved repetition reduction and authorized expiry MUST be distinguishable
  from tampering or unexpected removal. These assurances MUST remain verifiable for retained
  evidence throughout its lifetime. End-user signatures and proof that an end user personally
  performed an action are not required by the term non-repudiation in this feature.
- **FR-010**: Only authorized personnel MUST retrieve evidence. Existing access boundaries MUST
  not be broadened implicitly, and credentials and unnecessary personal data MUST NOT be copied
  into diagnostics, specifications, or investigation results.
- **FR-011**: The provisional planning baseline MUST keep required evidence searchable for five
  calendar years from event time, with a documented UTC cutoff rule. Investigators MUST be able
  to search any single day or a continuous period of up to six calendar months within available
  retention, including user activity and users of a given network address. At least 95% of
  complete searches in each query family MUST finish within 30 seconds against a representative
  five-year evidence collection under normal ingestion load. The 30-second value is an explicit
  provisional assumption, not a user-approved service level. Planning MUST establish the
  representative event volume, result sizes, and concurrent search load and document their
  measurement basis. Final retention policy remains subject to legal review; this specification
  does not authorize changing deployed retention or deletion configuration.
- **FR-012**: Lifecycle maintenance MUST preserve evidence inside its retention window and
  MUST NOT enable automatic deletion implicitly. Authorized expiry MUST be distinguishable
  from unexpected evidence loss, including for any reduced representation crossing a cutoff.
- **FR-013**: Existing evidence and producers MUST remain usable during transition. Changes
  MUST document rollout order, compatibility, historical coverage, and recovery limitations;
  historical data MUST NOT be silently reduced, removed, or presented as newly verified.
- **FR-014**: Documentation MUST describe logging and repetition rules, retention, the five
  search procedures, interpretation limits, verification, failure recovery, and measured search
  performance. A reviewer MUST be able to trace each policy to acceptance evidence.

### Key Entities *(include if feature involves data)*

- **Audit occurrence**: A recorded authentication, permission decision, or observed action,
  with subject, event time, target, outcome, relevant context, and provenance. These categories
  have distinct meanings and must not be conflated.
- **Evidence representation**: Retained evidence for one occurrence or an explicitly permitted
  group, including verification status and any count, time-range, or precision limitations.
- **Logging and retention policy**: Versioned rules determining necessity, minimum fields,
  permitted reduction, searchable lifetime, and expiry for each category.
- **Investigation request and result**: Authorized scope, time range, matching evidence,
  coverage/completion status, and provenance or interpretation limitations.
- **Ingestion disposition**: Whether submitted evidence is retained, pending, rejected,
  redelivered, reduced, or excluded under a documented rule.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All five investigation families return 100% of the expected evidence and no
  unrelated evidence in the labeled acceptance collection; every deliberately introduced gap
  or unavailable interval is identified without implying complete coverage.
- **SC-002**: At least 95% of searches in each of the five families complete within 30 seconds,
  including full result retrieval for single-day and six-month periods across a representative
  five-year collection under normal ingestion load. Validation MUST state event volume, result
  size, and concurrent search load. This provisional target is subject to workload validation.
- **SC-003**: The redundancy acceptance collection retains 100% of distinct required occurrences,
  introduces zero extra occurrences or summary-count increments from identifiable redelivery,
  and contains no redundant representations removable under FR-006. Summarized sequences preserve
  exact counts of distinguishable accepted inputs and their time bounds; sub-period searches
  disclose precision limits. Identical inputs with no distinguishing identity MUST NOT be
  presented as proof of an exact number of underlying decisions or user actions.
- **SC-004**: Fault-and-recovery exercises end with zero lost durably accepted required
  occurrences, zero unexplained ingestion dispositions, and no successful acknowledgement of
  evidence that was not durably accepted.
- **SC-005**: Every controlled alteration, origin-substitution, and unauthorized-deletion test
  is detected, including tests after authorized expiry or repetition reduction elsewhere in the
  collection. Untampered evidence and authorized lifecycle actions cause zero false tampering
  results in that test collection. Legacy evidence is never mislabeled as meeting assurances it lacks.
- **SC-006**: All cases immediately before, at, and after the five-calendar-year cutoff follow
  the documented UTC expiry rule, including leap-day events and summaries spanning the boundary.
  Evidence still within its lifetime remains searchable; cleanup disabled yields zero automatic
  removals, and searches identify intentionally expired coverage.
- **SC-007**: A service operator can complete all five documented investigation procedures and
  the documented verification and failure-recovery exercises without undocumented steps.

## Assumptions

- Producers cannot be required to add an event identifier, sequence number, or new policy metadata
  for this feature. Repetition classification must use available input fields; insufficient context
  requires separate retention rather than a claim of equivalence.
- Content equivalence does not establish event identity. Available delivery metadata may identify
  transport retries, but independently submitted identical inputs cannot always be distinguished
  from producer duplicates. Summary counts must state that they describe distinguishable accepted
  inputs, excluding identifiable retries, rather than guarantee the count of real-world actions.
- The audience is authorized internal investigators and service operators. A new public portal,
  end-user self-service workflow, or expansion of investigator privileges is outside this feature.
- The five query families in issue #332 define the minimum investigation scope. Delivery channel
  and internal design are planning decisions; this spec does not prescribe a new user interface.
- Existing records primarily capture authentication and authorization decisions. Proof of actual
  reads, signing, or submission depends on reliable evidence from the responsible producers.
  Planning must identify producer gaps and coordinated work; a permission grant is insufficient.
- The initial logging-policy baseline preserves distinct authentication and meaningful action
  evidence and changed outcomes. Further exclusion requires an explicit policy justification.
- Repetition reduction applies to new evidence after rollout unless historical conversion is
  separately specified. Summaries preserve counts and time ranges; they need not reproduce each
  permission check's exact timestamp, but MUST disclose that limitation to investigators.
- Five years is the user's provisional planning assumption while the legal team determines
  retention. It is not a statement of a legal requirement or permission to delete existing data.
  Evidence already removed cannot be reconstructed; historical coverage must be documented.
- A 30-second full-result completion target for 95% of searches is a provisional specification
  assumption because the user did not supply a numeric response-time target. The baseline
  performance test uses one active investigator while ingestion continues; planning must measure
  expected concurrency and workload and revise the benchmark or target explicitly if needed.
- The retention clock starts at the occurrence's event time. Search intervals use an inclusive
  start and exclusive end in UTC; the cutoff for a leap-day event follows the last valid day
  of the corresponding expiry month. These are explicit boundary assumptions for planning.
- The issue mentions partitioning. Its design belongs in planning; the required outcomes are
  searchable retained evidence, bounded search time, and safe lifecycle maintenance.
- The user reports approximately 20,000,000 PDP calls logged per day. At a constant rate, this
  is about 231 events/second averaged over 24 hours, 600,000,000 events per 30-day month, and
  36,500,000,000 events over five 365-day years before repetition reduction. These projections
  assume no growth and exclude additional event streams. They are workload inputs, not measured
  system capacity. Peak rates, payload sizes, repetition ratio, and concurrent search load must
  be measured before performance acceptance. No storage-reduction percentage is assumed.
- Dependencies include available producer context, the existing investigator access process,
  policy owners for retention and logging, legal review of the five-year assumption, and
  representative workload measurements. These are tracked planning and rollout dependencies.

## Clarifications

### Session 2026-09-14

- Q: How long must evidence remain searchable, and what search periods matter? A: The legal
  team is still working on retention; assume five years. Typical searches cover what a user did
  on a specific day, who used a given network address over a period, and a user's activity over
  a period such as six months. No numeric response-time target was supplied.
- Q: What does non-repudiation mean here? A: Verifiable source and tamper evidence, including
  detectable alteration or unauthorized deletion; end-user signatures are not required.
- Q: Which repeated events may be reduced? A: Summarize equivalent permission checks while
  preserving count and time range; retain distinct user actions and changed outcomes separately.
- Q: Can this design require a new event identifier? A: No. The design must use existing inputs;
  memory cache is a candidate optimization. No new producer identifier or sequence is a prerequisite.
- Q: What ingestion volume must the design consider? A: Approximately 20,000,000 PDP calls are
  logged each day. Peak throughput and message sizes have not yet been supplied.
