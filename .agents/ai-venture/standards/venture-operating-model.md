# Venture Operating Model

Canonical definition of the ai-venture Venture model, its Categories, Department routing, and
human-authority boundaries.

## Venture

A **Venture** is one independently managed, value-producing unit represented by one repository. It
may be a hobby, software product, service, physical operation, infrastructure, tooling, or
company-like business. Software is the common case, but neutral terminology is used where it
remains clear. `Project` is excluded from the formal vocabulary; a bounded coordinated effort inside
a Venture is an Initiative and is normally represented by the Roadmap.

In plain words, a Venture is a project its owner works on steadily, run in one standard way. The
same flows across every Venture build good habits, make work with AI agents efficient, and let the
owner switch focus quickly: every Venture can answer where it is going and what the next step is.
Its value may serve other Ventures, as a utility serves its customers. A Venture "plays" a company
only so that roles can be handed to AI agents or people intuitively.

### Owner and roles

Every Venture has an owner with the final say; a Venture may later be co-owned or handed over.
Management (direction, priorities, Goals, Roadmap) is always staffed, typically by an AI agent with
the owner as the role's ultimate owner. Other Departments are staffed only where they make sense.
A Venture lists its staffed roles in its own files.

### Repositories

The **Venture repo** is the Venture's one primary repository. Only it carries the Venture: its
definition, Goals, Roadmap, and wiki mapping, and agents always start there. `.agents/` is its
descriptive and operating layer, not a second `venture/` container. In its relationship to
ai-venture the same repository is an **AV client**. ai-venture itself is a Venture repo but not a
client. Older text says "downstream" or "DS" for a client; those words are being phased out.

Content belongs in the Venture repo by default. A **secondary repository** needs a reason it cannot
live there, such as being public, having its own release cycle, or its own access. A secondary
repository is neither a Venture repo nor an AV client: it lives inside the Venture repo as a Git
submodule over SSH, `.gitmodules` is the formal list of them, and agents change it from the Venture
repo on request. Everything about a Venture stays private unless there is a reason to publish it.
Dependencies between Ventures may be recorded for information only, never as a duty to keep them
current.

### Names

A Venture has one name in full words, lowercase kebab-case, used for its Venture repo, the clone
folder, the wiki path `ventures/<name>`, and server processes (`<name>-<component>` when it runs
several). Worktree slots are sibling folders `<name>-1`, `<name>-2` on branches `_<name>-1`,
`_<name>-2`; the folder `<name>` is on the trunk. The recommended, unenforced clone location is
`~/Dev/Ventures/<name>`. Renaming an existing Venture happens in one session, in verifiable steps.
The `repo_id` is this name; an older two-letter id keeps working until the human switches it.

### Lifecycle

A repository attached to ai-venture is a **Venture Candidate** until the Venture checklist below is
complete; agents work there normally, but every session start says it is a candidate and names the
unmet points. A Venture may later be **stalled** (only noted), **asleep** (paused), or **closed**:
its repository archived, its wiki pages moved under `ventures/archive/<name>`, and its entry removed
from ai-venture's client list. Stalled and asleep Ventures keep receiving ai-venture updates and
reviews. The status is one line in `.agents/VENTURE.md`; ai-venture's client list mirrors it.

A new Venture starts with an empty repository named by these rules, then discovery with the owner
(`create-venture`), then attaching ai-venture.

### Venture checklist

1. Venture repo named by these rules; secondary repositories, if any, as listed submodules.
2. Definition: Identity, Purpose, Vision, and Strategy filled.
3. Goals as OKRs: Objectives with Key Results.
4. Roadmap in the format ai-venture expects.
5. Venture page published as the Venture root page on the wiki, verified on the wiki itself.
6. Half-year OKR Review on January 1 and July 1, recorded in `.agents/OKR_REVIEWS.md`.
7. Management staffed.

Tooling for points 1, 6, and 7 and for the candidate notice is still being built; until then they
are checked by hand.

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
guard may warn rather than fail during migration so a new ai-venture release does not break every
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

## OKR Review

Every Venture holds its OKR Review twice a year, on January 1 and July 1; always call it by that
full name. There is no per-Venture date to choose. The guard prints the half-year and the next OKR
Review date every session (`scripts/venture.py cycle-status`).

The guard requests the OKR Review 14 days before each date, from December 18 and June 17. A
finished OKR Review is recorded in `.agents/OKR_REVIEWS.md`. When a date passes with nothing
recorded, the guard records that OKR Review as passed automatically and asks for nothing more.
`venture-review` performs the check and never blocks on it: it looks at whether Strategy still
serves Vision, Goals remain traceable to Purpose or Vision, roadmap work cites live objectives, and
accumulated exceptions reveal a contradiction. A coherent review is silent. Findings are
non-blocking and return to `venture-discovery` before approved Venture intent is changed.

