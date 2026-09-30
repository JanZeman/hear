#!/usr/bin/env python3
"""Run a secret-safe stdio MCP probe and summarize managed-host evidence."""

from __future__ import annotations

import argparse
import json
import os
import re
import selectors
import subprocess
import sys
import time
from pathlib import Path


STATUSES = ("present", "missing", "denied", "not-checked")
AUTH = re.compile(r"unauthorized|authentication|forbidden|\b401\b|\b403\b", re.I)
MAX_PROTOCOL_LINE = 1_000_000
MAX_TOOL_ARGUMENTS = 64_000


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host-config", choices=("present", "missing", "not-checked"), default="not-checked")
    parser.add_argument("--source-check", help="operator-local checker: exit 0 present, 1 missing, 77 denied")
    parser.add_argument("--source-status", choices=STATUSES, default=None)
    parser.add_argument("--broadcast-env", help="check this variable's presence in the diagnostic session")
    parser.add_argument("--broadcast-status", choices=STATUSES, default=None,
                        help="status reported by an operator-local broadcast adapter")
    parser.add_argument("--launcher-env", help="check this variable's presence in the fresh launcher's inherited environment")
    parser.add_argument("--launcher-env-status", choices=STATUSES, default="not-checked",
                        help="status established by an operator-local launcher adapter")
    parser.add_argument("--managed-initialize", choices=("succeeded", "failed", "not-checked"), default="not-checked")
    parser.add_argument("--managed-tools-list", choices=("succeeded", "failed", "not-checked"), default="not-checked")
    parser.add_argument("--managed-read-only-call", choices=("succeeded", "failed", "not-checked"), default="not-checked")
    parser.add_argument("--authorize-read-only-call", action="store_true")
    parser.add_argument("--read-only-tool", help="explicitly selected tool name for a read-only call")
    parser.add_argument("--read-only-args-file", help="JSON object file containing arguments for that call")
    parser.add_argument("launcher", nargs=argparse.REMAINDER,
                        help="launcher command after --; its arguments and output are never printed")
    args = parser.parse_args(argv)
    if args.source_check and args.source_status:
        parser.error("use only one of --source-check and --source-status")
    if args.broadcast_env and args.broadcast_status:
        parser.error("use only one of --broadcast-env and --broadcast-status")
    if args.read_only_tool or args.read_only_args_file or args.authorize_read_only_call:
        if not (args.authorize_read_only_call and args.read_only_tool and args.read_only_args_file):
            parser.error("a read-only call requires --authorize-read-only-call, --read-only-tool, and --read-only-args-file")
    if args.launcher and args.launcher[0] == "--":
        args.launcher = args.launcher[1:]
    if args.launcher and args.launcher[0] != "--":
        # argparse REMAINDER normally retains a separator; accept the first token as command too.
        pass
    if args.managed_tools_list == "succeeded" and args.managed_initialize != "succeeded":
        parser.error("managed tools/list success requires managed initialize success")
    if args.managed_read_only_call == "succeeded" and args.managed_tools_list != "succeeded":
        parser.error("managed read-only call success requires managed tools/list success")
    return args


def source_status(args: argparse.Namespace) -> str:
    if args.source_status:
        return args.source_status
    if not args.source_check:
        return "not-checked"
    try:
        result = subprocess.run([args.source_check], stdout=subprocess.DEVNULL,
                                stderr=subprocess.DEVNULL, timeout=15, check=False)
        if result.returncode == 77:
            return "denied"
        if result.returncode == 0:
            return "present"
        if result.returncode == 1:
            return "missing"
        return "not-checked"
    except (OSError, subprocess.TimeoutExpired):
        return "not-checked"


