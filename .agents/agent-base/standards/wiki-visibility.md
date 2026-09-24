# Wiki Visibility

Classify the destination before writing to a wiki. Privacy depends on the actual host and path,
not on a Venture name, the connector label, or whether an agent can authenticate. A known private
classification answers the visibility question; it does not approve the external write required
by `AB-DESIGN-001` or permit sending secrets under `AB-SAFE-001`.

## Operator-local policy

The private `${XDG_CONFIG_HOME:-$HOME/.config}/agent-base/wikis.json` registry owns the facts.
Each relevant `wikis` entry names a lowercase `host` in addition to its existing `path`,
`connector`, and `root`. The top-level `visibility_policies` object maps exact hosts to a
`default` of `private` or `public`. Optional `private_path_segments` match complete path segments,
including paths with a locale prefix; a partial word never matches. For example:

```json
{
  "wikis": [
    {"path": "/absolute/client/root", "connector": "wiki-example", "root": "en/ventures/example", "host": "wiki.example.com"}
  ],
  "visibility_policies": {
    "wiki.example.com": {"default": "public", "private_path_segments": ["internal"]}
  }
}
```

Keep the registry mode `0600`. Never put its concrete hosts, paths, or policy values in tracked
agent-base or template files. A missing host, policy, invalid policy, or ambiguous target is
`unknown`, never an assumed private page. Ask only for that unresolved classification, and record
the answer in the operator-local policy so the next session does not ask again.

## Resolve a target

From a downstream Venture, run `python3 scripts/venture.py --root . wiki-visibility` before its
mapped root write. For another page on the same host, add `--path <host-relative-path>`; the
command returns `private`, `public`, or `unknown`. For an arbitrary URL, or in the standalone
agent-base clone without a root Venture helper, match its hostname exactly to the registry policy
and apply the same complete-segment rule to the URL path. Never classify another host by a
connector's name or by a different Venture's mapped root.

On a public page, remove private material or choose a verified private destination before asking
for publication approval. On an unknown page, resolve visibility with the human before writing.
Do not repeat a visibility question when the policy already determines the answer.
