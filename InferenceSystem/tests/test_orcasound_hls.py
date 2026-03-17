"""
Tests for the orcasound_hls module (replacement for orca-hls-utils).

Validates S3 folder lookup, timestamp computation, and audio clip retrieval
against real Orcasound S3 data.

Usage:
    pytest tests/test_orcasound_hls.py -v
"""

import math
import os
import shutil
import wave
from datetime import datetime, timezone
from pathlib import Path

import pytest
from pytz import timezone as pytz_tz

# Ensure src/ is on the path (same as orchestrator tests)
import sys
sys.path.insert(0, str(Path(__file__).parent.parent / "src"))

from orcasound_hls import DateRangeHLSStream
from orcasound_hls.hls_locator import (
    find_folder_for_timestamp,
    list_folders_in_range,
    m3u8_exists,
    m3u8_url,
)


# --- Helpers ---

def pst_to_unix(pst_str: str) -> int:
    dt = datetime.strptime(pst_str, "%Y-%m-%d %H:%M")
    return int(pytz_tz("US/Pacific").localize(dt).timestamp())


WAV_DIR = "test_wav_dir_hls"
ORCASOUND_LAB_BUCKET = "audio-orcasound-net"
ORCASOUND_LAB_HYDRO = "rpi_orcasound_lab"

# Known date range with audio (from positive test config)
KNOWN_START_PST = "2020-09-01 15:13"
KNOWN_END_PST = "2020-09-01 16:45"
KNOWN_FOLDER_EPOCH = 1598988619  # folder containing audio for this range


# --- HLS Locator Tests ---

class TestHLSLocator:
    def test_find_folder_for_known_timestamp(self):
        """Prefix-filtered lookup finds the correct folder for a known timestamp."""
        start_unix = pst_to_unix(KNOWN_START_PST)
        folder, offset = find_folder_for_timestamp(
            ORCASOUND_LAB_BUCKET, ORCASOUND_LAB_HYDRO, start_unix
        )
        assert folder is not None
        assert folder == KNOWN_FOLDER_EPOCH
        assert offset == start_unix - KNOWN_FOLDER_EPOCH
        assert offset > 0

    def test_list_folders_in_range(self):
        """Prefix-filtered range listing returns folders covering the date range."""
        start_unix = pst_to_unix(KNOWN_START_PST)
        end_unix = pst_to_unix(KNOWN_END_PST)
        folders = list_folders_in_range(
            ORCASOUND_LAB_BUCKET, ORCASOUND_LAB_HYDRO, start_unix, end_unix
        )
        assert len(folders) > 0
        assert KNOWN_FOLDER_EPOCH in folders
        # All folders should be sorted
        assert folders == sorted(folders)

    def test_m3u8_exists_for_known_folder(self):
        assert m3u8_exists(ORCASOUND_LAB_BUCKET, ORCASOUND_LAB_HYDRO, KNOWN_FOLDER_EPOCH)

    def test_m3u8_not_exists_for_fake_folder(self):
        assert not m3u8_exists(ORCASOUND_LAB_BUCKET, ORCASOUND_LAB_HYDRO, 9999999999)

    def test_find_folder_for_future_timestamp(self):
        """A timestamp far in the future should return None."""
        folder, offset = find_folder_for_timestamp(
            ORCASOUND_LAB_BUCKET, ORCASOUND_LAB_HYDRO, 9999999999
        )
        assert folder is None
        assert offset is None


# --- DateRangeHLSStream Tests ---

