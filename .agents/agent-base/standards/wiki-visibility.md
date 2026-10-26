# Wiki Visibility

Classify the destination before writing to a wiki. Privacy depends on the actual host and path,
not on a Venture name, the connector label, or whether an agent can authenticate. A known private
classification answers the visibility question; it does not approve the external write required
by `AV-DESIGN-001` or permit sending secrets under `AV-SAFE-001`.

## Repository policy

The repository's own `.agents/wiki.json` owns the facts, next to its mapping. Besides `connector`
and `root` it names a lowercase `host` and a `visibility` object with a `default` of `private` or
`public`. Optional `private_path_segments` match complete path segments, including paths with a
locale prefix; a partial word never matches. For example:

```json
{
  "connector": "operator-connector-id",
  "root": "ventures/venture-name",
  "host": "wiki.example.com",
  "visibility": {"default": "public", "private_path_segments": ["internal"]}
}
```

Never put credentials or absolute URLs there. A missing host, policy, invalid policy, or ambiguous
target is `unknown`, never an assumed private page. Ask only for that unresolved classification,
and record the answer in `.agents/wiki.json` so the next session does not ask again.

## Resolve a target

From a downstream Venture, run `python3 scripts/venture.py --root . wiki-visibility` before its
mapped root write. For another page on the same host, add `--path <host-relative-path>`; the
command returns `private`, `public`, or `unknown`. For an arbitrary URL, or in the standalone
ai-venture clone without a root Venture helper, match its hostname exactly to a known repository policy
and apply the same complete-segment rule to the URL path. Never classify another host by a
connector's name or by a different Venture's mapped root.

On a public page, remove private material or choose a verified private destination before asking
for publication approval. On an unknown page, resolve visibility with the human before writing.
Do not repeat a visibility question when the policy already determines the answer.
