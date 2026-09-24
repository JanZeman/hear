# Formatting

Canonical reference for code and documentation formatting rules in this project.

**Audience**: human contributors and coding agents.

**Principle**: format before you finish, not after. Do not leave formatting to git hooks or CI.
Run the relevant commands during the task, before considering it done.

## What to run

- Markdown: `bash scripts/markdown-lint.sh --fix --changed`

<!-- TODO: Add additional formatting commands for your project. Examples:

- Source code: `bash scripts/format_source.sh`
- Everything: `bash scripts/format-changed-files.sh`

Or use your framework's built-in formatter, a Makefile target, etc.
-->

## Source code formatting

<!-- TODO: Describe your source code formatting setup. Examples:

- Tool and version (e.g. Prettier, Black, dart format, clang-format)
- How to invoke it (script, command, IDE integration)
- Key rules (line length, indentation, import ordering)
-->

## Markdown formatting

- Tool: [markdownlint-cli](https://github.com/igorshubovych/markdownlint-cli), configured in `.markdownlint.jsonc`
- Lint: `bash scripts/markdown-lint.sh --changed`
- Fix: `bash scripts/markdown-lint.sh --fix --changed`
- Lint staged (for hooks): `bash scripts/markdown-lint.sh --staged`

<!-- TODO: Add project-specific Markdown rules or exceptions here. -->

## Agent workflow

<!-- TODO: Describe the expected formatting sequence for agents. Examples:

1. Make edits.
2. Run the project's format command.
3. Verify no remaining lint errors.
4. Only then consider the task complete.

Formatting during the task keeps diffs clean and review fast.
-->

## Typography

### Hyphens only, no em dashes or en dashes

Use the ASCII hyphen-minus (`-`, U+002D) in every agent-authored output: chat replies, email and
other correspondence, internal documents, code, comments, tests, commit messages, and pull-request
text. Never emit U+2014, including while quoting or rewriting source material. Use a Unicode
escape in code that must detect the character. Also avoid introducing U+2013.

### Existing content and ownership

When normally editing agent-authored canonical text, replace U+2014 with ASCII `-` only on the
lines changed by that edit. Preserve borrowed excerpts, quotations, and test fixture data. Do not
start a one-time or broad punctuation cleanup.

Do not directly edit borrowed, vendored, generated, mirrored, or submodule content solely for this
rule. For generated or mirrored content, locate and edit its canonical source, then regenerate or
resynchronize the copy. A mirror may contain project-owned code, but it is still a derived copy;
identify the authoritative source before changing it.

Preserve a test fixture when U+2014 is the input or expected output that the test is designed to
exercise. Ordinary agent-authored test code and prose still follow this typography rule.

## Cross-references

- Behavior rules: see `AGENTS.md`
- Architecture: see `.agents/standards/architecture.md`
