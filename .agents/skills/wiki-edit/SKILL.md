---
name: wiki-edit
description: |
  Find and update Wiki.js pages, including mirroring a Venture's identity, purpose, or a
  completed workflow onto its wiki. Use whenever writing to any wiki page. Load this before
  every wiki write, not only for large edits: the failure it exists to prevent, writing a
  page's content in the wrong format for its actual editor, happens on a single full-content
  write just as easily as a large one.
---

# wiki-edit

A skill for finding and updating pages in a Wiki.js wiki.

## When to invoke

Use this skill when:

- The user asks to update, write, or correct a wiki page
- A completed task should be documented on the wiki
- A wiki page describing a procedure needs to reflect a changed approach

Do NOT use this skill when:

- The user is asking what the wiki says (just read the page, no skill needed)

## Steps

### 1. Orient

Use `wiki_search` or `wiki_get_tree` to locate the target page before touching anything.
If the user named the page directly, confirm the path exists before reading.

### 2. Read the full page - and its format

Fetch the current content with `wiki_get_page`. Never edit a page you have not read in
this session. Also note the page's editor and content type from the response (e.g.
`markdown` vs. `html`/`ckeditor`) - do not assume it from one sample page. A single wiki
commonly mixes formats: newly created pages default to Markdown, while older pages are
often `html`/`ckeditor` or plain text. Every page's format must be checked on its own.

### 3. Pre-check: automation before manual steps

If the page documents operational procedures (deployment, setup, configuration, install):

- List every manual command or step currently on the page.
- For each one, ask: does a script, Makefile target, CI workflow, or tool already do this?
  Check the project repo (root scripts, sub-scripts shipped with the app, CI config) and
  any platform-level tooling the project documents.
- If automation covers a step, replace the expanded commands with the single invocation.
  Do not repeat inside the wiki what a script already encodes - that duplication diverges
  over time and misleads future readers.
- Only write out manual commands for steps that have no automation equivalent.

### 4. Apply surgical edits

Prefer `edits` (find-and-replace pairs) over full content replacement. Reserve full
replacement for pages where the structure itself is being redesigned. Before writing,
check the change against the human readability rules below.

This preference has a second, sharper reason beyond avoiding redundant rewrites: a
find-and-replace pair inherits the surrounding format automatically, because it only
touches the text next to content that is already correct for this page. A full-content
replacement has no such anchor - it is exactly how foreign syntax gets written into a page
of the wrong format (Markdown syntax landing on an `html`/`ckeditor` page, or vice versa).

### 5. Verify

After the update, confirm the page reads as a complete, correct procedure with no
broken references, and that it still satisfies the human readability rules below. Do not
rely on judgment alone for format correctness - check it mechanically: re-fetch the page
with `wiki_get_page(..., include_render=True)` and compare the rendered output against
what you intended. If the rendered HTML still shows the literal syntax you wrote (e.g. raw
`**asterisks**` or a raw `#` heading marker instead of bold text or an actual heading), the
page's format was not what you assumed in step 2 - fix the content to match the page's real
format, then re-verify.

## Human readability rules

A growable checklist, independent of which Wiki.js editor the page uses. Apply every rule
in this list to any content you write or edit, on every page, regardless of topic.

1. **Blank line before every new paragraph.** For Markdown-edited pages, separate every
   paragraph with a blank line in the source - Markdown treats a single line break as no
   break at all, so two paragraph ideas written on adjacent lines collapse into one dense
   block. For CKEditor/HTML-edited pages, give each paragraph its own block-level element
   (its own `<p>`) rather than merging distinct ideas into one, and don't let a paragraph
   sit flush against the heading, table, or callout that follows it. Never hand a human
   reader a wall of text with no visible paragraph break.

2. **Never let a page's layout depend on whitespace.** Wiki.js normalizes spaces and line
   endings on save, so raw indentation or blank-space alignment is not preserved reliably -
   an ASCII diagram whose shape depends on exact newlines can come back mangled, even inside
   a `<pre>` block, because converting the surrounding tags does not by itself guarantee the
   newlines inside survive the save. For anything with real visual structure (diagrams,
   tables, aligned columns), use real markup for that structure (a Markdown table, a fenced
   code block, an actual `<table>`) instead of relying on whitespace to carry it, and verify
   with `include_render=True` (see step 5) that it still looks right after saving.

## Expected outputs

- Wiki page updated with accurate, non-redundant content
- Procedure pages reference scripts rather than inlining their commands
- Every paragraph break is visible, regardless of the page's editor type
- Page format verified against the rendered output, not assumed
- No page layout silently depends on whitespace that Wiki.js may normalize away
