---
name: venture-review
description: Review an existing Venture hierarchy for internal coherence when the guard says the periodic review is due or the human asks; report only actionable findings and hand human-approved repairs to venture-discovery.
---

# Venture Review

Review whether an already approved Venture still hangs together. This is diagnosis, not discovery:
do not invent motives, rewrite human intent, or turn a clean review into routine reporting.

## Boundary

- `venture-discovery` elicits or repairs Identity, Purpose, Vision, Strategy, and Goals with the
  responsible human. This skill only detects possible incoherence and proposes what to revisit.
- `ab-quality-check` judges the Agent Base/downstream relationship and its registered quality
  conditions. This skill judges the downstream Venture's own hierarchy.
- `venture-wiki-sync` reconciles repository documents with their wiki mirrors. This skill reads
  repository-authoritative content and performs no wiki operation.

## Review

1. Confirm the review is due with
   `python3 scripts/venture.py --root . check-due --kind review`, unless the human explicitly
   requested it now.
2. Read `.agents/VENTURE.md`, `.agents/goals/_GOALS_CATALOG.md`, every current objective file, the
   active roadmap catalog, and roadmap items carrying `**Venture exception**:`.
3. Check only these coherence conditions:
   - Strategy still serves Vision rather than a previous direction.
   - Current Goals measure progress toward Vision and each is traceable to Purpose or Vision.
   - Accumulated Strategy choices have not silently rewritten Purpose.
   - No live roadmap item cites a superseded objective.
   - Each objective's `Last reviewed` date and falsifying evidence remain credible.
   - The generated Venture Summary represents the current sources without hiding a material
     choice; verify with `python3 scripts/venture.py --root . summary`.
4. Treat one explicit Venture exception as an approved local trade-off. Report a repeated pattern
   or contradiction, not the mere existence of an exception.
5. Report only actionable findings with the conflicting entities and exact evidence. Findings are
   non-blocking. If the hierarchy is coherent, continue silently.
6. After the real review, run
   `python3 scripts/venture.py --root . mark-checked --kind review`. The timestamp is local cadence
   state, not a claim that future drift cannot occur.

When a finding requires changing approved Venture content, present it to the human and load
`venture-discovery` for the repair. Never apply that repair from this review alone.
