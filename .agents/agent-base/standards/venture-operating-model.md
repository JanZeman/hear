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

Each Venture declares one recurring **Cycle anchor** date in `.agents/VENTURE.md` (own section,
next to Strategy): the day its OKR cycle first started. Cycles repeat every 3 calendar months from
that month and day, clamped to the shorter month where the day does not exist. A per-Venture
anchor, rather than one shared calendar date, is what keeps a fleet's reviews from landing on the
same day; each Venture is staggered by whatever date its own human picked. The guard prints the
current cycle position every session (`scripts/venture.py cycle-status`) -- which cycle, which day
within it, and the next boundary date -- so the question "where are we" needs no separate lookup.

A review becomes due exactly when today crosses a cycle boundary that has not yet been checked;
there is no lead-time warning before it and no grace window after it, only the boundary itself.
`venture-review` performs the check and never blocks on it: it looks at whether Strategy still
serves Vision, Goals remain traceable to Purpose or Vision, roadmap work cites live objectives, and
accumulated exceptions reveal a contradiction. A coherent review is silent. Findings are
non-blocking and return to `venture-discovery` before approved Venture intent is changed.

A Venture without a Cycle anchor yet (every one seeded before this existed) falls back to the
legacy 30-day rolling check instead of failing `AB-VENTURE-001` -- backfilling the anchor is a
per-Venture human decision about when its cycle should start, not something inferred the day this
field shipped.

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

## What the wiki is for

**The wiki is for the human.** Everyone who writes on it, human or agent, writes for a person. It is
a human-friendly window into the Venture: a person opens it to understand quickly what the Venture
is, where it is going, and what a person needs to know to run it. That agents can read it too is a
useful side effect, never its purpose. Nothing that exists for agents (skills, standards, catalogs,
the roadmap, agent-base framework content, or any other agent technicality) ever appears on it.

### The Venture page

Every Venture has one page that says what it is: the **Venture page** at `<root>/venture`, titled
"<Venture name> Venture". It holds the Venture Definition (Identity, Purpose, Vision, Strategy)
and the Goals, all on that one page and written for a person: each Goal with its objective, status,
key results, and why they were chosen. Working history (dated notes, rejected formulations) stays
in the repository. The repository owns exactly this page and nothing else on the wiki: it is
rendered from `.agents/VENTURE.md` and `.agents/goals/` and published one way, repository to wiki.
A change is made in the repository and published again; when the page was changed by hand, the
agent shows that difference to the human before publishing over it.

Everything else on the wiki belongs to the human, the Venture root page included. A person writes
it, and an agent writes it only when asked, for a person to read. The root page links to the
Venture page; when no root page exists yet, the agent proposes one holding only a title and that
link, and from then on it is the human's. Nothing on the wiki is synchronized or mirrored into the
repository; an agent that needs it reads the wiki directly.

### Where a Venture lives on the wiki

Whatever is declared a Venture lives under the wiki's Venture area: on a shared wiki host its root
is `ventures/<venture-name>`, the same place every other Venture on that host uses. The name is
written out in full, never abbreviated, so a person recognizes it in a path. A host dedicated to
one Venture may keep the root documented for it. A path never starts with a locale such as `en/`:
the wiki serves its default language without one. Connector identity, the root, and the host's
visibility live in the repository's own `.agents/wiki.json`; a mapping is mandatory (`AB-Q006`),
and the guard reports a missing one without naming any actual host or path.

### Moving pages

Every move on the wiki, whether converting existing content, retiring an old page, or renaming a
root, happens in two steps, so nothing is lost:

1. Copy: create the page at its new path with its content unchanged, show the human the result,
   and ask whether the original may be deleted.
2. Delete: only after that approval, delete the original and verify the copy is still there.

### Converting an existing wiki

When a Venture adopts a wiki that already describes it somewhere else, its existing pages move to
the Venture root; they are not left where they were. The move must not damage that information:

- The existing pages move with their structure and content unchanged, their subtree reproduced
  below the Venture root, each by the two steps of "Moving pages".
- If the existing content already has a root page (an overview), it becomes the Venture root page
  unchanged except for a link to the Venture page. Nothing below it is rewritten.

The only later restructuring is the one the Venture definition itself calls for, such as the
Venture's basic Departments. It is never part of the move: move first, then propose any
restructuring as a separate, approved step that names the Venture rule requiring each change.
Never overwrite, move, or delete an existing page without the human's approval of that item.
