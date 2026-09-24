# Venture Operating Model

Canonical definition of the agent-base Venture model, its Categories, Department routing, and
human-authority boundaries.

## Venture

A **Venture** is one independently managed, value-producing unit represented by one repository. It
may be a hobby, software product, service, physical operation, or company-like business. Software
is the common case, but neutral terminology is used where it remains clear. `Project` is excluded
from the formal vocabulary; a bounded coordinated effort inside a Venture is an Initiative and is
normally represented by the Roadmap.

The repository is the Venture. `.agents/` is its descriptive and operating layer, not a second
`venture/` container.

## Objectives

The model exists to improve agent decisions through:

- **Alignment**: enough context to choose as the human directing the agent likely would, while
  preserving that human's authority over material decisions.
- **Continuity**: a familiar structure that new Ventures and agents tailor instead of reinventing.
- **Learning**: durable, human-approved guidance from meaningful corrections and outcomes.
- **Anti-bureaucracy**: no empty pages, routine reports, or ceremonial artifacts. Represent a
  concept only when it improves a decision, coordination, continuity, risk handling, or delivery.

## Context

`.agents/VENTURE.md` owns Identity, Purpose, Vision, and Strategy. `.agents/goals/` separately
owns Goals. Identity says what the Venture is; Purpose says why it exists; Vision describes the
intended future; Strategy records current high-level choices; Goals state measurable desired
outcomes. Identity sits above the rest because every other section assumes it.

One block rendered into `AGENTS.md` carries the full Identity, full Strategy, and every current
Objective, ordered by what decides things rather than by hierarchy. Purpose and Vision are
deliberately not in it: broad by design and an aspiration respectively, neither settles a choice.
Key Results load only when a change plausibly moves their Objective. This generated Venture text is
project-owned context and remains whole; the composed session gauge surfaces excess instead of
letting the renderer silently choose what to discard. The guard prints a pointer to the block last,
for recency, never a second copy of it.

Every agent loads Venture Context before substantive work. Missing, unreadable, or unfilled
Purpose, Vision, Strategy, or Goals is a behavioral stop: ask the current user to define it. The
guard may warn rather than fail during migration so a new agent-base release does not break every
existing repository.

## Discovery

Undefined Venture Context starts human-led discovery, not repository inference. Load the
`venture-discovery` skill before proposing or writing Purpose, Vision, Strategy, or Goals. The
repository may show what exists and make follow-up questions concrete, but it cannot establish why
the human began, what makes the work worthwhile, its intended ambition, or its constraints.

Ask one high-value question at a time, beginning with why the Venture exists and why the human
started it. Continue only until beneficiaries and value, desired success, important constraints,
and the Venture's role beside the human's other activities are clear. Then structure a concise
draft and obtain human approval before persisting it. Do not invent Goal metrics or force useful
but impractical-to-measure guidance into an OKR. Existing human-approved context is not
automatically reopened unless the human requests it or a material contradiction makes it uncertain.

## Review

The guard periodically surfaces that a Venture hierarchy review is due; it never performs or
blocks on the judgment. `venture-review` checks whether Strategy still serves Vision, Goals remain
traceable to Purpose or Vision, roadmap work cites live objectives, and accumulated exceptions
reveal a contradiction. A coherent review is silent. Findings are non-blocking and return to
`venture-discovery` before approved Venture intent is changed.

## Categories

The canonical tree is descriptive, not a demand for directories or content:

```text
Venture
├── Departments
├── Feedback
├── Goals
├── Products
│   └── Product
│       └── Features
├── Requests
├── Resources
├── Roadmap
├── Skills
├── Standards
└── Workflows
```

- **Products** are units of value supplied to external or explicitly named internal beneficiaries,
  including software, services, physical goods, and content. A Feature is a durable,
  beneficiary-observable capability or property of one Product, not the work that creates it.
- **Feedback** records external or internal observations with an explicit source. Support owns
  intake and deduplication; specialist Departments establish truth; Management owns material
  priority decisions.
- **Requests** are this Venture's change requests against agent-base itself, recorded under
  `.agents/requests/`. Distinct from Feedback, which is external or internal observation about
  this Venture's own Products. Governed by `AB-REQUEST-001`.
- **Resources** records only durable, significant assets and dependencies whose awareness improves
  decisions or continuity. It excludes secrets, people, Products, exhaustive dependency lists,
  financial ledgers, and disposable supplies.
- **Goals**, **Roadmap**, **Standards**, **Skills**, and **Workflows** retain their existing
  agent-base contracts. Workflow is the canonical term for repeatable Venture activity; a Skill is
  an agent procedure used within one.

