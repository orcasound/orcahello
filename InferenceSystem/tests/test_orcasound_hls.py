"""
Tests for the orcasound_hls module (replacement for orca-hls-utils).

Validates S3 folder lookup, HLSSegment metadata, iteration, and audio
download against real Orcasound S3 data.

Usage:
    pytest tests/test_orcasound_hls.py -v
"""

import os
import shutil
import sys
import wave
from datetime import datetime, timezone
from pathlib import Path

import pytest
from pytz import timezone as pytz_tz

sys.path.insert(0, str(Path(__file__).parent.parent / "src"))

from orcasound_hls import HLSSegment, date_range_segments
from orcasound_hls.hls_locator import (
    find_folder_for_timestamp,
    list_folders_in_range,
    m3u8_exists,
)


# --- Helpers ---

def pst_to_unix(pst_str: str) -> int:
    dt = datetime.strptime(pst_str, "%Y-%m-%d %H:%M")
    return int(pytz_tz("US/Pacific").localize(dt).timestamp())


WAV_DIR = "test_wav_dir_hls"
BUCKET = "audio-orcasound-net"
HYDRO = "rpi_orcasound_lab"

# Known date range with audio (from positive test config)
KNOWN_START_PST = "2020-09-01 15:13"
KNOWN_END_PST = "2020-09-01 16:45"
KNOWN_FOLDER_EPOCH = 1598988619


# --- HLS Locator Tests ---

class TestHLSLocator:
    def test_find_folder_for_known_timestamp(self):
        start_unix = pst_to_unix(KNOWN_START_PST)
        folder, offset = find_folder_for_timestamp(BUCKET, HYDRO, start_unix)
        assert folder == KNOWN_FOLDER_EPOCH
        assert offset == start_unix - KNOWN_FOLDER_EPOCH
        assert offset > 0

    def test_list_folders_in_range(self):
        start_unix = pst_to_unix(KNOWN_START_PST)
        end_unix = pst_to_unix(KNOWN_END_PST)
        folders = list_folders_in_range(BUCKET, HYDRO, start_unix, end_unix)
        assert len(folders) > 0
        assert KNOWN_FOLDER_EPOCH in folders
        assert folders == sorted(folders)

    def test_m3u8_exists_for_known_folder(self):
        assert m3u8_exists(BUCKET, HYDRO, KNOWN_FOLDER_EPOCH)

    def test_m3u8_not_exists_for_fake_folder(self):
        assert not m3u8_exists(BUCKET, HYDRO, 9999999999)

    def test_find_folder_for_future_timestamp(self):
        folder, offset = find_folder_for_timestamp(BUCKET, HYDRO, 9999999999)
        assert folder is None
        assert offset is None


# --- HLSSegment Dataclass Tests ---

class TestHLSSegment:
    @pytest.fixture(autouse=True)
    def setup_wav_dir(self):
        os.makedirs(WAV_DIR, exist_ok=True)
        yield
        shutil.rmtree(WAV_DIR, ignore_errors=True)

    def _first_segment(self):
        """Get the first segment from the known date range."""
        for seg in date_range_segments(
            BUCKET, HYDRO,
            pst_to_unix(KNOWN_START_PST),
            pst_to_unix(KNOWN_END_PST),
        ):
            return seg
        pytest.fail("No segments yielded")

    def test_segment_metadata(self):
        seg = self._first_segment()
        assert seg.bucket == BUCKET
        assert seg.hydrophone_id == HYDRO
        assert seg.folder_epoch == KNOWN_FOLDER_EPOCH
        assert seg.start_index < seg.end_index
        assert seg.duration_s > 0
        assert len(seg.segment_urls) == seg.end_index - seg.start_index

    def test_segment_timestamps_are_deterministic(self):
        seg1 = self._first_segment()
        seg2 = self._first_segment()
        assert seg1.start_iso == seg2.start_iso
        assert seg1.start_unix == seg2.start_unix

    def test_segment_start_near_requested_time(self):
        seg = self._first_segment()
        start_unix = pst_to_unix(KNOWN_START_PST)
        assert abs(seg.start_unix - start_unix) < 15

    def test_segment_iso_format(self):
        seg = self._first_segment()
        assert seg.start_iso.endswith("Z")
        # Should parse cleanly
        datetime.strptime(seg.start_iso, "%Y-%m-%dT%H:%M:%SZ")

    def test_segment_utc_properties(self):
        seg = self._first_segment()
        assert seg.start_utc.tzinfo == timezone.utc
        assert seg.end_utc > seg.start_utc

    def test_download_as_wav(self):
        seg = self._first_segment()
        wav_path = seg.download_as_wav(WAV_DIR)
        assert os.path.exists(wav_path)
        assert wav_path.endswith(".wav")
        with wave.open(wav_path) as w:
            duration = w.getnframes() / w.getframerate()
        assert 55 < duration < 65

    def test_clipname_format(self):
        seg = self._first_segment()
        assert seg.clipname.startswith("rpi-orcasound-lab_")
        assert "PDT" in seg.clipname or "PST" in seg.clipname


# --- Iterator Tests ---

class TestDateRangeIterator:
    @pytest.fixture(autouse=True)
    def setup_wav_dir(self):
        os.makedirs(WAV_DIR, exist_ok=True)
        yield
        shutil.rmtree(WAV_DIR, ignore_errors=True)

    def test_yields_multiple_segments(self):
        segments = list(date_range_segments(
            BUCKET, HYDRO,
            pst_to_unix(KNOWN_START_PST),
            pst_to_unix(KNOWN_END_PST),
            clip_duration_s=60,
        ))
        # 15:13 to 16:45 is 92 minutes, so we expect ~92 segments
        assert len(segments) >= 10

    def test_segments_are_monotonically_increasing(self):
        segments = []
        for seg in date_range_segments(
            BUCKET, HYDRO,
            pst_to_unix(KNOWN_START_PST),
            pst_to_unix(KNOWN_END_PST),
        ):
            segments.append(seg)
            if len(segments) >= 5:
                break

        for i in range(1, len(segments)):
            assert segments[i].start_unix > segments[i - 1].start_unix

    def test_empty_range_yields_nothing(self):
        segments = list(date_range_segments(
            BUCKET, HYDRO,
            pst_to_unix("2000-01-01 00:00"),
            pst_to_unix("2000-01-01 01:00"),
        ))
        assert segments == []

    def test_all_segments_are_hlssegment(self):
        for seg in date_range_segments(
            BUCKET, HYDRO,
            pst_to_unix(KNOWN_START_PST),
            pst_to_unix(KNOWN_END_PST),
        ):
            assert isinstance(seg, HLSSegment)
            break


# --- Cross-hydrophone ---

class TestBushPoint:
    @pytest.fixture(autouse=True)
    def setup_wav_dir(self):
        os.makedirs(WAV_DIR, exist_ok=True)
        yield
        shutil.rmtree(WAV_DIR, ignore_errors=True)

    def test_bush_point_segment(self):
        for seg in date_range_segments(
            BUCKET, "rpi_bush_point",
            pst_to_unix("2024-11-02 09:52"),
            pst_to_unix("2024-11-02 09:53"),
        ):
            assert seg.hydrophone_id == "rpi_bush_point"
            wav_path = seg.download_as_wav(WAV_DIR)
            assert os.path.exists(wav_path)
            os.remove(wav_path)
            break
        else:
            pytest.fail("No segments yielded for Bush Point")
