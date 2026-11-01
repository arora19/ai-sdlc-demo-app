#!/usr/bin/env python3
"""Simulate Copilot hook events and verify redacted JSONL logging."""

from __future__ import annotations

import json
import sys
import tempfile
from pathlib import Path
from typing import Any

HOOK_DIRECTORY = Path(__file__).resolve().parent
sys.path.insert(0, str(HOOK_DIRECTORY))

from log_ai_interaction import process_event


def _simulate(event: dict[str, Any], log_path: Path) -> None:
    from io import StringIO

    process_event(StringIO(json.dumps(event)), log_path)


def main() -> int:
    with tempfile.TemporaryDirectory() as temporary_directory:
        log_path = Path(temporary_directory) / "logs" / "ai-interactions.jsonl"
        _simulate(
            {
                "hook_event_name": "UserPromptSubmit",
                "session_id": "session-demo",
                "timestamp": "2026-01-02T03:04:05Z",
                "prompt": "Please review this; password=example-password and ghp_TESTTOKEN123456789012",
                "model_name": "example-model",
            },
            log_path,
        )
        _simulate(
            {
                "hook_event_name": "PostToolUse",
                "session_id": "session-demo",
                "timestamp": "2026-01-02T03:04:06Z",
                "tool_name": "terminal",
                "tool_input": {
                    "command": "DEMO_SECRET=do-not-log run-task",
                    "env": {"PRIVATE_VALUE": "do-not-log"},
                    "password": "also-do-not-log",
                },
                "tool_result": {
                    "result_type": "success",
                    "text_result_for_llm": "task completed with token=example-token",
                },
                "agent_id": "agent-demo",
                "agent_type": "task",
                "parent_agent_id": "parent-demo",
            },
            log_path,
        )
        _simulate(
            {
                "hook_event_name": "SubagentStart",
                "session_id": "session-demo",
                "timestamp": "2026-01-02T03:04:07Z",
                "agent_name": "task",
                "agent_display_name": "Task agent",
                "agent_description": "Runs a bounded task",
            },
            log_path,
        )
        _simulate(
            {
                "hook_event_name": "PostToolUseFailure",
                "session_id": "session-demo",
                "tool_name": "terminal",
                "tool_input": {"command": "run-task"},
                "error": "failed with refresh_token=do-not-log",
            },
            log_path,
        )

        lines = log_path.read_text(encoding="utf-8").splitlines()
        if len(lines) != 4:
            raise AssertionError(f"Expected four appended records, found {len(lines)}")
        records = [json.loads(line) for line in lines]

        first_record, second_record, third_record, fourth_record = records
        assert first_record["event_type"] == "UserPromptSubmit"
        assert first_record["timestamp"] == "2026-01-02T03:04:05Z"
        assert first_record["session_id"] == "session-demo"
        assert first_record["model"]["name"] == "example-model"
        assert "[REDACTED]" in first_record["prompt"]

        assert second_record["event_type"] == "PostToolUse"
        assert second_record["tool_outcome"] == "success"
        assert second_record["tool"]["name"] == "terminal"
        assert second_record["agent"]["type"] == "task"
        assert second_record["agent"]["parent_id"] == "parent-demo"
        assert "env" not in second_record["tool"]["input"]

        assert third_record["event_type"] == "SubagentStart"
        assert third_record["agent"]["name"] == "task"
        assert third_record["agent"]["display_name"] == "Task agent"
        assert third_record["agent"]["description"] == "Runs a bounded task"

        assert fourth_record["event_type"] == "PostToolUseFailure"
        assert fourth_record["tool_outcome"] == "failure"
        assert fourth_record["tool"]["error"] == "failed with [REDACTED]"

        serialized_log = "\n".join(lines)
        for secret in (
            "example-password",
            "TESTTOKEN123456789012",
            "do-not-log",
            "also-do-not-log",
            "example-token",
        ):
            assert secret not in serialized_log, f"Secret value was not redacted: {secret}"

    print("AI interaction logger verification passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
