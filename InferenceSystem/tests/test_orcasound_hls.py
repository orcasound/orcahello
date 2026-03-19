"""
Tests for the orcasound_hls module (replacement for orca-hls-utils).

Validates S3 folder lookup, OrcasoundHLSSegment metadata, client API, and audio
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

from orcasound_hls import OrcasoundHLSClient, OrcasoundHLSSegment
from orcasound_hls.types import FOLDER_TO_AUDIO_OFFSET
from orcasound_hls.utils import (
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
KNOWN_FOLDER_DURATION_S = 21590  # has 2159 ts_segments × 10s = ~21590s of audio.
KNOWN_NEXT_FOLDER_EPOCH = 1599010219


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


# --- OrcasoundHLSSegment Dataclass Tests ---


class TestHLSSegment:
    @pytest.fixture(autouse=True)
    def setup_wav_dir(self):
        os.makedirs(WAV_DIR, exist_ok=True)
        yield
        shutil.rmtree(WAV_DIR, ignore_errors=True)

    @pytest.fixture()
    def first_segment(self):
        """Get the first segment from the known date range (cached per test class)."""
        client = OrcasoundHLSClient(BUCKET, HYDRO)
        segments = client.get_segments(
            pst_to_unix(KNOWN_START_PST),
            pst_to_unix(KNOWN_END_PST),
        )
        assert len(segments) > 0, "No segments returned"
        return segments[0]

    def test_segment_metadata(self, first_segment):
        seg = first_segment
        assert seg.bucket == BUCKET
        assert seg.hydrophone_id == HYDRO
        assert seg.folder_epoch == KNOWN_FOLDER_EPOCH
        assert seg.start_index < seg.end_index
        assert seg.duration_s > 0
        assert len(seg.segment_urls) == seg.end_index - seg.start_index

    def test_segment_timestamps_are_deterministic(self, first_segment):
        seg1 = first_segment
        # Fetch again to compare
        client = OrcasoundHLSClient(BUCKET, HYDRO)
        seg2 = client.get_segments(
            pst_to_unix(KNOWN_START_PST), pst_to_unix(KNOWN_END_PST)
        )[0]
        assert seg1.start_iso == seg2.start_iso
        assert seg1.start_unix == seg2.start_unix

    def test_segment_start_near_requested_time(self, first_segment):
        start_unix = pst_to_unix(KNOWN_START_PST)
        assert abs(first_segment.start_unix - start_unix) < 15

    def test_segment_iso_format(self, first_segment):
        assert first_segment.start_iso.endswith("Z")
        datetime.strptime(first_segment.start_iso, "%Y-%m-%dT%H:%M:%SZ")

    def test_segment_utc_properties(self, first_segment):
        assert first_segment.start_utc.tzinfo == timezone.utc
        assert first_segment.end_utc > first_segment.start_utc

    def test_download_as_wav(self, first_segment):
        wav_path = first_segment.download_as_wav(WAV_DIR)
        assert os.path.exists(wav_path)
        assert wav_path.endswith(".wav")
        with wave.open(wav_path) as w:
            duration = w.getnframes() / w.getframerate()
        assert 45 < duration < 65

    def test_name_format(self, first_segment):
        assert first_segment.name.startswith("rpi-orcasound-lab_")
        assert "PDT" in first_segment.name or "PST" in first_segment.name


# --- Client Tests ---


class TestClient:
    @pytest.fixture()
    def client(self):
        return OrcasoundHLSClient(BUCKET, HYDRO)

    def test_returns_multiple_segments(self, client):
        segments = client.get_segments(
            pst_to_unix(KNOWN_START_PST),
            pst_to_unix(KNOWN_END_PST),
            segment_size=60,
        )
        # 15:13 to 16:45 is 92 minutes, so we expect ~92 segments
        assert len(segments) >= 10

    def test_segments_are_monotonically_increasing(self, client):
        segments = client.get_segments(
            pst_to_unix(KNOWN_START_PST),
            pst_to_unix(KNOWN_END_PST),
        )
        for i in range(1, min(len(segments), 5)):
            assert segments[i].start_unix > segments[i - 1].start_unix

    def test_get_segments_empty_range(self, client):
        segments = client.get_segments(
            pst_to_unix("2000-01-01 00:00"),
            pst_to_unix("2000-01-01 01:00"),
        )
        assert segments == []

    def test_all_segments_are_orcasound_hls_segment(self, client):
        segments = client.get_segments(
            pst_to_unix(KNOWN_START_PST),
            pst_to_unix(KNOWN_END_PST),
        )
        assert len(segments) > 0
        for seg in segments[:3]:
            assert isinstance(seg, OrcasoundHLSSegment)

    def test_latest_stream_start(self, client):
        epoch = client.latest_stream_start()
        assert isinstance(epoch, (int, float))
        # Should be a reasonable unix timestamp (after 2020)
        assert epoch > 1577836800

    def test_tail_audio_dropped(self, client):
        """Larger window with segment_size=60 should yield 1 segment; tail is dropped."""
        segment_size = 60
        window_size = 65
        start = KNOWN_FOLDER_EPOCH + 502  # safely inside folder
        end = start + window_size
        segments = client.get_segments(start, end, segment_size=segment_size)
        assert len(segments) == 1, f"Expected 1 segment, got {len(segments)}"
        assert segment_size * 0.5 <= segments[0].duration_s <= segment_size * 1.02

    def test_cross_folder_boundary(self, client):
        """Segments spanning a folder boundary come from both folders."""
        audio_offset = FOLDER_TO_AUDIO_OFFSET
        folder_audio_end = KNOWN_FOLDER_EPOCH + audio_offset + KNOWN_FOLDER_DURATION_S
        # Request 90s before folder 1 ends through 90s into folder 2
        start = folder_audio_end - 90
        end = KNOWN_NEXT_FOLDER_EPOCH + audio_offset + 90
        segments = client.get_segments(start, end, segment_size=60)
        assert len(segments) >= 2, f"Expected >= 2 segments, got {len(segments)}"
        # Should have segments from both folders
        folders_seen = {seg.folder_epoch for seg in segments}
        assert KNOWN_FOLDER_EPOCH in folders_seen, "Missing segment from first folder"
        assert KNOWN_NEXT_FOLDER_EPOCH in folders_seen, (
            "Missing segment from second folder"
        )
        # There's a gap at the boundary (tail of folder 1 + start of folder 2)
        folder1_segs = [s for s in segments if s.folder_epoch == KNOWN_FOLDER_EPOCH]
        folder2_segs = [
            s for s in segments if s.folder_epoch == KNOWN_NEXT_FOLDER_EPOCH
        ]
        gap = folder2_segs[0].start_unix - folder1_segs[-1].end_unix
        assert gap >= 0, "Segments should not overlap across folders"


# --- Cross-hydrophone ---


class TestBushPoint:
    @pytest.fixture(autouse=True)
    def setup_wav_dir(self):
        os.makedirs(WAV_DIR, exist_ok=True)
        yield
        shutil.rmtree(WAV_DIR, ignore_errors=True)

    def test_bush_point_segment(self):
        client = OrcasoundHLSClient(BUCKET, "rpi_bush_point")
        segments = client.get_segments(
            pst_to_unix("2024-11-02 09:52"),
            pst_to_unix("2024-11-02 09:53"),
        )
        assert len(segments) > 0, "No segments returned for Bush Point"
        seg = segments[0]
        assert seg.hydrophone_id == "rpi_bush_point"
        wav_path = seg.download_as_wav(WAV_DIR)
        assert os.path.exists(wav_path)
        os.remove(wav_path)
