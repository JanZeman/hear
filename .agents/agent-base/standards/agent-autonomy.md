# Agent Autonomy

Canonical operator guide for reducing Codex, Claude Code, GitHub Copilot, and Warp permission
prompts without granting unrestricted host access.

**"Agent Autonomy" is vocabulary this project coined, not a vendor feature.** Searching vendor
documentation for the phrase finds nothing: each vendor names only its own half of it. Codex
combines a `workspace-write` sandbox with Auto-review, Claude calls it Auto mode, Copilot calls it
assisted permissions over local sandboxing, and Warp exposes Agent Decides. The name deliberately
omits "sandboxed": a sandbox is how three of the four bound the autonomy, but Warp has none that
agent-base can configure, so the invariant that actually holds everywhere is narrower -- a boundary
exists, and it is never disabled globally.

## Architecture

Agent-base separates three configuration scopes:

1. Root `vendor.sh` audits and requests explicit consent before
   `scripts/agent-autonomy.py` installs machine-wide defaults in
   `~/.codex/config.toml`, `~/.claude/settings.json`, and `~/.copilot/settings.json`. Warp has no
   documented local settings API, so the script presents and records a versioned manual review.
2. `.claude/settings.agent-base.*.json` supplies narrow repository and universal Claude policy.
   Sync merges human-only Git-history denials into user settings, removes the obsolete `git add`
   denial so machine policy does not contradict `AB-GIT-002`'s complete command handoff, and
   removes old agent-base command allowlists while preserving unrelated rules.
3. IDE state stays in the IDE. The checker diagnoses known VS Code conflicts and missing sandbox
   settings, but never rewrites editor state. Warp UI state is likewise manual.

The machine-wide installer is idempotent, preserves unrelated values, records only its owned
changes in private state, and supports `install`, `update`, `status`, `check`, `restore`, and
`uninstall`. `vendor.sh` is the operator entry point: it audits first, shows the proposed values,
and accepts only an explicit `yes` before a write. Each existing settings file that changes is
copied byte-for-byte into a private timestamped directory under the Agent Base operator config;
the printed `vendor.sh --<vendor> --restore <backup-dir>` command validates vendor and target,
backs up the current file, and then restores the selected snapshot atomically.
Every downstream session guard runs only `check`. If action is needed, its output tells the agent
which installed vendors and machine values already comply, reads the repository and VS Code user
settings for conflicts, and distinguishes writable machine fixes from manual IDE/UI work. The
agent directs the human to the consent-gated command, which reruns the check afterward and turns
only the remaining findings into exact user instructions. Manual-only findings cause no approval
question. The guard continues even when the repository is not yet configured.

## Install or update

From agent-base itself:

```bash
bash vendor.sh --all
```

From a synchronized downstream repository:

```bash
bash vendor.sh --all
```

Select one vendor with `--codex`, `--claude`, `--copilot`, or `--warp`; `--all` checks every
vendor and skips unavailable ones. Re-run the same command after Agent Base ships new defaults.
This remains an explicit operator action because the target files live outside the repository.

## Codex

The installer manages these top-level user settings:

```toml
approval_policy = "on-request"
approvals_reviewer = "auto_review"
sandbox_mode = "workspace-write"
```

Normal reads, repository writes, tests, builds, and developer commands run inside the workspace
sandbox. Eligible attempts to cross its boundary go to a separate Auto-review model instead of
immediately interrupting the human. The reviewer does not weaken or bypass the sandbox.

Codex protects `.git`, `.agents`, and `.codex` recursively even when they are under a writable
root. Agent-base deliberately keeps that protection: maintenance of `.agents` requests an
escalation, which Auto-review normally decides. Git history remains additionally protected by the
agent-base human-only Git contract.

Do not replace this with `approval_policy = "never"`. Under `workspace-write`, a never policy
cannot approve required `.agents` maintenance; it converts prompts into failed work. Do not use
full access as the default.

Diagnostics:

```text
/status
/debug-config
```

`/status` should show `on-request`, `workspace-write`, and the writable roots. When Auto-review
denies an eligible request that you intend to allow, `/approve` retries that one request with a
human decision. Starting `codex --approve-for-me` is a useful one-session comparison.

Official references:

