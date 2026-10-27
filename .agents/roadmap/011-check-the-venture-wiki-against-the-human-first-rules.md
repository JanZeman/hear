# Check the Venture wiki against the human-first rules

**Status**: Open
**Milestone**: -
**Depends on**: -

<!-- AI-VENTURE-MIGRATION: venture-wiki-checklist -->
<!-- Auto-created by apply-ai-venture.sh. Introduced in: v1.0.338. -->
<!-- Detected during sync to: ai-venture v1.0.351. If that's much later than v1.0.338, this
     repo skipped several ai-venture releases before catching up -- check whether any other
     AI-VENTURE-MIGRATION items were created in the same sync; they'd share this same date. -->

## What needs to happen

The wiki is for the human: whoever writes it, human or agent, writes for a person. The repository
publishes exactly one page there, one way: the Venture page, titled "<Venture name> Venture",
with the Venture Definition and the Goals. Its path is the venture_page that
python3 scripts/venture.py --root . wiki-config prints (by default <root>/<name>-venture).
Everything else on the wiki, the root page included, belongs to the human and is never
synchronized. Read "What the wiki is for" in
.agents/ai-venture/standards/venture-operating-model.md and the venture-wiki-sync skill, then go
through this checklist once, together with the human, in their next session. Change nothing on the
wiki without their approval. Move pages with the wiki's native move where one exists, so history
and format survive; fixing the links is part of every move. A step that finds nothing to do is a
valid result.

1. Location: the root in .agents/wiki.json is under the wiki's Venture area (ventures/<name> on a
   shared host), with no locale prefix such as en/. If the root uses an abbreviation instead of
   the Venture's full name, only note it: the whole Venture (repository, folders, wiki root,
   processes) is renamed later in one session, once ai-venture supports full-name ids.
   If this Venture's pages live elsewhere or under the old root, plan the move per "Move existing
   content" in venture-wiki-sync: a verified full list first, structure unchanged, links fixed,
   no restructuring in the same step. Update .agents/wiki.json once the pages are at the new root.
2. Venture page: if the default page name reads badly, set venture_page in .agents/wiki.json.
   Publish the page with venture-wiki-sync. If the root page carries a copy of the Venture
   definition, propose moving that content onto the Venture page; the rest of the root page stays
   as the human wrote it.
3. Root page: it links to the Venture page with the framework info box from the standard. If there
   is none, propose one holding only a title and that box.
4. Old Goal pages and other agent material: list wiki pages that duplicate the Venture page (such
   as Goal pages) or exist for agents (skills, standards, catalogs, roadmap, ai-venture content).
   Propose deleting each; the human decides page by page.
5. Human pages: everything else stays as the human wrote it; note anything that reads as a file
   dump rather than a page for a person, and leave fixing it to the human.
6. Repository: delete .agents/WIKI_SYNC.json if it exists and remove any "mirror" key from
   .agents/wiki.json; neither is used any more.
7. Verify with: bash ai-venture-guard.sh

## Definition of done

- [ ] Migration completed as described above.
- [ ] Verified with: `bash ai-venture-guard.sh`

## Notes

- 2026-10-27: Auto-created by apply-ai-venture.sh. This migration was introduced in
  v1.0.338; this repo was syncing to ai-venture v1.0.351 when it was found still pending.
  Registering that the work exists is automatic. If the migration requires deciding what
  old content becomes, that decision still needs a human.
