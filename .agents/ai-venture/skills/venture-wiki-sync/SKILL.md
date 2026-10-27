---
name: venture-wiki-sync
description: Publish the Venture page (Venture Definition and Goals) to the Venture's wiki, check the daily wiki notice, or move existing wiki content onto the Venture root. Use when the daily Venture wiki check is due or the user asks to publish, check, or move Venture wiki content. Never use it to write on every repository edit.
---

# Venture Wiki Sync

**The wiki is for the human.** Read "What the wiki is for" in
`.agents/ai-venture/standards/venture-operating-model.md` first. Whoever writes on the wiki, human
or agent, writes for a person. Nothing that exists for agents ever appears on it.

The repository owns exactly one page on the wiki: the **Venture page**, titled "<Venture name>
Venture", with the Venture Definition (Identity, Purpose, Vision, Strategy) and the Goals. It is
the Venture root page itself: the `venture_page` that `python3 scripts/venture.py --root .
wiki-config` prints. It is published one way, repository to wiki. Everything else on the wiki
belongs to the human and is never synchronized. Paths and links
carry no locale prefix such as `en/`.

Resolve the connector and Venture root with `python3 scripts/venture.py --root . wiki-config`; its
source is the repository's own `.agents/wiki.json`. If the mapping still lives only in a legacy
operator-local `wikis.json`, run `python3 scripts/venture.py --root . migrate-wiki` and commit
`.agents/wiki.json`. If there is no mapping, ask for the connector, root, host, and visibility only
when a wiki operation is requested; ordinary repository work continues.
`python3 scripts/venture.py --root . inventory` lists what the repository publishes.

Before any wiki write, run `python3 scripts/venture.py --root . wiki-visibility` and classify every
target path; resolve `unknown` with the human first
(`.agents/ai-venture/standards/wiki-visibility.md`). Before writing any page, load
`.agents/skills/wiki-edit/SKILL.md`.

## Daily check

Run at most once per 24 hours at the first session-start or resume boundary, when
`python3 scripts/venture.py --root . check-due` says it is due. Read only the Venture page at the
resolved `venture_page` path. If it
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
   exist yet, check that no other page already occupies its path.
3. The page starts with the framework info box from the standard. If the root page holds anything
   besides the Venture page, or a separate old `<root>/<name>-venture` page exists, convert first:
   propose item by item where everything else goes (usually a page below the root), move it as
   "Move existing content" says, and only then publish onto the root and retire the old page.
4. Show the exact pages and changes, get the human's approval, write, and verify the result.

## Move existing content

Follow "Where a Venture lives on the wiki", "Moving pages", and "Converting an existing wiki" in
`venture-operating-model.md`.

1. Verified full list first, read-only. List every page under the old location and under the
   Venture root with its id, path, title, editor format, and history length, and count them.
   Connector listing, tree, and search calls can truncate results or ignore path filters; confirm
   the list by a second independent source (another listing, or a read-only query of the wiki's
   own database when the human allows it) before relying on it. Keep the list as the baseline.
2. Plan: old path to new path for every page, structure and content unchanged; the existing
   overview, if there is one, moves to a page below the root, because the root is the Venture page.
   Verify that every page the plan names exists before proposing a change to it. Pages that only
   duplicate the Venture page (such as old Goal pages) are proposed for deletion instead of moving.
3. Plan the link fixes; they are most of the work. Look in:
   - wiki pages: absolute and relative links, and prose that mentions a page path;
   - the repository: README, scripts, configuration, open roadmap items (never session snapshots
     in `.agents/sessions/`);
   - other wikis on the same host that link here;
   - databases of applications that store a wiki link. Serialized values there break under a plain
     SQL text replace because their stored lengths change; use the application's own
     search-and-replace tool after a dry run, and only with the human's consent on production.
   Write new links without a locale prefix.
4. No restructuring in the same step. Department restructuring, if the Venture definition calls
   for it, comes later as a separate, approved step naming the rule that requires each change.
5. Nothing is written until the human approves the plan. Move each page with the wiki's native
   move when the connector offers one; otherwise copy, show the copy, and delete the original only
   after the human's answer, saying first that history and editor format may be lost.
6. Verify against the baseline: the same count and ids at the new paths, nothing left at the old
   ones, and no link to an old address. Report links you could not verify to the human.

An outcome of "nothing to delete" or "nothing to move" is a valid result; report it as such.

## Stop conditions

Stop the affected page without destructive repair when its content could be lost in conversion,
the target is ambiguous, connector results are incomplete, or a deletion lacks confirmation. Report
the facts and a recommendation.
