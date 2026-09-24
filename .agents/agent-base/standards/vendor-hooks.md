# Vendor Hooks

Canonical reference for the hook mechanism agent-base relies on: the abstract points in an agent
session where a small script can run automatically, and exactly how each supported vendor
implements each point today. Distinct from `.agents/hooks.md`, which holds the actual injected
content (the commands and prose each vendor's adapter receives); this file explains the mechanism
those injections run on.

**Audience**: human contributors and coding agents building or debugging a hook.

## The idea, in plain terms

Picture a garden robot. Before it ever drives out onto the lawn, it stops at the gate and looks
around once: is the grass as expected, did anything change overnight, is there a new instruction
waiting? It only does this once, right at the start, and it is the cheapest, most reliable check,
because every robot ever built, whatever factory made it, hears this one the same way.

Then it gets to work, and it has two magic ears plus one small extra one.

The first ear wakes up every single time someone speaks to it, even the tenth time in one
afternoon. It listens quietly, and only when it hears a word like "password" or "secret key" does
it whisper into the robot's mind: "Careful, before you touch that, check whose secret this is."
Otherwise it stays silent, so it never becomes annoying.

The second ear wakes up right before the robot reaches for a spade or a rake. It checks: is this
tool the neighbor's, or ours, and does it look like something we should not just pick up? If so, it
whispers the same warning, but exactly at that moment, not an hour earlier, because by then the
robot might have forgotten.

The small extra ear watches for something else: whenever the robot has unfinished work in the
garden, it quietly reminds it how to write the note for the gardener properly before leaving --
short, clear, and what to do next.

The robot does not remember any of this forever; its memory fades a little the longer it works.
That is why these ears do not fire only once in the morning, but again and again, all day, exactly
when it matters.

## Abstract layer

Three hook points matter today, independent of vendor. Each is a moment in the session lifecycle
where a script can run without the agent having to remember to run it itself.

1. **Session start.** Fires once, when an agent session begins (or resumes). Cheapest and
   broadest: whatever it prints reaches the agent before anything else happens. Cannot react to
   anything that happens mid-session.
2. **Every turn** (the human submits a message). Fires repeatedly, once per user message, before
   the model processes it. Sees the session's working directory and the message text, not what
   tool the agent is about to call. Good for a reminder that must survive a long session, because
   it re-injects fresh content every turn instead of relying on the agent's own recall of
   something loaded many turns earlier.
3. **Before a tool runs** (a specific command or file operation is about to execute). Fires once
   per tool call, sees that call's actual parameters (a file path, a shell command), and can either
   let it through with an added note or block it outright. The only one of the three that can see
   the *specific target* of the next action, not just the general moment.

A fourth category (after a tool runs, session end, compaction) exists on at least one vendor but
has no shipped agent-base mechanism yet; this file gets extended when one does.

## Concrete implementation per vendor

### Claude Code

Configuration lives in `.claude/settings.json` under a `hooks` object, keyed by event name.

| Abstract point | Event name | Notes |
| --- | --- | --- |
| Session start | `SessionStart` | `matcher: "startup"` scopes it to a fresh session, not a resume |
| Every turn | `UserPromptSubmit` | No matcher; fires on every message |
| Before a tool runs | `PreToolUse` | `matcher` selects tool names, e.g. `"Read\|Edit\|Bash"`, or a regex like `"mcp__.*"` |

Every hook receives JSON on stdin. Common fields: `session_id`, `transcript_path`, `cwd`,
`hook_event_name`, `permission_mode`. `PreToolUse` adds `tool_name` and `tool_input`, whose shape
depends on the tool: `Bash` gives `tool_input.command`; `Read` and `Edit` give a clean
`tool_input.file_path`, not embedded in a larger string.

A hook communicates back in one of two ways:

- **Exit code 0** with a JSON object on stdout. For a non-blocking reminder that lets the tool
  proceed but adds a note the agent will see:

  ```json
  {"hookSpecificOutput": {"hookEventName": "PreToolUse", "permissionDecision": "allow"},
   "systemMessage": "Reminder text here."}
  ```

- **Exit code 2**, with the block reason on stderr. Blocks the action entirely; used sparingly,
  since a wrong block on a `UserPromptSubmit` hook can erase the human's message.

### Codex CLI

Configuration lives in `.codex/hooks.json`, same top-level `hooks` object keyed by event name as
Claude Code, but as its own file rather than sharing Claude's settings file.

| Abstract point | Event name | Notes |
| --- | --- | --- |
| Session start | `SessionStart` | Fires on a fresh session or a resumed one |
| Every turn | `UserPromptSubmit` | Fires on every message, before it reaches the model |
| Before a tool runs | `PreToolUse` | Intercepts `Bash`, file edits made through `apply_patch`, and MCP tool calls |

Same common fields as Claude Code (`session_id`, `cwd`, `hook_event_name`, `tool_name`,
`tool_input`, plus a Codex-specific `turn_id`), but two concrete differences that change how a
detection script must be written:

- **No separate file-path field.** Both `Bash` and `apply_patch` report their target inside
  `tool_input.command` as a string. A script that needs the file being touched must parse that
  string; it cannot read a clean `file_path` the way it can on Claude Code's `Read`/`Edit`. A
  `matcher` may still target `apply_patch` (aliased as `Edit`/`Write`) specifically, even though
  `tool_name` itself always reports the canonical `apply_patch`.
- **Different non-blocking response field.** A reminder that lets the action proceed is
  `additionalContext`, not `systemMessage`:

  ```json
  {"hookSpecificOutput": {"hookEventName": "PreToolUse",
   "additionalContext": "Reminder text here."}}
  ```

  Blocking uses `permissionDecision: "deny"` plus `permissionDecisionReason`, or the same
  exit-code-2-with-stderr fallback as Claude Code.

`agent-base`'s existing pattern for a script that must reach both vendors: write one shared,
vendor-neutral script containing the actual detection logic, printing plain text on success and
nothing on a clean run; then write one thin per-vendor wrapper that calls the shared script and
reformats its output into that vendor's expected JSON envelope. `scripts/check-pending-migrations.sh`
(shared logic, plain stdout, wired directly into Claude Code's `UserPromptSubmit`) and
`scripts/check-pending-migrations-codex.sh` (wraps the same shared script, reformats into Codex's
`additionalContext` shape) are the worked example; copy that split rather than inventing a new one.

### Other vendors

Warp and GitHub Copilot have no confirmed equivalent to any of the three points above as of this
writing. Treat their coverage as an open question per vendor, the same way `AB-RESEARCH-001`
already requires, rather than assuming parity with Claude Code and Codex.

## When each abstract layer is actually used

- **Session start** is where `agent-base-guard.sh` runs. One shot, reaches every vendor with no
  vendor-specific code at all, so it is the first thing to reach for per skill 001's "prefer the
  guard over a vendor hook" -- anything that only needs to be said once, at the top of a session,
  belongs here before anywhere else is considered.
- **Every turn** is where `check-pending-migrations.sh` runs, alongside two more built the same
  way: `check-safe-001-reminder.sh` (fires only when the submitted prompt text plausibly involves
  a credential) and `check-git-commit-format-reminder.sh` (fires only when the repo has
  uncommitted changes). Reach for this when the guard's one-shot reminder is not enough because
  the session runs long and the thing being reminded is a standing obligation ("always do X before
  acting on a credential", "always use the exact commit-block format") rather than a one-time fact
  ("here is what changed since your last sync"). A rule phrased as a positive requirement measurably
  loses compliance as a session gets deeper (`rule-writing.md`'s cited Gamage 2026 finding); a
  fresh, repeated reminder at every turn is not competing with that decay because it never has to
  survive more than one turn's worth of recall.
- **Before a tool runs** is for exactly one situation the other two cannot cover: the reminder or
  block depends on the *specific target* of the next action, not just on the fact that some turn is
  happening. Reach for it only when a cheaper layer genuinely cannot do the job -- for example,
  detecting that the very next file read belongs to a different repository than the one the session
  started in, which neither session-start nor every-turn can see. Built as
  `check-credential-repo-boundary.sh` (roadmap item 123): fires when the next tool's target either
  looks credential-shaped by filename or resolves to a different Git repository than the session's
  own `cwd`. Note in passing while building it: `PreToolUse`'s non-blocking reminder needs the
  structured JSON envelope on *both* vendors, unlike every-turn and session-start, where Claude
  Code also accepts plain stdout -- budget for a wrapper script per vendor here even when one
  vendor would otherwise need none.
- **After the agent's turn ends** (a `Stop`-shaped event) exists on both vendors and can even force
  the turn to continue rather than end, but no agent-base mechanism uses it yet. Checked while
  designing the `AB-GIT-002` reminder above and deliberately not used for it: Codex's payload
  carries a `stop_hook_active` guard against an infinite continuation loop, but Claude Code's does
  not, so a blocking implementation here is asymmetric risk between vendors, not just asymmetric
  effort. Reach for this point only for something that must react to the agent's own outgoing
  message specifically, and prefer a purely observational use (log, do not block) until a
  Claude-side loop guard is designed with the same care as the other three points above.

## Cross-references

- Injected hook content: `.agents/hooks.md`
- Where a new capability belongs (abstract vs. concrete, and which file category): skill 001
- Behavior rules and rule IDs: see `AGENTS.md`