- [Codex Auto-review](https://learn.chatgpt.com/docs/sandboxing/auto-review)
- [Codex approvals and security](https://learn.chatgpt.com/docs/agent-approvals-security)
- [Codex configuration reference](https://learn.chatgpt.com/docs/config-file/config-reference)

## Claude Code

The installer manages the following user settings while preserving `autoMode.environment` and
other unrelated configuration:

```json
{
  "permissions": {
    "defaultMode": "auto"
  },
  "sandbox": {
    "enabled": true,
    "autoAllowBashIfSandboxed": true,
    "failIfUnavailable": true
  }
}
```

It also denies sandboxed commands direct reads of `~/.ssh` and the AWS credentials file, and
removes common cloud/package tokens from the sandbox environment. These restrictions do not
expose or copy credential values. Add explicit filesystem or network paths only when a real tool
needs them; prefer a narrow sandbox boundary over a `Bash(...)` command allowlist.

`permissions.defaultMode = "auto"` must be a user-level default. Claude does not activate Auto
from `.claude/settings.json` or `.claude/settings.local.json`; a project `defaultMode` can instead
override or suppress the user default. Agent-base sync removes only its exact legacy
`acceptEdits` seed and preserves a project value chosen by its owner.

Diagnostics:

```text
/status
/sandbox
```

`/sandbox` should report enabled filesystem and network isolation plus auto-allow for sandboxed
Bash. `claude auto-mode config` displays the Auto-mode environment supplied to the classifier.
Run `python3 scripts/agent-autonomy.py status` to find user, project, or IDE conflicts.

The VS Code setting `claudeCode.initialPermissionMode` does not support `auto` and takes
precedence when present. Remove that setting, open a new Claude Code conversation, and choose
**Auto** once in the extension's mode indicator. The extension remembers that selection; the
installer reports the conflict but does not edit editor state.

Official references:

- [Claude Code permission modes](https://code.claude.com/docs/en/permission-modes)
- [Claude Code sandboxing](https://code.claude.com/docs/en/sandboxing)
- [Claude Code settings](https://code.claude.com/docs/en/configuration)
- [Claude Code Auto mode](https://code.claude.com/docs/en/auto-mode-config)
- [Claude Code for VS Code](https://code.claude.com/docs/en/vs-code)

## GitHub Copilot

The installer manages these Copilot CLI user settings:

```json
{
  "sandbox": {
    "enabled": true,
    "auth": { "git": false, "gh": false }
  },
  "permissions": {
    "disableBypassPermissionsMode": "allow-auto-only"
  }
}
```

The sandbox handles ordinary work. Authenticated Git and GitHub credentials remain outside it,
and full `allow-all` bypass is disabled while Copilot's reviewer-assisted approval remains
available. In each Copilot CLI session choose `/permissions assisted`; verify with `/sandbox
status` and `/sandbox policy`. Copilot does not currently document a persistent default for that
session mode.

Copilot settings support JSONC. Agent-base safely creates or updates strict JSON, but refuses to
rewrite an existing commented/trailing-comma file because doing so would erase its comments. In
that case, apply the four documented values manually and rerun `status`.

For VS Code, the checker recommends:

```json
{
  "chat.assistedPermissions.enabled": true,
  "chat.agent.sandbox.enabled": "on"
}
```

Then choose **Assisted permissions** for the Agent Host session. It cannot be expressed through
`chat.permissions.default`, whose automatic modes are broader. Never enable
`chat.tools.global.autoApprove` as a substitute: it bypasses the reviewer boundary.

Official references:

- [VS Code agent approvals](https://code.visualstudio.com/docs/agents/run/approvals)
- [VS Code AI settings](https://code.visualstudio.com/docs/agents/reference/ai-settings)
- [Copilot CLI local sandboxing](https://docs.github.com/en/copilot/how-tos/cloud-and-local-sandboxes/using-local-sandboxing)
- [Copilot CLI configuration](https://docs.github.com/en/enterprise-cloud@latest/copilot/reference/copilot-cli-reference/cli-config-dir-reference)

## Warp

Warp exposes autonomy controls in its UI but no documented stable local configuration API that
agent-base can safely modify or audit. The installer therefore presents a versioned checklist and
records only that it was presented. Set **Read files**, **Execute commands**, and **Apply code
diffs** to **Agent Decides**, allow the current repository directory, and retain narrow denies for
destructive or credential-bearing commands. Avoid **Always Allow** as a global default.

Official references:

- [Warp admin settings and autonomy](https://docs.warp.dev/knowledge-and-collaboration/admin-panel)
- [Warp CLI](https://docs.warp.dev/reference/cli)

## Remaining safety boundary

Human intervention can still be legitimate for destructive or credential-bearing work, protected
configuration, unusual unsandboxed commands, system-wide writes, and reviewer/classifier denials.
Claude tools that inherently request user interaction and Codex computer-use/app prompts are not
silently answered by these settings. Network access remains constrained by each vendor's sandbox
and review flow.

Warnings from `status` identify project or IDE overrides but do not make an otherwise successful
machine installation fail. `check` is quieter: it prints nothing when no action is required and
always returns success so clean client work can continue. New sessions are required to pick up
changed user configuration.

To remove only the machine-wide defaults owned by this installer:

```bash
python3 scripts/agent-autonomy.py uninstall --vendor all --consent-recorded
```

Run this low-level removal only after the human explicitly approves it. It creates backups before
changing file-backed vendor settings, just like installation and restore.
