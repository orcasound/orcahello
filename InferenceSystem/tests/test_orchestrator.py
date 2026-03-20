"""
Integration tests for LiveInferenceOrchestrator running the
orchestrator as a subprocess against orchestrator config files.

Usage:
    pytest tests/test_orchestrator.py -v
    pytest tests/test_orchestrator.py -v -k fail           # fail/edge-case tests
    pytest tests/test_orchestrator.py -v -k negative       # known negative detections
    pytest tests/test_orchestrator.py -v -k positive       # known positive detections
    pytest tests/test_orchestrator.py -v -k livehls        # smoke test
"""

import subprocess
import sys
from pathlib import Path

import pytest

INFERENCE_DIR = Path(__file__).parent.parent
ORCHESTRATOR = INFERENCE_DIR / "src" / "LiveInferenceOrchestrator.py"
ORCH_CONFIGS_DIR = INFERENCE_DIR / "tests" / "orch_configs"


def run_orchestrator(
    config_path: Path,
    max_segments: int | None = None,
    max_live_iterations: int | None = None,
) -> tuple[str, int]:
    """Run the orchestrator and return (combined stdout+stderr output, return code)."""
    cmd = [
        sys.executable,
        str(ORCHESTRATOR),
        "--orch_config",
        str(config_path),
    ]
    if max_segments is not None:
        cmd += ["--max_segments", str(max_segments)]
    if max_live_iterations is not None:
        cmd += ["--max_live_iterations", str(max_live_iterations)]
    result = subprocess.run(
        cmd,
        capture_output=True,
        text=True,
        cwd=str(INFERENCE_DIR),
    )
    output = result.stdout + result.stderr
    return output, result.returncode


# -----------------------------------------------------------------------------
# Fail / edge-case tests — expect clean exit (no crash)
# -----------------------------------------------------------------------------


@pytest.mark.parametrize(
    "config_file",
    [
        "DateRangeHLS_NoAudio.yml",
        "DateRangeHLS_NoAudio2.yml",
    ],
)
def test_fail_no_audio(config_file):
    """No-audio configs: orchestrator must exit cleanly (exit 0) without crashing."""
    config = ORCH_CONFIGS_DIR / "Fail" / config_file
    output, returncode = run_orchestrator(config, max_segments=1)
    print(output)
    assert returncode == 0, f"Orchestrator crashed (exit {returncode}) on {config_file}"


def test_fail_incomplete_minute():
    """Incomplete-minute config: orchestrator must exit cleanly without crashing."""
    config = ORCH_CONFIGS_DIR / "Fail" / "DateRangeHLS_IncompleteMinute.yml"
    output, returncode = run_orchestrator(config, max_segments=1)
    print(output)
    assert returncode == 0, f"Orchestrator crashed (exit {returncode})"


# -----------------------------------------------------------------------------
# Negative test — expect global_prediction=0
# -----------------------------------------------------------------------------


def test_negative_detection_point_robinson():
    """Known negative clip: orchestrator must report global_prediction=0."""
    config = ORCH_CONFIGS_DIR / "Negative" / "DateRangeHLS_PointRobinson.yml"
    output, returncode = run_orchestrator(config, max_segments=1)
    print(output)
    assert returncode == 0, f"Orchestrator exited with code {returncode}"
    assert "global_prediction: 0" in output, (
        "Expected global_prediction: 0 not found in output"
    )


# -----------------------------------------------------------------------------
# Positive tests — expect global_prediction=1
# -----------------------------------------------------------------------------

POSITIVE_CONFIGS = [
    "DateRangeHLS_OrcasoundLab.yml",
    "DateRangeHLS_AndrewsBay.yml",
    "DateRangeHLS_BushPoint.yml",
    "DateRangeHLS_NorthSJC.yml",
    "DateRangeHLS_PortTownsend.yml",
    "DateRangeHLS_SunsetBay.yml",
    "DateRangeHLS_MastCenter.yml",
]


@pytest.mark.parametrize("config_file", POSITIVE_CONFIGS)
def test_positive_detection(config_file):
    """Known positive clips: orchestrator must report global_prediction=1."""
    config = ORCH_CONFIGS_DIR / "Positive" / config_file
    output, returncode = run_orchestrator(config, max_segments=1)
    print(output)
    assert returncode == 0, f"Orchestrator exited with code {returncode}"
    assert "global_prediction: 1" in output, (
        f"Expected global_prediction: 1 not found in output for {config_file}"
    )


# -----------------------------------------------------------------------------
# LiveHLS smoke test
# -----------------------------------------------------------------------------


def test_livehls_smoke():
    """Smoke test: orchestrator runs 2 iterations against live stream without crashing."""
    config = ORCH_CONFIGS_DIR / "LiveHLS" / "LiveHLS_OrcasoundLab.yml"
    output, returncode = run_orchestrator(config, max_live_iterations=2)
    print(output)
    assert returncode == 0, f"Orchestrator exited with code {returncode}"
    assert "[iter 1]" in output, "Expected at least two iterations"
    expected_strings = [
        "WARNING No HLS folders",
        "WARNING Failed to load M3U8",
        "global_prediction",
    ]
    assert any(s in output for s in expected_strings), (
        f"Expected at least one of the expected strings in output: {expected_strings}"
    )