class TestDateRangeHLSStream:
    @pytest.fixture(autouse=True)
    def setup_wav_dir(self):
        os.makedirs(WAV_DIR, exist_ok=True)
        yield
        shutil.rmtree(WAV_DIR, ignore_errors=True)

    def _make_stream(self, start_pst=KNOWN_START_PST, end_pst=KNOWN_END_PST):
        start_unix = pst_to_unix(start_pst)
        end_unix = pst_to_unix(end_pst)
        return DateRangeHLSStream(
            f"https://s3-us-west-2.amazonaws.com/{ORCASOUND_LAB_BUCKET}/{ORCASOUND_LAB_HYDRO}",
            60, start_unix, end_unix, WAV_DIR, False,
        )

    def test_init_finds_folders(self):
        stream = self._make_stream()
        assert len(stream.valid_folders) > 0
        assert KNOWN_FOLDER_EPOCH in stream.valid_folders

    def test_init_raises_for_no_audio_range(self):
        """A date range with no HLS folders should raise IndexError."""
        with pytest.raises(IndexError):
            self._make_stream("2000-01-01 00:00", "2000-01-01 01:00")

    def test_get_first_clip(self):
        """First successful clip has deterministic timestamp and ~60s duration."""
        stream = self._make_stream()
        # Skip folders until we get a clip
        wav_path = None
        start_ts = None
        for _ in range(10):
            wav_path, start_ts, _ = stream.get_next_clip()
            if wav_path:
                break

        assert wav_path is not None, "Failed to get any clip in 10 iterations"
        assert os.path.exists(wav_path)
        assert start_ts is not None

        # Timestamp should be ISO-8601 UTC
        assert start_ts.endswith("Z")
        parsed = datetime.strptime(start_ts, "%Y-%m-%dT%H:%M:%SZ")

        # Should be close to our requested start time (within audio_offset tolerance)
        start_unix = pst_to_unix(KNOWN_START_PST)
        ts_unix = int(parsed.replace(tzinfo=timezone.utc).timestamp())
        assert abs(ts_unix - start_unix) < 15, (
            f"Timestamp {start_ts} is {ts_unix - start_unix}s from requested start {start_unix}"
        )

        # WAV duration should be ~60s
        with wave.open(wav_path) as w:
            duration = w.getnframes() / w.getframerate()
        assert 55 < duration < 65, f"Clip duration {duration:.1f}s, expected ~60s"

    def test_consecutive_clips_advance(self):
        """Consecutive clips have monotonically increasing timestamps."""
        stream = self._make_stream()
        timestamps = []
        for _ in range(15):  # enough to skip empty folders + get 3 clips
            if stream.is_stream_over():
                break
            wav_path, start_ts, _ = stream.get_next_clip()
            if wav_path and start_ts:
                timestamps.append(start_ts)
                os.remove(wav_path)
            if len(timestamps) >= 3:
                break

        assert len(timestamps) >= 2, f"Got only {len(timestamps)} clips"
        # Timestamps should be strictly increasing
        for i in range(1, len(timestamps)):
            assert timestamps[i] > timestamps[i - 1], (
                f"Timestamps not increasing: {timestamps[i-1]} >= {timestamps[i]}"
            )

    def test_timestamp_is_deterministic(self):
        """Running the same date range twice produces the same timestamp."""
        ts_runs = []
        for _ in range(2):
            stream = self._make_stream()
            for __ in range(10):
                wav_path, start_ts, _ = stream.get_next_clip()
                if wav_path:
                    os.remove(wav_path)
                    ts_runs.append(start_ts)
                    break

        assert len(ts_runs) == 2
        assert ts_runs[0] == ts_runs[1], (
            f"Non-deterministic timestamps: {ts_runs[0]} vs {ts_runs[1]}"
        )


# --- Bush Point (different hydrophone) ---

class TestBushPoint:
    @pytest.fixture(autouse=True)
    def setup_wav_dir(self):
        os.makedirs(WAV_DIR, exist_ok=True)
        yield
        shutil.rmtree(WAV_DIR, ignore_errors=True)

    def test_bush_point_clip(self):
        """DateRangeHLSStream works for Bush Point hydrophone."""
        start_unix = pst_to_unix("2024-11-02 09:52")
        end_unix = pst_to_unix("2024-11-02 09:53")
        stream = DateRangeHLSStream(
            "https://s3-us-west-2.amazonaws.com/audio-orcasound-net/rpi_bush_point",
            60, start_unix, end_unix, WAV_DIR, False,
        )

        wav_path = None
        for _ in range(10):
            wav_path, start_ts, _ = stream.get_next_clip()
            if wav_path:
                break

        assert wav_path is not None, "Failed to get Bush Point clip"
        assert start_ts.endswith("Z")
        os.remove(wav_path)
