#!/usr/bin/env python3
"""Install and audit sandbox-first autonomy defaults across supported agents.

Repository guards run only its read-only check. A human must approve the machine-wide
installation because it changes user configuration outside the repository.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
from pathlib import Path
import re
import shlex
import shutil
import subprocess
import sys
import tempfile
import tomllib


STATE_VERSION = 1
AUTONOMY_PROFILE_VERSION = "1.0.106"
CODEX_START = "# AGENT-BASE-AUTONOMY:START"
CODEX_END = "# AGENT-BASE-AUTONOMY:END"
CODEX_SETTINGS = {
    "approval_policy": "on-request",
    "approvals_reviewer": "auto_review",
    "sandbox_mode": "workspace-write",
}
CLAUDE_SCALARS = {
    ("permissions", "defaultMode"): "auto",
    ("sandbox", "enabled"): True,
    ("sandbox", "autoAllowBashIfSandboxed"): True,
    ("sandbox", "failIfUnavailable"): True,
}
CLAUDE_CREDENTIAL_FILES = (
    {"path": "~/.aws/credentials", "mode": "deny"},
    {"path": "~/.ssh", "mode": "deny"},
)
CLAUDE_CREDENTIAL_ENV = (
    {"name": "AWS_ACCESS_KEY_ID", "mode": "deny"},
    {"name": "AWS_SECRET_ACCESS_KEY", "mode": "deny"},
    {"name": "AWS_SESSION_TOKEN", "mode": "deny"},
    {"name": "GH_TOKEN", "mode": "deny"},
    {"name": "GITHUB_TOKEN", "mode": "deny"},
    {"name": "NPM_TOKEN", "mode": "deny"},
)
COPILOT_SCALARS = {
    ("sandbox", "enabled"): True,
    ("sandbox", "auth", "git"): False,
    ("sandbox", "auth", "gh"): False,
    ("permissions", "disableBypassPermissionsMode"): "allow-auto-only",
}
VENDORS = ("codex", "claude", "copilot", "warp")


def config_home() -> Path:
    override = os.environ.get("AB_AGENT_AUTONOMY_CONFIG_HOME")
    if override:
        return Path(override)
    return Path(os.environ.get("XDG_CONFIG_HOME", Path.home() / ".config")) / "agent-base" / "agent-autonomy"


def state_path() -> Path:
    return config_home() / "state.json"


def codex_config_path() -> Path:
    override = os.environ.get("AB_AGENT_AUTONOMY_CODEX_CONFIG")
    if override:
        return Path(override)
    return Path(os.environ.get("CODEX_HOME", Path.home() / ".codex")) / "config.toml"


def claude_settings_path() -> Path:
    override = os.environ.get("AB_AGENT_AUTONOMY_CLAUDE_SETTINGS")
    if override:
        return Path(override)
    return Path(os.environ.get("CLAUDE_CONFIG_DIR", Path.home() / ".claude")) / "settings.json"


def copilot_settings_path() -> Path:
    override = os.environ.get("AB_AGENT_AUTONOMY_COPILOT_SETTINGS")
    if override:
        return Path(override)
    return Path(os.environ.get("COPILOT_HOME", Path.home() / ".copilot")) / "settings.json"


def vscode_settings_path() -> Path:
    override = os.environ.get("AB_AGENT_AUTONOMY_VSCODE_SETTINGS")
    if override:
        return Path(override)
    if sys.platform == "darwin":
        return Path.home() / "Library/Application Support/Code/User/settings.json"
    if os.name == "nt":
        return Path(os.environ.get("APPDATA", Path.home())) / "Code/User/settings.json"
    return Path(os.environ.get("XDG_CONFIG_HOME", Path.home() / ".config")) / "Code/User/settings.json"


def vscode_copilot_installed() -> bool:
    override = os.environ.get("AB_AGENT_AUTONOMY_VSCODE_COPILOT")
    if override is not None:
        return override == "1"
    extension_roots = (Path.home() / ".vscode/extensions", Path.home() / ".vscode-insiders/extensions")
    installed_extension = any(
        next(root.glob("github.copilot-chat-*"), None)
        for root in extension_roots
        if root.exists()
    )
    global_storage = vscode_settings_path().parent / "globalStorage/github.copilot-chat"
    return installed_extension or global_storage.exists()


def atomic_text(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    old_mode = path.stat().st_mode & 0o777 if path.exists() else 0o600
    fd, temp_name = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as handle:
            handle.write(content)
        os.chmod(temp_name, old_mode)
        os.replace(temp_name, path)
    finally:
        if os.path.exists(temp_name):
            os.unlink(temp_name)


def atomic_bytes(path: Path, content: bytes, mode: int | None = None) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    target_mode = mode
    if target_mode is None:
        target_mode = path.stat().st_mode & 0o777 if path.exists() else 0o600
    fd, temp_name = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    try:
        with os.fdopen(fd, "wb") as handle:
            handle.write(content)
        os.chmod(temp_name, target_mode)
        os.replace(temp_name, path)
    finally:
        if os.path.exists(temp_name):
            os.unlink(temp_name)


def atomic_json(path: Path, data: dict) -> None:
    atomic_text(path, json.dumps(data, indent=2) + "\n")
    os.chmod(path, 0o600)


def ensure_private_dir(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)
    os.chmod(path, 0o700)


def vendor_settings_path(vendor: str) -> Path:
    if vendor == "codex":
        return codex_config_path()
    if vendor == "claude":
        return claude_settings_path()
    if vendor == "copilot":
        return copilot_settings_path()
    raise ValueError(f"{vendor} has no settings file that agent-base can manage")


def backup_settings_file(vendor: str, path: Path, reason: str) -> Path | None:
    if not path.exists():
        return None
    now = dt.datetime.now(dt.timezone.utc)
    timestamp = now.strftime("%Y%m%dT%H%M%S.%fZ")
    root = config_home() / "backups"
    ensure_private_dir(config_home())
    ensure_private_dir(root)
    directory = root / f"{timestamp}-{vendor}"
    sequence = 1
    while directory.exists():
        directory = root / f"{timestamp}-{vendor}-{sequence}"
        sequence += 1
    ensure_private_dir(directory)
    snapshot = directory / "settings.snapshot"
    mode = path.stat().st_mode & 0o777
    atomic_bytes(snapshot, path.read_bytes(), 0o600)
    metadata = {
        "version": 1,
        "vendor": vendor,
        "target": str(path.resolve()),
        "snapshot": snapshot.name,
        "original_mode": mode,
        "created_at": now.isoformat().replace("+00:00", "Z"),
        "reason": reason,
    }
    atomic_json(directory / "metadata.json", metadata)
    print(f"Backed up {path} to {directory}")
    print(f"Restore with: bash vendor.sh --{vendor} --restore {shlex.quote(str(directory))}")
    return directory


def restore_settings_file(vendor: str, reference: Path) -> None:
    if vendor == "warp":
        raise ValueError("Warp has no local settings file that agent-base can restore")
    root = (config_home() / "backups").resolve()
    directory = reference.expanduser().resolve()
    try:
        directory.relative_to(root)
    except ValueError as exc:
        raise ValueError(f"backup must be inside {root}") from exc
    metadata_path = directory / "metadata.json"
    metadata = load_json(metadata_path)
    if metadata.get("version") != 1 or metadata.get("vendor") != vendor:
        raise ValueError(f"{directory} is not a valid {vendor} backup")
    target = vendor_settings_path(vendor).resolve()
    if metadata.get("target") != str(target):
        raise ValueError(f"backup target does not match current {vendor} settings path {target}")
    snapshot_name = metadata.get("snapshot")
    if not isinstance(snapshot_name, str) or Path(snapshot_name).name != snapshot_name:
        raise ValueError(f"invalid snapshot name in {metadata_path}")
    snapshot = directory / snapshot_name
    if not snapshot.is_file():
        raise ValueError(f"backup snapshot is missing: {snapshot}")
    mode = metadata.get("original_mode")
    if not isinstance(mode, int) or mode < 0 or mode > 0o777:
        raise ValueError(f"invalid original mode in {metadata_path}")
    backup_settings_file(vendor, target, "pre-restore")
    atomic_bytes(target, snapshot.read_bytes(), mode)
    print(f"Restored {target} from {directory}")


def load_json(path: Path) -> dict:
    if not path.exists():
        return {}
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ValueError(f"cannot parse {path}: {exc}") from exc
    if not isinstance(data, dict):
        raise ValueError(f"expected a JSON object in {path}")
    return data


def load_state() -> dict:
    data = load_json(state_path())
    if not data:
        return {"version": STATE_VERSION, "vendors": {}}
    if data.get("version") != STATE_VERSION or not isinstance(data.get("vendors"), dict):
        raise ValueError(f"unsupported state in {state_path()}")
    return data


def get_nested(data: dict, path: tuple[str, ...]) -> tuple[bool, object | None]:
    current: object = data
    for key in path:
        if not isinstance(current, dict) or key not in current:
            return False, None
        current = current[key]
    return True, current


def set_nested(data: dict, path: tuple[str, ...], value: object) -> None:
    current = data
    for key in path[:-1]:
        child = current.get(key)
        if not isinstance(child, dict):
            child = {}
            current[key] = child
        current = child
    current[path[-1]] = value


def delete_nested(data: dict, path: tuple[str, ...]) -> None:
    parents: list[tuple[dict, str]] = []
    current = data
    for key in path[:-1]:
        child = current.get(key)
        if not isinstance(child, dict):
            return
        parents.append((current, key))
        current = child
    current.pop(path[-1], None)
    for parent, key in reversed(parents):
        if isinstance(parent.get(key), dict) and not parent[key]:
            parent.pop(key)


def managed_codex_block() -> str:
    rows = [CODEX_START, "# Sandbox first; automatic review handles eligible boundary crossings."]
    rows.extend(f'{key} = "{value}"' for key, value in CODEX_SETTINGS.items())
    rows.append(CODEX_END)
    return "\n".join(rows)


def remove_codex_block(text: str) -> str:
    pattern = re.compile(
        rf"(?ms)^{re.escape(CODEX_START)}\n.*?^{re.escape(CODEX_END)}\n?"
    )
    return pattern.sub("", text)


def remove_top_level_codex_keys(text: str) -> str:
    result: list[str] = []
    at_top = True
    key_pattern = re.compile(
        r"^\s*(approval_policy|approvals_reviewer|sandbox_mode)\s*="
    )
    for line in text.splitlines(keepends=True):
        if at_top and re.match(r"^\s*\[", line):
            at_top = False
        if at_top and key_pattern.match(line):
            continue
        result.append(line)
    return "".join(result)


def parse_toml(text: str, path: Path) -> dict:
    try:
        return tomllib.loads(text) if text.strip() else {}
    except tomllib.TOMLDecodeError as exc:
        raise ValueError(f"cannot parse {path}: {exc}") from exc


def install_codex(state: dict) -> None:
    path = codex_config_path()
    text = path.read_text(encoding="utf-8") if path.exists() else ""
    parsed = parse_toml(text, path)
    vendor_state = state["vendors"].get("codex")
    if vendor_state is None:
        already_managed = CODEX_START in text
        original = {}
        for key in CODEX_SETTINGS:
            original[key] = {
                "present": False if already_managed else key in parsed,
                "value": None if already_managed else parsed.get(key),
            }
        vendor_state = {"original": original}
        state["vendors"]["codex"] = vendor_state

    body = remove_top_level_codex_keys(remove_codex_block(text)).lstrip("\n")
    updated = managed_codex_block() + "\n"
    if body:
        updated += "\n" + body
    parse_toml(updated, path)
    if updated != text:
        backup_settings_file("codex", path, "install")
        atomic_text(path, updated)
    print(f"Configured Codex Auto-review: {path}")


def uninstall_codex(state: dict) -> None:
    path = codex_config_path()
    if not path.exists():
        state["vendors"].pop("codex", None)
        return
    current_text = path.read_text(encoding="utf-8")
    text = remove_codex_block(current_text).lstrip("\n")
    vendor_state = state["vendors"].get("codex", {})
    originals = vendor_state.get("original", {})
    restored = []
    for key in CODEX_SETTINGS:
        item = originals.get(key, {})
        if item.get("present"):
            value = item.get("value")
            if not isinstance(value, str):
                raise ValueError(f"cannot safely restore non-string Codex setting {key}")
            restored.append(f"{key} = {json.dumps(value)}")
    if restored:
        text = "\n".join(restored) + "\n\n" + text
    parse_toml(text, path)
    if text != current_text:
        backup_settings_file("codex", path, "uninstall")
        atomic_text(path, text)
    state["vendors"].pop("codex", None)
    print(f"Removed Agent-base Codex defaults: {path}")


def policy_path() -> Path | None:
    override = os.environ.get("AB_AGENT_AUTONOMY_CLAUDE_POLICY")
    if override:
        return Path(override)
    script = Path(__file__).resolve()
    candidates = (
        script.parents[1] / ".claude/settings.agent-base.universal.json",
        script.parents[1] / "_sub/agent-base/template/.claude/settings.agent-base.universal.json",
    )
    return next((candidate for candidate in candidates if candidate.exists()), None)


def apply_claude_universal_policy(data: dict) -> int:
    path = policy_path()
    if path is None:
        return 0
    source = load_json(path)
    permissions = data.setdefault("permissions", {})
    if not isinstance(permissions, dict):
        raise ValueError("Claude permissions must be a JSON object")
    removed = 0
    removals = source.get("permission_removals", {})
    for kind in ("allow", "deny"):
        entries = permissions.get(kind, [])
        if not isinstance(entries, list):
            raise ValueError(f"Claude permissions.{kind} must be an array")
        for obsolete in removals.get(kind, []):
            while obsolete in entries:
                entries.remove(obsolete)
                removed += 1
        if entries:
            permissions[kind] = entries
        else:
            permissions.pop(kind, None)
    for kind in ("allow", "deny"):
        entries = permissions.setdefault(kind, [])
        opposite = "deny" if kind == "allow" else "allow"
        opposite_entries = permissions.get(opposite, [])
        for entry in source.get("permissions", {}).get(kind, []):
            while entry in opposite_entries:
                opposite_entries.remove(entry)
            if entry not in entries:
                entries.append(entry)
        if not opposite_entries:
            permissions.pop(opposite, None)
    if not permissions:
        data.pop("permissions", None)
    return removed


def add_owned_list_entries(
    data: dict, path: tuple[str, ...], desired: tuple[dict, ...]
) -> list[dict]:
    present, current = get_nested(data, path)
    if not present:
        current = []
        set_nested(data, path, current)
    if not isinstance(current, list):
        raise ValueError(f"Claude {'.'.join(path)} must be an array")
    added = []
    for entry in desired:
        if entry not in current:
            current.append(dict(entry))
            added.append(dict(entry))
    return added


def install_claude(state: dict) -> None:
    path = claude_settings_path()
    data = load_json(path)
    original_data = json.dumps(data, sort_keys=True)
    vendor_state = state["vendors"].get("claude")
    if vendor_state is None:
        original = {}
        for nested_path in CLAUDE_SCALARS:
            present, value = get_nested(data, nested_path)
            original[".".join(nested_path)] = {"present": present, "value": value}
        vendor_state = {"original": original, "added": {"files": [], "envVars": []}}
        state["vendors"]["claude"] = vendor_state

    for nested_path, value in CLAUDE_SCALARS.items():
        set_nested(data, nested_path, value)
    added_files = add_owned_list_entries(
        data, ("sandbox", "credentials", "files"), CLAUDE_CREDENTIAL_FILES
    )
    added_env = add_owned_list_entries(
        data, ("sandbox", "credentials", "envVars"), CLAUDE_CREDENTIAL_ENV
    )
    recorded = vendor_state.setdefault("added", {"files": [], "envVars": []})
    for entry in added_files:
        if entry not in recorded["files"]:
            recorded["files"].append(entry)
    for entry in added_env:
        if entry not in recorded["envVars"]:
            recorded["envVars"].append(entry)

    removed = apply_claude_universal_policy(data)
    if json.dumps(data, sort_keys=True) != original_data:
        backup_settings_file("claude", path, "install")
        atomic_json(path, data)
    print(f"Configured Claude Auto mode and sandbox: {path}")
    if removed:
        print(f"Removed {removed} obsolete Agent-base command allowlist entries.")


def uninstall_claude(state: dict) -> None:
    path = claude_settings_path()
    if not path.exists():
        state["vendors"].pop("claude", None)
        return
    data = load_json(path)
    original_data = json.dumps(data, sort_keys=True)
    vendor_state = state["vendors"].get("claude", {})
    originals = vendor_state.get("original", {})
    for nested_path, managed_value in CLAUDE_SCALARS.items():
        present, current = get_nested(data, nested_path)
        if present and current != managed_value:
            print(
                f"Preserved user-modified Claude setting {'.'.join(nested_path)}.",
                file=sys.stderr,
            )
            continue
        original = originals.get(".".join(nested_path), {})
        if original.get("present"):
            set_nested(data, nested_path, original.get("value"))
        else:
            delete_nested(data, nested_path)

    added = vendor_state.get("added", {})
    for name in ("files", "envVars"):
        nested_path = ("sandbox", "credentials", name)
        present, current = get_nested(data, nested_path)
        if not present or not isinstance(current, list):
            continue
        for entry in added.get(name, []):
            while entry in current:
                current.remove(entry)
        if not current:
            delete_nested(data, nested_path)
    if json.dumps(data, sort_keys=True) != original_data:
        backup_settings_file("claude", path, "uninstall")
        atomic_json(path, data)
    state["vendors"].pop("claude", None)
    print(f"Removed Agent-base Claude autonomy defaults: {path}")


def install_copilot(state: dict) -> None:
    path = copilot_settings_path()
    try:
        data = load_json(path)
    except ValueError as exc:
        print(
            f"Copilot manual update required: {exc}. Copilot accepts JSONC, but agent-base "
            "will not erase comments while rewriting it; apply the documented values manually.",
            file=sys.stderr,
        )
        return
    original_data = json.dumps(data, sort_keys=True)
    vendor_state = state["vendors"].get("copilot")
    if vendor_state is None:
        original = {}
        for nested_path in COPILOT_SCALARS:
            present, value = get_nested(data, nested_path)
            original[".".join(nested_path)] = {"present": present, "value": value}
        vendor_state = {"original": original}
        state["vendors"]["copilot"] = vendor_state

    for nested_path, value in COPILOT_SCALARS.items():
        set_nested(data, nested_path, value)
    if json.dumps(data, sort_keys=True) != original_data:
        backup_settings_file("copilot", path, "install")
        atomic_json(path, data)
    print(f"Configured GitHub Copilot CLI sandbox defaults: {path}")
    print("Copilot CLI: select `/permissions assisted`; verify with `/sandbox status` and `/sandbox policy`.")


def uninstall_copilot(state: dict) -> None:
    path = copilot_settings_path()
    if not path.exists():
        state["vendors"].pop("copilot", None)
        return
    data = load_json(path)
    original_data = json.dumps(data, sort_keys=True)
    vendor_state = state["vendors"].get("copilot", {})
    originals = vendor_state.get("original", {})
    for nested_path, managed_value in COPILOT_SCALARS.items():
        present, current = get_nested(data, nested_path)
        if present and current != managed_value:
            print(
                f"Preserved user-modified Copilot setting {'.'.join(nested_path)}.",
                file=sys.stderr,
            )
            continue
        original = originals.get(".".join(nested_path), {})
        if original.get("present"):
            set_nested(data, nested_path, original.get("value"))
        else:
            delete_nested(data, nested_path)
    if json.dumps(data, sort_keys=True) != original_data:
        backup_settings_file("copilot", path, "uninstall")
        atomic_json(path, data)
    state["vendors"].pop("copilot", None)
    print(f"Removed Agent-base Copilot CLI defaults: {path}")


def install_warp(state: dict) -> None:
    state["vendors"]["warp"] = {
        "recommendation_presented": AUTONOMY_PROFILE_VERSION,
    }
    print("Warp settings cannot be verified safely through a documented local API.")
    print("Warp UI: set Read files, Execute commands, and Apply code diffs to Agent Decides;")
    print("allow the current repository directory and keep dangerous/destructive commands denied.")
    print("This records that the recommendation was presented, not that Warp UI state was verified.")


def uninstall_warp(state: dict) -> None:
    state["vendors"].pop("warp", None)
    print("Removed the Agent-base Warp recommendation acknowledgement; no Warp setting was changed.")


def feature_check(vendor: str) -> None:
    if os.environ.get("AB_AGENT_AUTONOMY_SKIP_FEATURE_CHECK") == "1":
        return
    if vendor not in {"codex", "claude"}:
        return
    executable = shutil.which(vendor)
    if executable is None:
        raise ValueError(f"{vendor} executable is not installed")
    result = subprocess.run(
        [executable, "--help"], capture_output=True, text=True, check=False
    )
    help_text = result.stdout + result.stderr
    if vendor == "codex" and "--approve-for-me" not in help_text:
        raise ValueError("installed Codex does not support Auto-review (--approve-for-me)")
    if vendor == "claude" and "\"auto\"" not in help_text and " auto" not in help_text:
        raise ValueError("installed Claude Code does not support auto permission mode")


def project_claude_override(repo: Path) -> list[str]:
    warnings = []
    for rel in (Path(".claude/settings.json"), Path(".claude/settings.local.json")):
        path = repo / rel
        if not path.exists():
            continue
        try:
            data = load_json(path)
        except ValueError as exc:
            warnings.append(str(exc))
            continue
        present, value = get_nested(data, ("permissions", "defaultMode"))
        if present:
            warnings.append(
                f"{path}: project defaultMode={value!r} overrides or suppresses the user Auto default"
            )
    return warnings


def project_codex_override(repo: Path) -> list[str]:
    path = repo / ".codex/config.toml"
    if not path.exists():
        return []
    try:
        data = parse_toml(path.read_text(encoding="utf-8"), path)
    except ValueError as exc:
        return [str(exc)]
    conflicts = [
        key for key, value in CODEX_SETTINGS.items() if key in data and data[key] != value
    ]
    return [f"{path}: project overrides {', '.join(conflicts)}"] if conflicts else []


def jsonc_scalar(text: str, key: str) -> object | None:
    match = re.search(
        rf'"{re.escape(key)}"\s*:\s*(true|false|"(?:[^"\\]|\\.)*")',
        text,
    )
    if not match:
        return None
    value = match.group(1)
    if value == "true":
        return True
    if value == "false":
        return False
    try:
        return json.loads(value)
    except json.JSONDecodeError:
        return None


def vscode_warnings() -> list[str]:
    path = vscode_settings_path()
    if not path.exists():
        return []
    text = path.read_text(encoding="utf-8", errors="replace")
    warnings = []
    claude_mode = jsonc_scalar(text, "claudeCode.initialPermissionMode")
    if claude_mode is not None:
        warnings.append(
            f"{path}: remove claudeCode.initialPermissionMode={claude_mode!r}; it cannot "
            "select Auto and overrides the extension's remembered/user-level Auto mode. "
            "Then select Auto once in the Claude Code mode indicator"
        )
    copilot_keys_present = any(
        f'"{key}"' in text
        for key in (
            "chat.assistedPermissions.enabled",
            "chat.agent.sandbox.enabled",
            "chat.tools.global.autoApprove",
        )
    )
    if vscode_copilot_installed() or copilot_keys_present:
        assisted = jsonc_scalar(text, "chat.assistedPermissions.enabled")
        if assisted is not True:
            warnings.append(
                f"{path}: set chat.assistedPermissions.enabled=true so Copilot Agent Host can "
                "offer Assisted permissions"
            )
        sandbox = jsonc_scalar(text, "chat.agent.sandbox.enabled")
        if sandbox != "on":
            warnings.append(
                f"{path}: set chat.agent.sandbox.enabled=\"on\" so sandboxed Copilot commands "
                "can run without repeated approval"
            )
        if jsonc_scalar(text, "chat.tools.global.autoApprove") is True:
            warnings.append(
                f"{path}: disable chat.tools.global.autoApprove; it bypasses the intended "
                "sandbox/reviewer boundary"
            )
    return warnings


def vendor_available(vendor: str) -> bool:
    if os.environ.get("AB_AGENT_AUTONOMY_SKIP_FEATURE_CHECK") == "1":
        return True
    if vendor in {"codex", "claude", "copilot"}:
        return shutil.which(vendor) is not None
    if vendor == "warp":
        return any(
            path.exists()
            for path in (
                Path("/Applications/Warp.app"),
                Path.home() / "Applications/Warp.app",
                Path.home() / "Library/Preferences/dev.warp.Warp-Stable.plist",
            )
        ) or shutil.which("oz") is not None
    return False


def managed_findings(vendors: list[str]) -> list[str]:
    findings = []
    try:
        state = load_state()
    except ValueError as exc:
        findings.append(f"agent-base state: {exc}")
        state = {"version": STATE_VERSION, "vendors": {}}
    for vendor in vendors:
        if not vendor_available(vendor):
            continue
        if vendor == "warp":
            presented = state["vendors"].get("warp", {}).get("recommendation_presented")
            if presented != AUTONOMY_PROFILE_VERSION:
                findings.append(
                    "Warp: review the current Agent Decides sandbox recommendations; "
                    "Warp has no documented local settings API that agent-base can verify"
                )
            continue

        try:
            if vendor == "codex":
                path = codex_config_path()
                data = parse_toml(path.read_text(encoding="utf-8"), path) if path.exists() else {}
                settings = CODEX_SETTINGS
            elif vendor == "claude":
                path = claude_settings_path()
                data = load_json(path)
                settings = CLAUDE_SCALARS
            else:
                path = copilot_settings_path()
                data = load_json(path)
                settings = COPILOT_SCALARS
        except (OSError, ValueError) as exc:
            findings.append(f"{vendor}: {exc}")
            continue

        mismatches = []
        for setting, expected in settings.items():
            if isinstance(setting, tuple):
                present, actual = get_nested(data, setting)
                label = ".".join(setting)
            else:
                present, actual = setting in data, data.get(setting)
                label = setting
            if not present or actual != expected:
                mismatches.append(f"{label}={actual!r} -> {expected!r}")
        if mismatches:
            findings.append(f"{vendor}: {', '.join(mismatches)} in {path}")
    return findings


def advisory_findings(vendors: list[str], repo: Path) -> list[str]:
    findings = []
    if "codex" in vendors and vendor_available("codex"):
        findings.extend(project_codex_override(repo))
    if "claude" in vendors and vendor_available("claude"):
        findings.extend(project_claude_override(repo))
    if "claude" in vendors or "copilot" in vendors:
        findings.extend(vscode_warnings())
    return findings


def vendor_has_finding(vendor: str, findings: list[str]) -> bool:
    return any(
        item.startswith(f"{vendor} ")
        or item.startswith(f"{vendor}:")
        or item.startswith(f"{vendor.capitalize()}:")
        for item in findings
    )


def print_status(vendors: list[str], repo: Path) -> int:
    issues = managed_findings(vendors)
    for vendor in vendors:
        if not vendor_available(vendor):
            print(f"SKIP {vendor}: installation not detected on this machine")
            continue
        vendor_issues = [item for item in issues if vendor_has_finding(vendor, [item])]
        if vendor_issues:
            for item in vendor_issues:
                print(f"MISSING {item}")
        elif vendor == "warp":
            print(f"MANUAL warp recommendation presented for profile {AUTONOMY_PROFILE_VERSION}")
        else:
            print(f"OK {vendor}: all managed machine settings match profile {AUTONOMY_PROFILE_VERSION}")
    for warning in advisory_findings(vendors, repo):
        print(f"WARN {warning}")
    if "codex" in vendors and vendor_available("codex"):
        print("VERIFY Codex: start a new session and run /status; use /approve for a denied review.")
    if "claude" in vendors and vendor_available("claude"):
        print("VERIFY Claude CLI: run /status and /sandbox; run `claude auto-mode config`.")
    if "copilot" in vendors and vendor_available("copilot"):
        print("VERIFY Copilot CLI: select `/permissions assisted`; run `/sandbox status` and `/sandbox policy`.")
    return 1 if issues else 0


def print_check(vendors: list[str], repo: Path) -> int:
    managed = managed_findings(vendors)
    advisory = advisory_findings(vendors, repo)
    if not managed and not advisory:
        return 0
    print("\nAGENT AUTONOMY DIAGNOSTIC ACTION REQUIRED")
    for vendor in vendors:
        if vendor_available(vendor) and not vendor_has_finding(vendor, managed):
            label = "recommendation already presented" if vendor == "warp" else "managed settings compliant"
            print(f"  ALREADY OK: {vendor}: {label}")
    for finding in managed:
        print(f"  MACHINE: {finding}")
    for finding in advisory:
        print(f"  MANUAL: {finding}")
    if managed:
        print("  Run: bash vendor.sh --all")
        print("  The helper repeats the audit, requests explicit consent, creates backups,")
        print("  applies only Agent Base-owned settings, and reports remaining MANUAL steps.")
    else:
        print("  No install approval is needed: give the human these exact MANUAL steps now,")
        print("  with the reason for each. Do not ask whether the agent should perform UI work.")
    print("  Summarize; do not repeat this diagnostic verbatim.")
    if advisory:
        print("  Never rewrite undocumented IDE/Warp state automatically.")
    return 0


def print_post_install_handoff() -> None:
    print("\nAGENT POST-INSTALL HANDOFF REQUIRED")
    print("  Agent: summarize what changed and what is already OK. Convert every MISSING,")
    print("  WARN, MANUAL, VERIFY, or SKIP line above into exact remaining user actions.")
    print("  Do not merely quote raw output, and do not ask another approval for manual-only")
    print("  IDE/UI steps that only the human can perform.")


def selected_vendors(value: str) -> list[str]:
    return list(VENDORS) if value == "all" else [value]


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Install and audit sandbox-first autonomy defaults across supported agents"
    )
    sub = parser.add_subparsers(dest="command", required=True)
    for name in ("install", "update", "status", "check", "uninstall"):
        command = sub.add_parser(name)
        command.add_argument("--vendor", choices=(*VENDORS, "all"), default="all")
        command.add_argument("--repo", type=Path, default=Path.cwd())
        if name in {"install", "update", "uninstall"}:
            command.add_argument(
                "--consent-recorded",
                action="store_true",
                help="confirm that the human explicitly approved the proposed machine writes",
            )
    restore = sub.add_parser("restore")
    restore.add_argument("--vendor", choices=VENDORS, required=True)
    restore.add_argument("--backup", type=Path, required=True)
    restore.add_argument(
        "--consent-recorded",
        action="store_true",
        help="confirm that the human explicitly approved restoring this backup",
    )
    args = parser.parse_args()
    vendors = selected_vendors(args.vendor)
    try:
        if args.command == "status":
            return print_status(vendors, args.repo.resolve())
        if args.command == "check":
            return print_check(vendors, args.repo.resolve())
        if not args.consent_recorded:
            raise ValueError(
                "refusing machine writes without explicit human consent; use bash vendor.sh "
                f"--{args.vendor}"
            )
        if args.command == "restore":
            restore_settings_file(args.vendor, args.backup)
            return 0
        state = load_state()
        if args.command in {"install", "update"}:
            for vendor in vendors:
                if args.vendor == "all" and not vendor_available(vendor):
                    print(f"Skipped {vendor}: installation not detected on this machine")
                    continue
                feature_check(vendor)
                if vendor == "codex":
                    install_codex(state)
                elif vendor == "claude":
                    install_claude(state)
                elif vendor == "copilot":
                    install_copilot(state)
                else:
                    install_warp(state)
        else:
            for vendor in vendors:
                if vendor == "codex":
                    uninstall_codex(state)
                elif vendor == "claude":
                    uninstall_claude(state)
                elif vendor == "copilot":
                    uninstall_copilot(state)
                else:
                    uninstall_warp(state)
        atomic_json(state_path(), state)
        if args.command == "uninstall":
            return 0
        result = print_status(vendors, args.repo.resolve())
        print_post_install_handoff()
        return result
    except (OSError, ValueError, subprocess.SubprocessError) as exc:
        print(f"agent-autonomy: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
