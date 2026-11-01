#!/usr/bin/env python3
"""Append redacted Copilot lifecycle events to the repository JSONL log."""

from __future__ import annotations

import json
import os
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Mapping, TextIO


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_LOG_PATH = REPOSITORY_ROOT / "logs" / "ai-interactions.jsonl"
REDACTED = "[REDACTED]"
OMIT_KEYS = {
    "env",
    "envfile",
    "envfilepath",
    "envvars",
    "envvariables",
    "setenv",
    "setenvvars",
    "environment",
    "environmentvariable",
    "environmentvariables",
    "processenv",
    "processenvironment",
}
SENSITIVE_KEY_PARTS = (
    "password",
    "passwd",
    "secret",
    "token",
    "credential",
    "authorization",
    "apikey",
    "accesskey",
    "privatekey",
    "clientsecret",
    "cookie",
)
TEXT_REDACTIONS = (
    re.compile(r"(?i)\bBearer\s+[A-Za-z0-9._~+/-]+=*"),
    re.compile(r"\b(?:gh[pousr]_[A-Za-z0-9_]{12,}|github_pat_[A-Za-z0-9_]{12,})\b"),
    re.compile(r"\bAKIA[0-9A-Z]{16}\b"),
    re.compile(r"\bsk-[A-Za-z0-9_-]{20,}\b"),
    re.compile(r"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\b"),
    re.compile(
        r"(?i)\b(password|passwd|secret|token|api[_-]?key|access[_-]?(?:key|token)|"
        r"refresh[_-]?token|id[_-]?token|auth[_-]?token|client[_-]?secret|"
        r"authorization)\b(\s*[:=]\s*|\s+)"
        r'("[^"]*"|\'[^\']*\'|[^\s,;&|]+)'
    ),
    re.compile(
        r"(?m)(?:\bexport\s+)?[A-Z_][A-Z0-9_]{1,}\s*=\s*"
        r'(?:"[^"]*"|\'[^\']*\'|[^\s;&|]+)'
    ),
    re.compile(r"(?m)^-----BEGIN [^-]+-----.*?^-----END [^-]+-----\s*$", re.DOTALL),
)


def _normalized_key(key: str) -> str:
    return re.sub(r"[^a-z0-9]", "", key.lower())


def _is_sensitive_key(key: str) -> bool:
    normalized_key = _normalized_key(key)
    return any(part in normalized_key for part in SENSITIVE_KEY_PARTS)


def _sanitize_text(value: str) -> str:
    sanitized = value
    for pattern in TEXT_REDACTIONS:
        sanitized = pattern.sub(REDACTED, sanitized)
    return sanitized


def _sanitize(value: Any) -> Any:
    if isinstance(value, str):
        return _sanitize_text(value)
    if isinstance(value, list):
        return [_sanitize(item) for item in value]
    if isinstance(value, dict):
        sanitized: dict[str, Any] = {}
        for key, item in value.items():
            if not isinstance(key, str):
                continue
            normalized_key = _normalized_key(key)
            if (
                normalized_key in OMIT_KEYS
                or normalized_key.startswith("environment")
                or normalized_key.startswith("envvar")
                or normalized_key.startswith("processenv")
            ):
                continue
            sanitized[key] = REDACTED if _is_sensitive_key(key) else _sanitize(item)
        return sanitized
    return value


def _copy_present(
    source: Mapping[str, Any],
    aliases: tuple[str, ...],
    target: dict[str, Any],
    target_key: str,
) -> None:
    for alias in aliases:
        if alias in source:
            target[target_key] = _sanitize(source[alias])
            return


def build_record(payload: Mapping[str, Any]) -> dict[str, Any]:
    """Build a compact record from known hook fields, omitting unavailable data."""
    record: dict[str, Any] = {
        "logged_at": datetime.now(timezone.utc).isoformat(),
    }

    fields = (
        (("hook_event_name", "hookEventName"), "event_type"),
        (("timestamp",), "timestamp"),
        (("session_id", "sessionId"), "session_id"),
        (("prompt",), "prompt"),
        (("initial_prompt", "initialPrompt"), "initial_prompt"),
    )
    for aliases, target_key in fields:
        _copy_present(payload, aliases, record, target_key)

    agent_fields = (
        (("agent_id", "agentId"), "id"),
        (("agent_type", "agentType"), "type"),
        (("agent_name", "agentName"), "name"),
        (("agent_display_name", "agentDisplayName"), "display_name"),
        (("agent_description", "agentDescription"), "description"),
        (("parent_agent_id", "parentAgentId"), "parent_id"),
    )
    agent: dict[str, Any] = {}
    for aliases, target_key in agent_fields:
        _copy_present(payload, aliases, agent, target_key)
    if agent:
        record["agent"] = agent

    model_fields = (
        (("model",), "model"),
        (("model_id", "modelId"), "id"),
        (("model_name", "modelName"), "name"),
        (("model_provider", "modelProvider"), "provider"),
        (("model_version", "modelVersion"), "version"),
    )
    model: dict[str, Any] = {}
    for aliases, target_key in model_fields:
        _copy_present(payload, aliases, model, target_key)
    if model:
        record["model"] = model

    tool: dict[str, Any] = {}
    _copy_present(payload, ("tool_name", "toolName"), tool, "name")
    _copy_present(payload, ("tool_input", "toolArgs"), tool, "input")
    _copy_present(payload, ("tool_result", "toolResult"), tool, "result")
    _copy_present(payload, ("error",), tool, "error")
    if tool:
        record["tool"] = tool

    event_type = record.get("event_type")
    if event_type == "PostToolUse":
        record["tool_outcome"] = "success"
    elif event_type == "PostToolUseFailure":
        record["tool_outcome"] = "failure"

    lifecycle: dict[str, Any] = {}
    for aliases, target_key in (
        (("reason",), "reason"),
        (("source",), "source"),
        (("stop_reason", "stopReason"), "stop_reason"),
    ):
        _copy_present(payload, aliases, lifecycle, target_key)
    if lifecycle:
        record["lifecycle"] = lifecycle

    _copy_present(
        payload,
        ("last_assistant_message", "response"),
        record,
        "agent_response",
    )
    return record


def append_record(record: Mapping[str, Any], log_path: Path) -> None:
    log_path.parent.mkdir(parents=True, exist_ok=True)
    encoded_record = (json.dumps(record, ensure_ascii=False, separators=(",", ":")) + "\n").encode(
        "utf-8"
    )
    descriptor = os.open(log_path, os.O_WRONLY | os.O_CREAT | os.O_APPEND, 0o600)
    try:
        if os.name == "posix":
            os.fchmod(descriptor, 0o600)
        remaining = memoryview(encoded_record)
        while remaining:
            written = os.write(descriptor, remaining)
            if written == 0:
                raise OSError("Unable to append the interaction record")
            remaining = remaining[written:]
    finally:
        os.close(descriptor)


def process_event(stream: TextIO, log_path: Path = DEFAULT_LOG_PATH) -> None:
    payload = json.load(stream)
    if not isinstance(payload, dict):
        raise ValueError("Copilot hook input must be a JSON object")
    append_record(build_record(payload), log_path)


def main() -> int:
    try:
        process_event(sys.stdin)
    except (json.JSONDecodeError, OSError, UnicodeDecodeError, ValueError) as error:
        print(f"AI interaction logger: {error}", file=sys.stderr)
        return 1

    sys.stdout.write("{}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
