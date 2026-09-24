# Runtime Support

Agent-base separates AI-provider-neutral behavior from the current level of support for each
RUNTIME. The support profile is operator-local because it changes with available subscriptions,
tools, and actual use. It is never committed to a project or copied into a downstream repository.

The legacy filename, command, configuration key, and `vendor` field name remain compatible. Their
values identify RUNTIMES such as Codex or Claude Code, not AI PROVIDERS such as OpenAI or Anthropic.

## Tiers

The profile assigns every supported RUNTIME one tier and one ordered position.

| Tier | Required treatment |
| --- | --- |
| `primary` | A new RUNTIME-specific capability must have a working native path and relevant verification before it is complete. |
| `maintained` | Preserve existing support. New RUNTIME-specific work may follow Primary coverage. |
| `compatibility-only` | Preserve the shared contract and basic startup path. Do not claim a native capability without separately implementing and verifying it. |

The ordered `priority` list resolves work order inside and across tiers. A profile lists all Primary
RUNTIMES first, followed by Maintained and Compatibility-only RUNTIMES. It has at least one Primary
RUNTIME.

## Local profile

The profile lives at `${XDG_CONFIG_HOME:-$HOME/.config}/agent-base/vendor-support.json`, or at
`AB_VENDOR_SUPPORT_CONFIG` when that variable is set. Keep it private and untracked. Its shape is:

```json
{
  "version": 1,
  "priority": ["claude", "codex", "copilot", "warp"],
  "support": {
    "claude": "primary",
    "codex": "primary",
    "copilot": "maintained",
    "warp": "compatibility-only"
  }
}
```

Inspect and validate the current profile with:

```bash
bash vendor.sh --support-status
```

## Applying the profile

Before adding or materially revising RUNTIME-specific behavior, read this standard first.

1. Design and verify the behavior for every Primary RUNTIME in profile order.
2. Preserve Maintained support and document any deliberately deferred native capability.
3. Keep Compatibility-only adapters able to receive shared rules, but do not represent them as
   full support.
4. State the exact RUNTIME and evidence whenever a release claim is narrower than the profile.

An AI-provider-neutral rule remains AI-provider-neutral. This profile only decides the order and
evidence required when implementation relies on a RUNTIME-specific adapter, hook, MODEL launch, or
native agent capability.
