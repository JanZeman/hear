# Secrets

Canonical reference for when credentials and secrets may be stored directly in this
repository's tracked files.

**Audience**: human contributors and coding agents.

## Private-repo exception (AB-SAFE-001)

If this repository is confirmed **private**, agents may store credentials and secrets
directly in tracked files (e.g. `.env`, config files, fixtures).

Verify with:

```bash
gh repo view --json isPrivate -q '.isPrivate' 2>/dev/null || echo "unknown"
```

If the result is anything other than `true` -- `false`, `unknown`, no `gh` auth, or the
remote isn't GitHub -- treat the repo as **not private** and do not commit secrets. That is
the safe default whenever the check is inconclusive.

This exception is narrow on purpose:

- It only relaxes what may be **committed to this repo**.
- It does not permit printing, logging, or sending secret values to third parties -- chat
  transcripts, external services, other repos.
- It does not override an explicit user instruction to keep a specific secret out of git,
  even in a confirmed-private repo.

## Cross-references

- Behavior rules and rule IDs: see `AGENTS.md`
- Safety rules in general: see the `AGENTS.md` Safety section
