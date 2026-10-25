---
name: venture-wiki-sync
description: Publish the Venture page (Venture Definition and Goals) to the Venture's wiki, check the daily wiki notice, or move existing wiki content onto the Venture root. Use when the daily Venture wiki check is due or the user asks to publish, check, or move Venture wiki content. Never use it to write on every repository edit.
---

# Venture Wiki Sync

**The wiki is for the human.** Read "What the wiki is for" in
`.agents/agent-base/standards/venture-operating-model.md` first. Whoever writes on the wiki, human
or agent, writes for a person. Nothing that exists for agents ever appears on it.

The repository owns exactly one page on the wiki: the **Venture page** at `<root>/venture`, titled
"<Venture name> Venture", with the Venture Definition (Identity, Purpose, Vision, Strategy) and the
Goals. It is published one way, repository to wiki. Everything else on the wiki, the root page
included, belongs to the human and is never synchronized. Paths carry no locale prefix such as
`en/`.

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
`python3 scripts/venture.py --root . check-due` says it is due. Read only the Venture page. If it
is missing or differs from what the repository would publish, make one short, non-blocking offer
to publish. Otherwise stay silent. Then run `python3 scripts/venture.py --root . mark-checked`. If
the connector or mapping is unavailable, do not block work and do not pretend a check happened.

## Publish

1. Render the Venture page for a person from `.agents/VENTURE.md` and `.agents/goals/`: Identity,
   Purpose, Vision, and Strategy, then each current Goal with its objective, status, key results,
   and why they were chosen. Leave out dated notes and rejected formulations; they stay in the
   repository.
2. Read the current Venture page. If it was changed by hand, show the human the difference first;
   take what they want to keep into the repository before publishing over it. If the page does not
   exist yet, check that no other page already occupies `<root>/venture`.
3. Check the root page links to the Venture page. If there is no root page, propose one holding
   only a title and that link; if it exists without the link, propose adding just the link. The
   root page is the human's: never change anything else on it.
4. Show the exact pages and changes, get the human's approval, write, and verify the result.

## Move existing content

Follow "Where a Venture lives on the wiki", "Moving pages", and "Converting an existing wiki" in
`venture-operating-model.md`.

1. Read-only map first: list every existing page that describes this Venture, under its old
   location and under the Venture root, read them, and say what each holds and what it links to.
2. Plan: old path to new path for every page, structure and content unchanged; the existing
   overview becomes the root page with only a link to the Venture page added; the links to
   rewrite to the new paths. Pages that only duplicate the Venture page (such as old Goal pages)
   are proposed for deletion instead of moving.
3. No restructuring in the same step. Department restructuring, if the Venture definition calls
   for it, comes later as a separate, approved step naming the rule that requires each change.
4. Nothing is written until the human approves the plan. Then move each page in two steps: copy
   it, show the human the copy and ask whether the original may be deleted; delete the original
   only after that answer, and verify.

## Stop conditions

Stop the affected page without destructive repair when its content could be lost in conversion,
the target is ambiguous, connector results are incomplete, or a deletion lacks confirmation. Report
the facts and a recommendation.
