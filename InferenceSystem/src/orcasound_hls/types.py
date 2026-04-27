"""OrcasoundHLSSegment — an immutable description of a contiguous audio clip within an HLS stream.

All fields are derived deterministically from S3 folder epoch + M3U8 playlist
metadata.  No I/O happens at construction time; call ``download_as_wav`` /
``download_as_flac`` to materialise audio on disk.
"""

from __future__ import annotations

import os
import shutil
import urllib.request
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from tempfile import TemporaryDirectory
from typing import List

import ffmpeg
from pytz import timezone as pytz_tz

FOLDER_TO_AUDIO_OFFSET = 2.0

HLS_DOWNLOAD_TIMEOUT_S = 30


@dataclass(frozen=True)
class OrcasoundHLSSegment:
    """Immutable metadata for a contiguous range of HLS .ts segments."""

    # --- location in S3 ---
    bucket: str
    hydrophone_id: str
    folder_epoch: int
    segment_urls: List[str] = field(repr=False)

    # --- position within the M3U8 playlist ---
    start_index: int
    end_index: int  # exclusive

    # --- cumulative durations from M3U8 playlist (relative to folder epoch) ---
    start_cum_dur_s: float
    end_cum_dur_s: float

    # --- audio offset (seconds after folder epoch before audio in M3U8 playlist actually starts) ---
    # approximate calibration constant that may vary between hydrophones or stream conditions
    folder_to_audio_offset_s: float = FOLDER_TO_AUDIO_OFFSET

    # --- convenience ---
    @property
    def duration_s(self) -> float:
        return self.end_cum_dur_s - self.start_cum_dur_s

    @property
    def start_unix(self) -> float:
        return self.folder_epoch + self.start_cum_dur_s + self.folder_to_audio_offset_s

    @property
    def end_unix(self) -> float:
        return self.folder_epoch + self.end_cum_dur_s + self.folder_to_audio_offset_s

    @property
    def start_utc(self) -> datetime:
        return datetime.fromtimestamp(self.start_unix, tz=timezone.utc)

    @property
    def end_utc(self) -> datetime:
        return datetime.fromtimestamp(self.end_unix, tz=timezone.utc)

    @property
    def start_iso(self) -> str:
        """ISO-8601 UTC timestamp, e.g. ``2020-09-01T22:13:02Z``."""
        return self.start_utc.strftime("%Y-%m-%dT%H:%M:%SZ")

    @property
    def name(self) -> str:
        """Human-readable segment name in Pacific time (matches legacy naming)."""
        pst = self.start_utc.astimezone(pytz_tz("US/Pacific"))
        slug = self.hydrophone_id.replace("_", "-")
        return slug + "_" + pst.strftime("%Y_%m_%d_%H_%M_%S_%Z")

    # --- I/O: download and convert ---

    def _download_ts(self, dest_dir: str) -> List[str]:
        """Download .ts segment files into *dest_dir*.  Returns local filenames."""
        filenames: List[str] = []
        for url in self.segment_urls:
            fname = os.path.basename(url)
            dest = os.path.join(dest_dir, fname)
            if not os.path.isfile(dest):
                with urllib.request.urlopen(url, timeout=HLS_DOWNLOAD_TIMEOUT_S) as resp, open(dest, "wb") as out:
                    shutil.copyfileobj(resp, out)
            filenames.append(fname)
        return filenames

    def _concat_and_convert(
        self, ts_dir: str, filenames: List[str], out_path: str
    ) -> str:
        """Concatenate .ts files then convert with ffmpeg to *out_path*."""
        concat_path = os.path.join(ts_dir, self.name + ".ts")
        with open(concat_path, "wb") as out:
            for fname in filenames:
                with open(os.path.join(ts_dir, fname), "rb") as inp:
                    shutil.copyfileobj(inp, out)

        stream = ffmpeg.input(concat_path)
        stream = ffmpeg.output(stream, out_path)
        ffmpeg.run(stream, quiet=True, overwrite_output=True)
        return out_path

    def download_as_wav(self, dest_dir: str) -> str:
        """Download segments, convert to WAV, return the output path."""
        Path(dest_dir).mkdir(parents=True, exist_ok=True)
        wav_path = os.path.join(dest_dir, self.name + ".wav")
        with TemporaryDirectory() as tmp:
            filenames = self._download_ts(tmp)
            self._concat_and_convert(tmp, filenames, wav_path)
        return wav_path

    def download_as_flac(self, dest_dir: str) -> str:
        """Download segments, convert to FLAC, return the output path."""
        Path(dest_dir).mkdir(parents=True, exist_ok=True)
        flac_path = os.path.join(dest_dir, self.name + ".flac")
        with TemporaryDirectory() as tmp:
            filenames = self._download_ts(tmp)
            self._concat_and_convert(tmp, filenames, flac_path)
        return flac_path
