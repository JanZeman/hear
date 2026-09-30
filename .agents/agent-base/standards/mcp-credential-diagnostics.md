# MCP Connection Diagnostics

Use this procedure when a managed MCP connection fails, especially when the host reports
`connection closed`, an authentication-shaped error, or a tool is missing. Establish the last
successful stage before choosing a cause. A command exit code, an empty resources list, or a fresh
server process alone does not prove that the managed connection works.

## Stages and evidence

Record each stage as `succeeded`, `failed`, or `not checked`:

1. **Host configuration**: the target server entry is present, enabled, and selected in the
   intended runtime configuration. Use the runtime's native configuration/status view.
2. **Launcher execution**: the configured executable starts in the environment supplied to it.
   Check executable resolution and environment-variable presence, never values.
3. **MCP `initialize`**: the fresh server responds to the protocol initialization request.
4. **`tools/list`**: the server responds to tool discovery. An empty `resources/list` says nothing
   about whether tools exist.
5. **Optional read-only service call**: call one specifically selected read-only tool only when
   that call is authorized. Do not use a write-capable or uncertain tool as a diagnostic probe.

Report the last stage that succeeded for both the fresh process and managed host. State unknowns
directly. A failed stage does not establish the status of later stages.

## Credential delivery: presence only

Keep these independent statuses:

| Check | Allowed statuses | Evidence |
| --- | --- | --- |
| Secret source | `present`, `missing`, `denied`, `not checked` | Operator-local source check that returns status only |
| Broadcast/session environment | `present`, `missing`, `denied`, `not checked` | Presence check in the environment receiving the operator's broadcast |
| Launcher environment | `present`, `missing`, `denied`, `not checked` | Presence check in the environment actually inherited by that launcher |

Never print, copy, compare, or persist credential values. If a source lookup is denied by a
sandbox, report `denied`; it does not prove the source is missing. Keep host-managed launcher
environment as `not checked` unless a runtime-native or operator-local adapter can inspect that
process boundary. The diagnostic's own environment is not evidence about an already-running host.

Prefer restoring delivery through the existing operator-local broadcast mechanism when the source
is present but a session or launcher environment is missing. Do not suggest credential rotation
for a missing environment variable.

## Fresh process and managed host

Run the portable diagnostic for a fresh stdio server process:

```bash
bash scripts/diagnose-mcp-credential-failure.sh \
  --host-config present \
  --source-status present \
  --broadcast-env NAME \
  --launcher-env NAME \
  -- launcher-executable launcher-argument
```

The direct probe covers stdio servers using the `initialize` handshake (protocol revision
`2025-11-25` and earlier). It does not probe remote transports or modern-only protocol revisions;
for those, use the native host or an operator-local adapter and keep unobserved stages `not
checked`.

The command, arguments, environment names, stdout, stderr, tool arguments, and service content are
not printed. The optional `--source-check /path/to/operator-local-checker` accepts exit `0` for
present, `1` for missing, and `77` for denied; other statuses mean `not checked`. It discards all
checker output. Make the operator-local checker map sandbox-denied lookups to `77`. Do not make a
checker print or return a credential value as its status.

The script performs `initialize` and `tools/list` and reports their outcomes without displaying
server responses or tool definitions. Protocol response lines are capped at 1 MB and read-only
argument files at 64 KB; larger inputs fail as unverified. A read-only call requires all three
explicit options:

```bash
bash scripts/diagnose-mcp-credential-failure.sh \
  --authorize-read-only-call --read-only-tool TOOL_NAME \
  --read-only-args-file /secure/path/args.json \
  -- launcher-executable launcher-argument
```

The arguments file must contain a JSON object and should be readable only by its owner. Its path
and contents are not printed. The human or agent invoking the option must already have authority
for that exact read. The script reports only whether the call succeeded or returned an
authentication-shaped error.

The script cannot inspect another process's private environment or ask an arbitrary runtime what
its managed MCP connection did. After direct probing, use that runtime's native interface to
check the managed host and report only its stage statuses with `--managed-initialize`,
`--managed-tools-list`, and, when authorized, `--managed-read-only-call`. Use
`--broadcast-status` and `--launcher-env-status` when an operator-local adapter supplies those
presence-only findings; each accepts `present`, `missing`, `denied`, or `not-checked`. The
`--broadcast-env` and `--launcher-env` options check the diagnostic process environment and the
environment inherited by its fresh child. These are observations, not substitutes for making the
calls in the host.

When a fresh process succeeds and managed initialization or a managed tool call fails, the server
has demonstrated that it can start; the host integration remains broken or unproven. If the
managed launcher inherited no credential variable, restore it through the existing broadcast
mechanism, fully quit and relaunch the host process that owns the MCP connection, then repeat the
managed `initialize`, `tools/list`, and authorized read-only call. A new chat/session/window inside
the same process is not a full restart. A successful direct call is evidence about the fresh
server, not proof that the managed host is repaired.

For the currently Primary Claude Code runtime, inspect configuration/status with `claude mcp list`
and `/mcp`, then verify actual tools with an in-session managed call. Read status locally; never
echo full CLI output because it can include server-reported error detail. For the currently Primary
Codex runtime, inspect configuration with `codex mcp list` and verify the managed connection using
a tool call in the active host. Configuration-list success alone does not prove `initialize`,
`tools/list`, or a service request succeeded. The local support profile at
`${XDG_CONFIG_HOME:-$HOME/.config}/agent-base/vendor-support.json` is the source of Primary
runtimes and must be checked when this coverage is revised.

## Verdict and next action

Use the script's single `VERDICT` and `NEXT` result, with the stage table above as evidence:

- `connection closed` during `initialize`: startup/launcher failure first. Inspect launcher setup
  and local redacted diagnostics. Do not call this service authentication failure without an
  actual service tool request returning authentication rejection.
- Launcher exits before `initialize`: check executable resolution, launch prerequisites, and the
  launcher's local stderr. Then rerun the fresh protocol probe.
- Sandbox denies a source lookup: keep source status `denied`; retry that presence-only check via
  an authorized local adapter. Never call it missing.
- Source is present, environment is missing: repair the existing broadcast path, fully restart the
  host, and retest through its managed MCP connection.
- Fresh direct process succeeds, managed host fails: inspect managed host configuration and
  inherited environment, repair delivery, fully restart, then repeat managed calls.
- `initialize` succeeds but `tools/list` fails: inspect protocol compatibility and startup logs.
- Only an actual service request that returns an authentication-shaped error supports an
  authentication diagnosis. Verify source, broadcast, launcher environment, request target, and
  permissions before considering credential rotation.
- Managed `initialize`, `tools/list`, and an authorized read-only call all succeed: the managed
  connection is established for this test. Report the exact call's bounded success status only.

## Runtime documentation

- Claude Code MCP management: [MCP servers](https://code.claude.com/docs/en/mcp)
- Codex MCP configuration and status: [OpenAI developer docs MCP guide](https://developers.openai.com/learn/docs-mcp)
- Protocol behavior: [MCP specification](https://modelcontextprotocol.io/specification)
- Credential handling: `AB-MODEL-001` and `.agents/agent-base/standards/secrets.md`
