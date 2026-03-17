"""Generator functions that yield ``HLSSegment`` objects.

These are the primary entry-points for consuming Orcasound HLS audio.
No mutable state is kept — each function is a plain generator that can be
composed with standard Python iteration tools.
"""

from __future__ import annotations

import logging
import math
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone
from typing import Iterator, Optional

import m3u8

from . import hls_locator
from .segment import HLSSegment

logger = logging.getLogger(__name__)

# Seconds after folder epoch before first audio is available.
_DEFAULT_AUDIO_OFFSET = 2


def _load_playlist(bucket: str, hydrophone_id: str, folder_epoch: int):
    """Load an M3U8 playlist and return (segments, cumulative_durations)."""
    url = hls_locator.m3u8_url(bucket, hydrophone_id, folder_epoch)
    stream_obj = m3u8.load(url)
    segments = stream_obj.segments
    if not segments:
        return segments, []
    cum = [0.0]
    for seg in segments:
        cum.append(cum[-1] + seg.duration)
    return segments, cum


def _build_segment(
    bucket: str,
    hydrophone_id: str,
    folder_epoch: int,
    segments,
    cum_durations: list,
    start_idx: int,
    end_idx: int,
    audio_offset: float,
) -> HLSSegment:
    """Construct an HLSSegment from playlist data and index range."""
    urls = [segments[i].base_uri + segments[i].uri for i in range(start_idx, end_idx)]
    return HLSSegment(
        bucket=bucket,
        hydrophone_id=hydrophone_id,
        folder_epoch=folder_epoch,
        start_index=start_idx,
        end_index=end_idx,
        segment_urls=urls,
        start_offset_s=cum_durations[start_idx] + audio_offset,
        end_offset_s=cum_durations[end_idx] + audio_offset,
    )


def date_range_segments(
    bucket: str,
    hydrophone_id: str,
    start_unix: int,
    end_unix: int,
    clip_duration_s: int = 60,
    audio_offset: float = _DEFAULT_AUDIO_OFFSET,
) -> Iterator[HLSSegment]:
    """Yield ``HLSSegment`` objects covering ``[start_unix, end_unix)``.

    Each segment represents *clip_duration_s* seconds of audio (or as close
    as the M3U8 .ts boundaries allow).

    Parameters
    ----------
    bucket : str
        S3 bucket name (e.g. ``"audio-orcasound-net"``).
    hydrophone_id : str
        Hydrophone location slug (e.g. ``"rpi_orcasound_lab"``).
    start_unix, end_unix : int
        Unix-timestamp boundaries.
    clip_duration_s : int
        Target clip length in seconds (default 60).
    audio_offset : float
        Seconds after folder epoch before audio starts (default 2).

    Yields
    ------
    HLSSegment
    """
    folders = hls_locator.list_folders_in_range(bucket, hydrophone_id, start_unix, end_unix)
    if not folders:
        logger.warning(
            "No HLS folders for %s in [%d, %d]", hydrophone_id, start_unix, end_unix,
        )
        return

    logger.info("Found %d folders in date range", len(folders))
    cursor = start_unix

    for folder_epoch in folders:
        if cursor >= end_unix:
            return

        segments, cum = _load_playlist(bucket, hydrophone_id, folder_epoch)
        if not segments:
            continue

        n = len(segments)
        avg_dur = cum[-1] / n

        while cursor < end_unix:
            offset = cursor - folder_epoch - audio_offset
            start_idx = math.ceil(max(0.0, offset) / avg_dur) if offset >= 0 else 0
            num_segs = math.ceil(clip_duration_s / avg_dur)
            end_idx = start_idx + num_segs

            if end_idx > n:
                # This folder is exhausted — advance to next.
                break

            seg = _build_segment(
                bucket, hydrophone_id, folder_epoch,
                segments, cum, start_idx, end_idx, audio_offset,
            )
            cursor = int(seg.end_unix)
            yield seg


def live_segments(
    bucket: str,
    hydrophone_id: str,
    stream_base_url: str,
    clip_duration_s: int = 60,
    audio_offset: float = _DEFAULT_AUDIO_OFFSET,
    buffer_s: float = 10.0,
) -> Iterator[HLSSegment]:
    """Yield ``HLSSegment`` objects from a live HLS stream, blocking as needed.

    This generator runs indefinitely (until the caller breaks out).  It
    sleeps until enough audio has accumulated, then yields the next segment.

    Parameters
    ----------
    bucket : str
        S3 bucket (e.g. ``"audio-orcasound-net"``).
    hydrophone_id : str
        Hydrophone slug.
    stream_base_url : str
        Full S3 base URL, e.g.
        ``"https://s3-us-west-2.amazonaws.com/audio-orcasound-net/rpi_orcasound_lab"``.
    clip_duration_s : int
        Target clip length in seconds (default 60).
    audio_offset : float
        Seconds after folder epoch before audio starts (default 2).
    buffer_s : float
        Extra seconds to wait after the expected segment end, to ensure
        all .ts files are uploaded (default 10).

    Yields
    ------
    HLSSegment
    """
    latest_url = f"{stream_base_url}/latest.txt"

    # Initialise cursor slightly in the past so first iteration fetches immediately.
    cursor_unix = datetime.now(timezone.utc).timestamp() - buffer_s

    while True:
        # --- determine how long to sleep ---
        now = datetime.now(timezone.utc).timestamp()
        sleep_s = (cursor_unix - now) + buffer_s
        if sleep_s > 0:
            time.sleep(sleep_s)

        # --- fetch latest folder epoch ---
        try:
            with urllib.request.urlopen(latest_url) as resp:
                folder_epoch = int(resp.read().decode("utf-8").strip())
        except (urllib.error.URLError, ValueError) as exc:
            logger.warning("Failed to fetch latest.txt: %s", exc)
            cursor_unix = datetime.now(timezone.utc).timestamp()
            continue

        # --- check m3u8 ---
        if not hls_locator.m3u8_exists(bucket, hydrophone_id, folder_epoch):
            logger.info(".m3u8 file does not exist, will retry")
            cursor_unix = datetime.now(timezone.utc).timestamp()
            continue

        # --- load playlist ---
        segments, cum = _load_playlist(bucket, hydrophone_id, folder_epoch)
        if not segments:
            cursor_unix = datetime.now(timezone.utc).timestamp()
            continue

        n = len(segments)
        avg_dur = cum[-1] / n

        # --- compute segment range ---
        time_since_start = cursor_unix - folder_epoch - audio_offset
        if time_since_start < clip_duration_s + 20:
            # Not enough data yet; retry after a short wait.
            logger.info("Not enough data for a %ds clip + 20s buffer", clip_duration_s)
            cursor_unix = datetime.now(timezone.utc).timestamp()
            continue

        num_segs = math.ceil(clip_duration_s / avg_dur)
        end_idx = math.ceil(time_since_start / avg_dur)
        start_idx = max(0, end_idx - num_segs)

        if end_idx > n:
            cursor_unix = datetime.now(timezone.utc).timestamp()
            continue

        seg = _build_segment(
            bucket, hydrophone_id, folder_epoch,
            segments, cum, start_idx, end_idx, audio_offset,
        )
        cursor_unix = seg.end_unix
        yield seg
