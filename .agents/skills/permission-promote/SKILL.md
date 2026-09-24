---
name: permission-promote
description: |
  Review repeated permission interruptions and promote only the narrow, sandbox-consistent
  grants that remain after the sandbox-first autonomy configuration is working. Use on
  "/permission-promote", a Stop hook reporting a repeated candidate, or a normal operation
  repeatedly escaping the configured sandbox.
---

# permission-promote

Review repeated permission interruptions and close only gaps that remain after the sandbox-first
autonomy configuration is working.

## When to invoke

The user runs `/permission-promote`, the Stop hook reports a repeated candidate, or an otherwise
normal operation repeatedly escapes the configured sandbox.

## Steps

### 1. Verify the autonomy baseline

Run from the repository root:

```bash
python3 scripts/agent-autonomy.py status --vendor all
```

Resolve `MISSING` configuration before proposing command permissions. Report `WARN` entries as
project or IDE precedence problems. Do not compensate for a disabled sandbox, missing Auto-review,
or wrong Claude permission mode with a broad allowlist.

### 2. Load interruption evidence

```bash
python3 scripts/human-interruptions.py recommend --days 30
```

Treat output as evidence, not authorization. Include every refused recommendation and its reason.
Show Codex candidates in the report, but do not edit `.codex/rules/`; the human applies an
accepted rule through Codex's normal rule workflow.

### 3. Strip and read Claude session entries

```bash
REPO_ROOT="$(git rev-parse --show-toplevel)"
bash "$REPO_ROOT/_sub/agent-base/template/scripts/permission-auto-strip.sh"
```

Read `.claude/settings.local.json`. If neither its `permissions.allow` list nor telemetry has a
candidate, report `Nothing to promote.` and stop.

### 4. Classify every candidate

Use this order and choose the first safe resolution:

1. **Already sandboxed** - discard the command rule; the configured vendor should auto-allow it.
2. **Sandbox boundary gap** - propose the narrowest filesystem path or network domain needed.
3. **Reviewer or precedence problem** - fix Auto-review/Auto mode or a project/IDE override.
4. **Stable unsandboxed exception** - only then propose one narrow command rule.
5. **Dangerous or unclear** - refuse persistent permission and explain why.

Never consolidate unrelated commands into family-wide rules such as `Bash(curl:*)`,
`Bash(python3:*)`, or `Bash(rm:*)`. Do not weaken mutating Git, destructive-action, credential,
network, system-wide, or external-write protection merely to reduce the count.

### 5. Present one grouped review

Batch all candidates into one human round trip. For each item show its full descriptive name on
first mention, evidence count, proposed resolution and scope, expected prompt reduction, and
security tradeoff. The available decisions are accept, defer, or refuse. A deferred item stays a
recommendation; a refused item remains visible with the refusal reason.

### 6. Apply accepted Claude exceptions

Prefer sandbox configuration (`sandbox.filesystem.*` or `sandbox.network.allowedDomains`) over
`permissions.allow`. User-wide exceptions belong in `~/.claude/settings.json`; project-specific
exceptions belong in `.claude/settings.json`. Preserve unrelated configuration.

If a genuine narrow command rule is still required, merge a user-wide rule with the agent-base
helper or edit the project file directly:

```bash
REPO_ROOT="$(git rev-parse --show-toplevel)"
MERGE_PY="$REPO_ROOT/_sub/agent-base/template/scripts/merge-permissions.py"
TMP="$(mktemp /tmp/permission-promote-XXXXXX.json)"
echo '{"permissions":{"allow":["ENTRY"]}}' > "$TMP"
python3 "$MERGE_PY" --source "$TMP" --target ~/.claude/settings.json \
  --label machine-wide --warn-removals
rm "$TMP"
```

The human's grouped decision authorizes only the listed changes. It never authorizes full access,
new credentials, a commit, or a push.

### 7. Clean and report

Remove handled entries from `.claude/settings.local.json`; leave unrelated entries untouched. Run
`bash "$REPO_ROOT/_sub/agent-base/template/scripts/lint-settings.sh"` after project changes.
Report accepted, deferred, and refused items, affected scope, expected reduction, and whether
validation passed. Do not ask a new question merely to confirm the report.
