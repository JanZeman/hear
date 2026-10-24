---
name: venture-wiki-sync
description: Inspect or reconcile the canonical Venture repository documents with their deterministic wiki mirrors. Use when the daily Venture wiki check is due, the user asks to sync or migrate Venture wiki content, or a repository/wiki discrepancy is encountered. Never use it to write on every repository edit.
---

# Venture Wiki Sync

Keep the repository and human-facing wiki useful as two editable representations without turning
normal work into continuous synchronization. The repository remains operationally authoritative;
wiki edits are valid incoming proposals, not disposable copies.

Read `.agents/agent-base/standards/venture-operating-model.md` first. Use
`python3 scripts/venture.py --root . inventory` for the canonical repository inventory and
deterministic relative wiki paths. Resolve the connector and Venture root with
`python3 scripts/venture.py --root . wiki-config`; its source is this repository's own
`.agents/wiki.json`, a permanent property of the repository. Never write credentials or absolute
URLs there. If it is absent, ask for the connector, root, host, and visibility only when a wiki
operation is requested, then write the file; ordinary repository work continues. If the mapping
still lives only in a legacy operator-local `wikis.json`, run
`python3 scripts/venture.py --root . migrate-wiki` and commit `.agents/wiki.json`.

Before a proposed wiki write, run `python3 scripts/venture.py --root . wiki-visibility` for the
mapped root and classify every target path under it. Use a known result without re-asking about
visibility; resolve `unknown` with the human before publishing. The procedure and policy schema
are in `.agents/agent-base/standards/wiki-visibility.md`. Privacy classification
does not replace the batch's explicit write approval below.

## What belongs on the wiki

Read "What the wiki is for" in `venture-operating-model.md` first: the wiki is the human's window
into the Venture, the repository is the superset, and nothing agents need in order to work ever
appears on the wiki. By default the inventory maps only what describes the Venture to its
people: `VENTURE.md`, goals, products and features, departments, resources, and feedback. Skills, standards, and workflows
instruct agents and are not mirrored; one a human really reads (a runbook, a procedure the operator
follows by hand) is added to the file's `mirror` list, by `.agents`-relative path or by a directory
ending in `/`. Agent-base framework content is never mirrored. Before proposing a page, name its
reader; a page with no reader is not proposed, even when the inventory lists it.

Never overwrite, move, or delete an existing wiki page without the human's approval of that item.
Only pages recorded in `.agents/WIKI_SYNC.json` are sync-owned.

## Read-only check

Run at most once per 24 hours at the first available session-start or resume boundary. A clean
result stays silent. If a connector or mapping is unavailable, do not block ordinary work and do
not pretend a comparison occurred.

1. Confirm the check is due with `python3 scripts/venture.py --root . check-due`.
2. Inventory repository documents and read the tracked `.agents/WIKI_SYNC.json` baseline if it
   exists.
3. Through the configured wiki connector, read only the deterministically mapped pages. Do not
   crawl unrelated wiki content.
4. Normalize Markdown before hashing. Compare repository, wiki, and baseline states.
5. After a real comparison, run `python3 scripts/venture.py --root . mark-checked`. This updates
   ignored local throttle state only.

No differences produce no user message. Differences produce one short, non-blocking offer stating
the number of affected pages and asking whether to prepare reconciliation now. Postponement never
blocks repository work.

## Prepare a batch

Classify each mapped page:

- repository-only change: propose publishing it;
- wiki-only change: propose incorporating it into the repository first;
- compatible two-sided change: propose a merged repository result and republication;
- material, incompatible, uncertain, or baseline-less two-sided change: require human resolution;
- one-sided absence of a sync-owned page: deletion candidate requiring human confirmation;
- strongly evidenced content-preserving rename: propose a move; otherwise treat it as uncertain.

First adoption (no `.agents/WIKI_SYNC.json` yet) always starts with a read-only map, before any
batch is proposed: list every existing page under the mapped root and under any older location the
human or the Venture's documents name, read them, and say what each holds. Then present one
concise plan: for each proposed page its reader, source, path, Markdown conversion, and conflict
with existing content; how each existing page becomes known to the repository; and rationale and
risks. Keep the existing pages and their structure; propose a restructuring only where the
Venture's definition requires it, naming that rule for each change ("Converting an existing wiki"
in `venture-operating-model.md`). Never
invent a placeholder Product or automatically move flat `.agents/features/` content. Clear items
may proceed while ambiguous items remain untouched.

## Approval and writes

The read-only check authorizes no write. Before an accepted batch, show the proposed repository
changes, wiki operations, deletions or moves, uncertainties, and new baseline effect. Wait for the
current user's explicit approval.

For each approved item:

1. Write or merge the canonical repository document first.
2. Verify its Markdown and deterministic mapping.
3. Publish the normalized repository result to the wiki.
4. Verify the wiki result.

Only after the whole accepted batch succeeds, write `.agents/WIKI_SYNC.json` with schema version,
relative repository and wiki paths, normalized hashes, and successful-sync time. Never store
content copies, secrets, host configuration, credentials, or absolute URLs. If execution reveals a
materially different fact, pause only that item for renewed human approval; do not roll uncertainty
into the rest of the batch.

## Stop conditions

Stop the affected item without destructive repair when the baseline cannot be recovered, Markdown
conversion may be materially lossy, mapping is ambiguous, connector results are incomplete, a
deletion lacks confirmation, or repository and wiki cannot be verified to agree. Report the facts
and a recommendation. Do not conceal divergence or establish a false baseline.
