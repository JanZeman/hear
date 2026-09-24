# Fix Log

Canonical workflow for promptly resolving a defect while preserving concise, reusable evidence.

## When a bug is reported

Repair and verify the bug within the approved scope. A reported bug, including one reported by a
customer, does not need its own roadmap item before work begins. After verification, create one
short record under `.agents/fixes/` from `_FIX_TEMPLATE.md` and add it to `_FIXES_CATALOG.md`.

Record only the symptom, cause, fix, verification, affected area, date, and source class
(`Customer` or `Internal`). Never record a customer identity, private conversation, secret, or
unnecessary reproduction data.

## When to use the log

Before fixing another bug or making related nontrivial code changes, search locally first:

```bash
rg -n -i '<relevant terms>' .agents/fixes
```

Read only matching entries. A prior fix may reveal an existing cause, accepted tradeoff, regression
pattern, or verification method; it does not replace current investigation.

## Roadmap boundary

The concise fix record is the durable record for an ordinary resolved bug. Create a roadmap item
only when the bug also exposes broader product, architectural, operational, or policy work that
cannot be completed as part of the immediate repair. Link that item from the fix record, but do not
delay the repair for its creation.