class MCPProbe:
    def __init__(self, command: list[str], timeout: float = 8.0):
        self.command = command
        self.timeout = timeout
        self.proc: subprocess.Popen[bytes] | None = None
        self.selector = selectors.DefaultSelector()
        self.buffer = bytearray()
        self.last_error = ""

    def start(self) -> bool:
        try:
            self.proc = subprocess.Popen(self.command, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                         stderr=subprocess.DEVNULL, env=os.environ.copy(), bufsize=0)
            assert self.proc.stdout is not None
            self.selector.register(self.proc.stdout, selectors.EVENT_READ)
            return True
        except OSError:
            return False

    def send(self, value: dict) -> None:
        assert self.proc and self.proc.stdin
        self.proc.stdin.write((json.dumps(value, separators=(",", ":")) + "\n").encode())
        self.proc.stdin.flush()

    def receive(self, request_id: int) -> dict | None:
        deadline = time.monotonic() + self.timeout
        while time.monotonic() < deadline:
            while b"\n" in self.buffer:
                line, _, remainder = self.buffer.partition(b"\n")
                self.buffer = bytearray(remainder)
                if len(line) > MAX_PROTOCOL_LINE:
                    return None
                try:
                    message = json.loads(line)
                except (json.JSONDecodeError, UnicodeDecodeError):
                    continue
                if isinstance(message, dict) and message.get("id") == request_id:
                    return message
            events = self.selector.select(max(0.0, deadline - time.monotonic()))
            if not events:
                break
            try:
                chunk = os.read(events[0][0].fd, 65536)
            except OSError:
                break
            if not chunk:
                break
            self.buffer.extend(chunk)
            if len(self.buffer) > MAX_PROTOCOL_LINE:
                return None
        return None

    def request(self, method: str, params: dict, request_id: int) -> dict | None:
        self.send({"jsonrpc": "2.0", "id": request_id, "method": method, "params": params})
        return self.receive(request_id)

    def close(self) -> None:
        if self.proc:
            try:
                if self.proc.stdin:
                    self.proc.stdin.close()
                self.proc.terminate()
                self.proc.wait(timeout=1)
            except (OSError, subprocess.TimeoutExpired):
                self.proc.kill()
                self.proc.wait()
        self.selector.close()


def mcp_probe(args: argparse.Namespace) -> tuple[str, str, bool]:
    """Return last successful direct stage, failure kind, and whether an auth service error occurred."""
    if not args.launcher:
        return "not checked", "not-checked", False
    probe = MCPProbe(args.launcher)
    if not probe.start():
        return "not checked", "launcher-failed", False
    last, failure, auth_error = "launcher-execution", "none", False
    try:
        init = probe.request("initialize", {"protocolVersion": "2025-11-25", "capabilities": {},
                                              "clientInfo": {"name": "agent-base-diagnostic", "version": "1"}}, 1)
        if not init or not isinstance(init.get("result"), dict):
            return last, "initialize-failed", False
        last = "initialize"
        probe.send({"jsonrpc": "2.0", "method": "notifications/initialized"})
        listed = probe.request("tools/list", {}, 2)
        if (not listed or not isinstance(listed.get("result"), dict)
                or not isinstance(listed["result"].get("tools"), list)):
            return last, "tools-list-failed", False
        last = "tools/list"
        if args.authorize_read_only_call:
            try:
                if Path(args.read_only_args_file).stat().st_size > MAX_TOOL_ARGUMENTS:
                    return last, "read-only-arguments-invalid", False
                tool_args = json.loads(Path(args.read_only_args_file).read_text(encoding="utf-8"))
                if not isinstance(tool_args, dict):
                    return last, "read-only-arguments-invalid", False
            except (OSError, json.JSONDecodeError):
                return last, "read-only-arguments-invalid", False
            called = probe.request("tools/call", {"name": args.read_only_tool, "arguments": tool_args}, 3)
            if not called or not isinstance(called.get("result"), dict):
                error_text = ""
                if called and isinstance(called.get("error"), dict):
                    error_text = str(called["error"].get("message", ""))
                auth_error = AUTH.search(error_text) is not None
                if auth_error:
                    return last, "service-auth-failed", True
                return last, "read-only-call-failed", False
            result = called["result"]
            if result.get("isError") is True:
                texts = " ".join(item.get("text", "") for item in result.get("content", [])
                                 if isinstance(item, dict) and isinstance(item.get("text"), str))
                auth_error = AUTH.search(texts) is not None
                return last, "service-auth-failed" if auth_error else "read-only-call-failed", auth_error
            last = "read-only-call"
        return last, "none", False
    except Exception:
        failed_stage = {
            "launcher-execution": "initialize-failed",
            "initialize": "tools-list-failed",
            "tools/list": "read-only-call-failed",
        }.get(last, "protocol-failed")
        return last, failed_stage, False
    finally:
        probe.close()


