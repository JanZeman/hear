---
name: venture-wiki-sync
description: Publish the Venture Definition and Goals to the Venture's wiki, check the daily wiki notice, or convert an existing wiki onto the Venture root. Use when the daily Venture wiki check is due or the user asks to publish, check, or move Venture wiki content. Never use it to write on every repository edit.
---

# Venture Wiki Sync

**The wiki is for the human.** Read "What the wiki is for" in
`.agents/agent-base/standards/venture-operating-model.md` first. Whoever writes on the wiki, human
or agent, writes for a person. Nothing that exists for agents ever appears on it.

The repository owns exactly one thing on the wiki: the **Venture Definition** at the top of the
Venture root page (Identity, Purpose, Vision, Strategy, links to the Goals) and the Goal pages
below it. They are published one way, repository to wiki. Everything else on the wiki belongs to
the human and is never synchronized.

Resolve the connector and Venture root with `python3 scripts/venture.py --root . wiki-config`; its
source is the repository's own `.agents/wiki.json`. If the mapping still lives only in a legacy
operator-local `wikis.json`, run `python3 scripts/venture.py --root . migrate-wiki` and commit
`.agents/wiki.json`. If there is no mapping, ask for the connector, root, host, and visibility only
when a wiki operation is requested; ordinary repository work continues.
`python3 scripts/venture.py --root . inventory` lists what the repository publishes.

Before any wiki write, run `python3 scripts/venture.py --root . wiki-visibility` and classify every
target path; resolve `unknown` with the human first
(`.agents/agent-base/standards/wiki-visibility.md`). Before writing any page, load
`.agents/skills/wiki-edit/SKILL.md`.

## Daily check

Run at most once per 24 hours at the first session-start or resume boundary, when
`python3 scripts/venture.py --root . check-due` says it is due. Read only the Venture root page and
the Goal pages. If the Venture Definition or a Goal page differs from what the repository would
publish, or the root page does not start with a Venture Definition section, make one short,
non-blocking offer to publish. Otherwise stay silent. Then run
`python3 scripts/venture.py --root . mark-checked`. If the connector or mapping is unavailable, do
not block work and do not pretend a check happened.

## Publish

1. Render the Venture Definition from `.agents/VENTURE.md` for a person: a section titled
   `Venture Definition` with Identity, Purpose, Vision, and Strategy, and a link to each Goal.
2. Read the current root page and Goal pages. Replace only the Venture Definition section; keep
   everything else on the root page exactly as it is. If the root page has none yet, add it at the
   top.
3. If the wiki copy of the Venture Definition or a Goal was changed by hand, show the human the
   difference first; take what they want to keep into the repository before publishing over it.
4. Show the exact pages and changes, get the human's approval, write, and verify the result.

## Convert an existing wiki

Follow "Where a Venture lives on the wiki" and "Converting an existing wiki" in
`venture-operating-model.md`.

1. Read-only map first: list every existing page that describes this Venture, under its old
   location and under the Venture root, read them, and say what each holds and what it links to.
2. Plan: old path to new path for every page, structure and content unchanged; the existing
   overview becomes the root page with the Venture Definition added at its top; the links to
   rewrite to the new paths; and what happens to the old addresses (the human decides).
3. No restructuring in the same step. Department restructuring, if the Venture definition calls
   for it, comes later as a separate, approved step naming the rule that requires each change.
4. Nothing is written until the human approves the plan. Then move page by page and verify each.

## Stop conditions

Stop the affected page without destructive repair when its content could be lost in conversion,
the target is ambiguous, connector results are incomplete, or a deletion lacks confirmation. Report
the facts and a recommendation.