A `## Cycle anchor` section left in an older `.agents/VENTURE.md` is no longer read; delete it.

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
- **Requests** are this Venture's change requests against ai-venture itself, recorded under
  `.agents/requests/`. Distinct from Feedback, which is external or internal observation about
  this Venture's own Products. Governed by `AV-REQUEST-001`.
- **Resources** records only durable, significant assets and dependencies whose awareness improves
  decisions or continuity. It excludes secrets, people, Products, exhaustive dependency lists,
  financial ledgers, and disposable supplies.
- **Goals**, **Roadmap**, **Standards**, **Skills**, and **Workflows** retain their existing
  ai-venture contracts. Workflow is the canonical term for repeatable Venture activity; a Skill is
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
`.agents/ai-venture/departments/`. A Venture-specific overlay under `.agents/departments/` exists
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
the roadmap, ai-venture framework content, or any other agent technicality) ever appears on it.

### The Venture page

Every Venture has one page that says what it is: the **Venture page**. It is the Venture root page
itself (for example `ventures/example`), titled with the Venture's name alone (for example "Betty", never "Betty Venture": it already
sits under Ventures), so the first page a
person opens is the one that explains the Venture. Only a host that cannot use its root names a
page below it with `venture_page` in `.agents/wiki.json`;
`python3 scripts/venture.py --root . wiki-config` prints the resolved path. The page holds the
Venture Definition (Identity, Purpose, Vision, Strategy) and the Goals, all on that one page and
written for a person: each Goal with its objective, status, key results, and why they were chosen.
Working history (dated notes, rejected formulations) stays in the repository. The repository owns
exactly this page and nothing else on the wiki: it is rendered from `.agents/VENTURE.md` and
`.agents/goals/` and published one way, repository to wiki. A change is made in the repository and
published again; when the page was changed by hand, the agent shows that difference to the human
before publishing over it.

Everything else on the wiki belongs to the human, the pages below the root included. A person
writes them, and an agent writes them only when asked, for a person to read. Nothing on the wiki
is synchronized or mirrored into the repository; an agent that needs it reads the wiki directly.

The Venture page starts with this info box (Wiki.js markdown):

```markdown
> This page is published from the Venture repository by the "AI-venture Framework". Change it there, not here.
{.is-info}
```

A Venture that still has a separate `<root>/<name>-venture` page, or a root page holding anything
besides the Venture page, is converted once as part of the Venture checklist. The agent proposes,
item by item, where everything else on the root page goes (usually a page below the root), moves
it as "Moving pages" says, then publishes the Venture page onto the root and retires the old
`-venture` page. Nothing is moved or deleted without the human's approval.

### Where a Venture lives on the wiki

Whatever is declared a Venture lives under the wiki's Venture area: on a shared wiki host its root
is `ventures/<venture-name>`, the same place every other Venture on that host uses. The name is
written out in full, never abbreviated, so a person recognizes it in a path. A host dedicated to
one Venture may keep the root documented for it. Connector identity, the root, and the host's
visibility live in the repository's own `.agents/wiki.json`; a mapping is mandatory (`AV-Q006`),
and the guard reports a missing one without naming any actual host or path.

Paths and links carry no locale such as `en/`. A wiki serves its default language without one, so
a locale-free link (`/ventures/<venture-name>/...`) works and survives a language change. Use that
form inside wiki pages and in repository files; only an editor URL that requires the locale keeps
it.

### Moving pages

Every move on the wiki, whether converting existing content, retiring an old page, or renaming a
root, keeps the page's content, format, and history:

- Prefer the wiki's native move. It keeps the page's identity, editor format, content, and
  history; it needs the human's approval like any other change.
- Copy, ask, then delete only across wikis or when no native move exists: create the copy, show
  it to the human, and delete the original only after their approval. Say beforehand what is lost
  that way, such as the page history and possibly the editor format.
- Moving includes fixing every link to the old address, and nothing is moved before a verified,
  complete list of the affected pages exists. `venture-wiki-sync` holds the procedure.

### Converting an existing wiki

When a Venture adopts a wiki that already describes it somewhere else, its existing pages move to
the Venture root; they are not left where they were. The move must not damage that information:

- The existing pages move with their structure and content unchanged, their subtree reproduced
  below the Venture root, each as "Moving pages" describes.
- If the existing content already has a root page (an overview), it moves to a page below the
  Venture root unchanged, because the root is the Venture page. Nothing below it is rewritten. A page named in
  a plan is verified to exist before any change to it is proposed.

The only later restructuring is the one the Venture definition itself calls for, such as the
Venture's basic Departments. It is never part of the move: move first, then propose any
restructuring as a separate, approved step that names the Venture rule requiring each change.
Never overwrite, move, or delete an existing page without the human's approval of that item.