def latest_managed(args: argparse.Namespace) -> str:
    last = "host-configuration" if args.host_config == "present" else "not established"
    if args.managed_initialize == "succeeded":
        last = "initialize"
    if args.managed_tools_list == "succeeded":
        last = "tools/list"
    if args.managed_read_only_call == "succeeded":
        last = "read-only-call"
    return last


def display_status(status: str) -> str:
    return {"denied": "access denied by sandbox", "not-checked": "not checked"}.get(status, status)


def direct_stage_status(stage: str, last: str, failure: str) -> str:
    order = {"launcher-execution": 1, "initialize": 2, "tools/list": 3, "read-only-call": 4}
    if stage == "launcher-execution" and failure == "launcher-failed":
        return "failed"
    if last == "not checked":
        return "not checked"
    if stage == "initialize" and failure == "initialize-failed":
        return "failed"
    if stage == "tools/list" and failure == "tools-list-failed":
        return "failed"
    if stage == "read-only-call" and failure in ("read-only-call-failed", "service-auth-failed"):
        return "failed"
    reached = order.get(last, 0)
    return "succeeded" if order[stage] <= reached else "not checked"


def verdict(args: argparse.Namespace, direct_last: str, failure: str, auth_error: bool,
            src: str) -> tuple[str, str]:
    if args.host_config == "missing":
        return "HOST CONFIGURATION FAILURE", "Correct the target server entry or enable it in the runtime's native configuration, then retest from the managed host."
    if args.managed_initialize == "failed" and direct_last in ("initialize", "tools/list", "read-only-call"):
        return "MANAGED HOST INTEGRATION FAILURE", "Use the runtime's managed MCP view to check host configuration and environment delivery; restore the existing broadcast mechanism if the launcher's environment is missing, fully restart the host, then retest its managed tools."
    if args.managed_initialize == "failed":
        return "MANAGED INITIALIZE FAILURE", "Inspect host configuration and launcher startup first. A connection closed during initialize is a startup/launcher failure until a real service request proves authentication failure."
    if args.managed_initialize == "succeeded" and args.managed_tools_list == "failed":
        return "MANAGED TOOLS/LIST FAILURE", "Inspect runtime tool discovery and local startup diagnostics. A resources/list result does not establish whether tools exist."
    if (args.managed_tools_list == "succeeded" and args.managed_read_only_call == "failed"):
        return "MANAGED READ-ONLY SERVICE CALL FAILURE", "Inspect the selected tool's status locally without copying its content. Check credential presence and delivery; classify authentication only if this actual service request returned an authentication rejection."
    if failure == "launcher-failed":
        return "LAUNCHER EXECUTION FAILURE", "Check the configured executable path, permissions, and launch prerequisites; then repeat the fresh-process MCP probe."
    if failure == "initialize-failed":
        return "STARTUP / LAUNCHER FAILURE DURING INITIALIZE", "Treat connection closed here as a launcher/startup problem, not service authentication failure. Check launcher stderr locally without sharing it, then verify credential delivery by presence only."
    if failure == "tools-list-failed":
        return "TOOLS/LIST FAILURE", "Inspect the MCP server's protocol/version compatibility and startup logs locally; do not infer tool availability from an empty resources/list result."
    if failure == "service-auth-failed" or auth_error:
        return "SERVICE AUTHENTICATION FAILURE", "Check whether the existing credential is present at its source and delivered through the existing broadcast mechanism; verify the request target and permission. Consider rotation only after this actual service request establishes rejection."
    if failure == "read-only-call-failed":
        return "READ-ONLY SERVICE CALL FAILURE", "Inspect the selected read-only tool's arguments and service response locally; the diagnostic intentionally withholds response content."
    if args.launcher_env and args.launcher_env not in os.environ:
        return "LAUNCHER ENVIRONMENT FAILURE", "The fresh launcher's inherited environment lacks the named variable. Restore it through the existing operator-local broadcast mechanism, fully restart the managed host, then retest its managed MCP tool."
    if args.broadcast_env and os.environ.get(args.broadcast_env) is None:
        return "CREDENTIAL DELIVERY FAILURE", "The named broadcast/session environment variable is absent. Restore it with the existing operator-local broadcast mechanism, fully restart the managed host, then retest the managed MCP tool."
    if args.launcher_env_status == "missing":
        return "LAUNCHER ENVIRONMENT FAILURE", "The launcher environment lacks the credential variable. Point the launcher at the existing broadcast mechanism, fully restart the managed host, then retest its managed MCP tool."
    broadcast_status = (args.broadcast_status if args.broadcast_status else
                        "not-checked" if not args.broadcast_env else
                        "present" if args.broadcast_env in os.environ else "missing")
    if broadcast_status == "missing":
        return "BROADCAST ENVIRONMENT FAILURE", "Restore the missing variable through the existing operator-local broadcast mechanism, fully restart the managed host, then retest its managed MCP tool."
    if args.source_status == "denied" or args.broadcast_status == "denied" or args.launcher_env_status == "denied":
        return "CREDENTIAL DELIVERY CHECK DENIED BY SANDBOX", "Keep the affected presence status as access denied by sandbox, not missing. Repeat that presence-only check with an authorized operator-local adapter."
    if failure == "none" and direct_last in ("tools/list", "read-only-call"):
        if args.managed_initialize != "succeeded":
            return "FRESH PROCESS SUCCEEDED; MANAGED HOST UNKNOWN", "This proves the fresh server path only. Check the actual managed connection in the host, restart the full host after any environment repair, and retest managed initialization and tools/list."
        if args.managed_read_only_call != "succeeded":
            return "MANAGED MCP INITIALIZED; SERVICE CALL UNPROVEN", "Retest one explicitly authorized read-only tool through the managed host; do not treat fresh-process success as host repair."
        return "MANAGED MCP SERVICE CALL SUCCEEDED", "No further diagnostic action is indicated."
    if failure == "none" and direct_last == "initialize":
        return "INITIALIZE SUCCEEDED; TOOL DISCOVERY UNKNOWN", "Run tools/list through the fresh process and the managed host; resources/list does not establish whether tools exist."
    if src == "denied":
        return "SECRET SOURCE CHECK DENIED", "Treat the source status as access denied by sandbox, not missing. Repeat the presence-only check with an authorized local path or operator adapter."
    if src == "missing":
        return "CREDENTIAL SOURCE MISSING", "Confirm the operator-local source path and restore the existing credential through its established broadcast mechanism; do not rotate it based on this check alone."
    return "INCOMPLETE MCP DIAGNOSTIC", "Check host configuration, credential source, broadcast/session environment, and launcher environment by presence only; then run the fresh MCP probe and verify managed-host stages."