Inactive Categories create no empty artifacts. Legacy root `.agents/features/` content requires a
human-approved Product migration; never invent a placeholder Product or move ambiguous Features.

## Departments

### Why this exists

A single implementer, human or agent, tends to see a task through one professional lens - usually
whichever lens the task looks like it needs. An engineer building a photo-sharing feature can miss
its Legal exposure (data protection law on shared personal images); a change framed as a pure code
fix can miss its Support cost (existing users hit by a breaking change). Neither omission is
carelessness; it is what a single point of view structurally cannot see.

Consulting other Departments is not a review process, and naming a Lead and Consulted list is not
the point of it. The actual value is the one deliberate question it forces before acting: *did I
miss an angle a specialist in something else would have caught?* Treat it as that quick self-check,
never as a formal gate. Skip the ceremony entirely when a task is genuinely single-angle and the
answer is obvious; never skip the question.

A Department is a virtual responsibility perspective, not a physical team, person, agent vendor,
Role, or Actor. The generic catalogue is always available under
`.agents/agent-base/departments/`. A Venture-specific overlay under `.agents/departments/` exists
only when durable local knowledge differs from or extends the generic definition. Start with one
file; migrate it to a same-named directory only after multiple real artifacts justify the depth.

Each generic Department defines exactly five concepts: Purpose, Owns, Leads, Consults, and
Boundaries. The canonical Departments are Management, UX, Engineering, Architecture, Quality,
Security, Safety, Marketing, Sales, Support, Finance, Legal, HR, and Operations.

## Routing

The user states the desired outcome; the main agent routes it automatically. For every substantive
phase:

1. Select exactly one **Lead Department** by ownership of the requested outcome and primary
   decision, never by tool, edited file, or who performs the most implementation.
2. Add only Departments whose responsibilities are materially affected as **Consulted**.
3. Load the Lead definition, consulted definitions, and any matching Venture overlays before
   acting.
4. State one short line naming the Lead, consulted Departments, and outcome-based reason.

One agent normally represents all perspectives. Use helper agents only for genuinely independent,
disputed, high-risk, or complex analysis. A request may have sequential phases with different
Leads; announce the handoff. If routing is uncertain and the choice could materially change the
outcome, ask the human rather than conceal the uncertainty.

The Lead summarizes consulted perspectives, recommendation, rationale, and trade-offs before a
material decision is implemented. The current user makes that decision unless the Venture names a
different authority. Routine details within an approved scope remain autonomous.

## Boundaries

Management stewards Purpose, Vision, Strategy, Goals, Roadmap, Product scope, priorities, pricing
strategy, and Venture-wide trade-offs. It does not become a generic middle-management layer.
Engineering changes technical systems or production methods and owns the design of a change;
Architecture owns the structure those changes accumulate into, and leads only where a boundary,
a dependency direction, a contract others follow, or the reversibility of a commitment is at
stake. Operations runs repeatable production, delivery, and availability. Quality establishes
required correctness and verifies it; Security addresses malicious threats; Safety addresses
accidental harm and its likelihood, severity, controls, and safe failure. Marketing owns brand and
positioning; UX owns usability and experience; Sales operates acquisition and commercial channels.

## Repository authority

When a repository artifact and its mapped wiki page represent the same fact, repository state is
operationally authoritative. A wiki edit is still a valid incoming proposal: compatible content is
first incorporated into the repository, then republished. Material, incompatible, or uncertain
differences go to the human with a recommendation. Never silently discard wiki work, overwrite an
uncertain conflict, scan the whole wiki before ordinary work, or perform an unauthorized external
write.

Wiki mapping and batch reconciliation are defined by the `venture-wiki-sync` skill. Framework
content under `.agents/agent-base/`, session continuity, templates, local throttle state, and the
sync manifest itself are not mirrored. Connector identity and the host-relative Venture wiki root
live only in the operator-local `wikis.json` registry.

Roadmap is a canonical Venture category but is never mirrored, for this Venture or any downstream
one. It is a working document for agents, not something a Product's beneficiaries or a Venture's
human read on the wiki; mirroring it would be ceremony with no reader. An update to a roadmap item
is made directly in the repository and needs no wiki republication.

A wiki mapping is mandatory (`AB-Q006`): every Venture, whether or not an operator-local registry
exists at all, must have one to be venture compatible. The guard reports a missing one every
session as a `strong`, never-blocking finding, naming the exact gap without naming any actual wiki
host or path. Reconciliation itself remains on-demand: an unconfigured mapping still triggers no
recurring *sync* work until the human asks for a wiki operation, only the recurring notice that one
is missing.
