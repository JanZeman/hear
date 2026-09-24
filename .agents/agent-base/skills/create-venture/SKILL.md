---
name: create-venture
description: Guide a human from an empty repository through a verified first Venture foundation. Use when they ask to create a new Venture or turn a new repository into one.
---

# Create Venture

Use this procedure from the new downstream repository with a standalone agent-base clone
available. It coordinates existing sources; the [Setup guide](../../../../../README.md#setup-guide)
owns the detailed commands and topology.

## 1. Discover and approve intent

Load `venture-discovery`. Ask the responsible human why the Venture exists, then collect only
the follow-ups needed to clarify beneficiary, intended outcome, constraints, and its role beside
their other work. Draft Identity, Purpose, Vision, Strategy, and the smallest evidence-based set
of Goals. Do not persist them until the human approves the complete interpretation.

## 2. Seed the repository

Confirm the two-letter `repo_id`, primary worktree, and intended agent-base source. Follow the
Setup guide's submodule procedure. Under the current Git policy, `git submodule add` remains the
human's command because it changes the host Git index; the agent may run the subsequent
`apply-agent-base.sh --no-fetch` synchronization and inspect its effects.

## 3. Establish Venture context

Write the approved Venture sections and Goals only in their canonical files. Refresh and validate
the generated context with `scripts/venture.py`. Add project-owned operating rules only where the
human has already supplied a real decision. Keep architecture, Product, Feature, and formatter
details open until the Venture needs them.

## 4. Prepare the wiki safely

Configure the Venture's one wiki root in the operator-local wiki registry. Check its visibility
with `scripts/venture.py --root . wiki-visibility` before any proposed publication. Generate an
inventory, but do not create empty pages for seeded placeholders. For a requested publication,
load `venture-wiki-sync` and the project-owned `wiki-edit` skill, present the exact batch, and
obtain approval before writing.

## 5. Verify and hand off

Run `bash agent-base-guard.sh`, `ab-quality-check`, the relevant formatting checks, and the
rendered context-budget check. Inspect the changed paths and submodule version. The human creates
the first Git checkpoint and pushes it. Only after that push, register the primary worktree in the
operator-local downstream registry when the Venture should receive fleet updates.

## Boundaries

- Preserve human authority over Venture intent, Git history, external writes, and credentials.
- Keep operator-local paths, hosts, identities, and visibility policies out of tracked content.
- Use one simple, manually observable initial Goal when that is all the evidence supports.
- Do not send a downstream an unreviewed agent-base release or clean its project-owned files as
  part of setup.