def main(argv: list[str] | None = None) -> int:
    args = parse_args(sys.argv[1:] if argv is None else argv)
    direct_last, failure, auth_error = mcp_probe(args)
    direct_success = direct_last in ("initialize", "tools/list", "read-only-call")
    broadcast_status = (args.broadcast_status if args.broadcast_status else
                        "not-checked" if not args.broadcast_env else
                        "present" if args.broadcast_env in os.environ else "missing")
    launcher_env = (args.launcher_env_status if not args.launcher_env else
                    "present" if args.launcher_env in os.environ else "missing")
    src = source_status(args)
    result, next_action = verdict(args, direct_last, failure, auth_error, src)
    if args.managed_initialize == "failed" and direct_success:
        result = "DIRECT SERVER SUCCEEDED; MANAGED HOST FAILED"
        next_action = ("Check the managed host's launcher environment for a missing inherited variable and use the existing operator-local broadcast mechanism to populate it; fully quit and relaunch the host, then retest managed initialize, tools/list, and an authorized read-only call.")
    print("MCP connection diagnostic")
    print(f"Host configuration: {display_status(args.host_config)}")
    print(f"Fresh launcher execution: {direct_stage_status('launcher-execution', direct_last, failure)}")
    print(f"Fresh MCP initialize: {direct_stage_status('initialize', direct_last, failure)}")
    print(f"Fresh tools/list: {direct_stage_status('tools/list', direct_last, failure)}")
    print(f"Fresh read-only service call: {direct_stage_status('read-only-call', direct_last, failure)}")
    print(f"Credential source: {display_status(src)}")
    print(f"Broadcast/session environment: {display_status(broadcast_status)}")
    print(f"Launcher environment: {display_status(launcher_env)}")
    print(f"Fresh process last succeeded at: {direct_last}")
    print(f"Managed host last succeeded at: {latest_managed(args)}")
    print(f"Managed initialize: {args.managed_initialize}; tools/list: {args.managed_tools_list}; read-only call: {args.managed_read_only_call}")
    print(f"VERDICT: {result}")
    print(f"NEXT: {next_action}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
