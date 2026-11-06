"""S3/M3U8 helpers for Orcasound HLS streams.
"""

from __future__ import annotations

import bisect
import logging
import urllib.error
import urllib.request
from typing import List, Optional, Tuple

import boto3
import m3u8
from botocore import UNSIGNED
from botocore.config import Config

from orcasound_hls.types import FOLDER_TO_AUDIO_OFFSET, HLS_DOWNLOAD_TIMEOUT_S

logger = logging.getLogger(__name__)

S3_BASE_URL = "https://s3-us-west-2.amazonaws.com"


# --- S3 helpers ---


def _s3_client():
    return boto3.client("s3", config=Config(signature_version=UNSIGNED))


def _list_folders_with_prefix(
    bucket: str, hls_prefix: str, ts_prefix: str
) -> List[int]:
    """List S3 HLS folder names (unix epochs) matching a timestamp prefix."""
    s3 = _s3_client()
    paginator = s3.get_paginator("list_objects_v2")
    full_prefix = f"{hls_prefix}{ts_prefix}"

    folders: List[int] = []
    for page in paginator.paginate(Bucket=bucket, Prefix=full_prefix, Delimiter="/"):
        for cp in page.get("CommonPrefixes", []):
            name = cp["Prefix"].rstrip("/").rsplit("/", 1)[-1]
            try:
                folders.append(int(name))
            except ValueError:
                continue
    return sorted(folders)


def find_folder_for_timestamp(
    bucket: str,
    hydrophone_id: str,
    unix_ts: int,
    prefix_length: int = 4,
) -> Tuple[Optional[int], Optional[int]]:
    """Find the HLS folder that contains audio for *unix_ts*.

    Returns (folder_epoch, offset_seconds) or (None, None).
    """
    hls_prefix = f"{hydrophone_id}/hls/"
    ts_prefix = str(unix_ts)[:prefix_length]

    # Check the current and preceding windows before widening the prefix, so
    # crossing a prefix boundary does not require scanning the whole archive.
    while True:
        folders = _list_folders_with_prefix(bucket, hls_prefix, ts_prefix)
        idx = bisect.bisect_right(folders, unix_ts)
        if idx > 0:
            break
        if not ts_prefix:
            logger.warning(
                "No HLS folders found before timestamp %d for %s", unix_ts, hydrophone_id
            )
            return None, None
        prev_prefix = int(ts_prefix) - 1
        if prev_prefix > 0:
            folders = _list_folders_with_prefix(
                bucket, hls_prefix, str(prev_prefix).zfill(len(ts_prefix))
            )
            idx = bisect.bisect_right(folders, unix_ts)
            if idx > 0:
                break
        ts_prefix = ts_prefix[:-1]

    folder_epoch = folders[idx - 1]
    try:
        segments = load_hls_playlist(bucket, hydrophone_id, folder_epoch)
    except Exception as exc:
        logger.warning("Failed to load M3U8 for folder %d: %s", folder_epoch, exc)
        return None, None

    audio_end = (
        folder_epoch + FOLDER_TO_AUDIO_OFFSET + sum(s.duration for s in segments)
    )
    if not segments or unix_ts >= audio_end:
        return None, None

    offset = unix_ts - folder_epoch
    return folder_epoch, offset


def list_folders_in_range(
    bucket: str,
    hydrophone_id: str,
    start_unix: int,
    end_unix: int,
    prefix_length: int = 4,
) -> List[int]:
    """List all HLS folders that may contain audio in [start_unix, end_unix].

    Uses prefix-filtered listing to avoid scanning all folders.
    """
    hls_prefix = f"{hydrophone_id}/hls/"
    start_prefix = int(str(start_unix)[:prefix_length])
    end_prefix = int(str(end_unix)[:prefix_length])

    all_folders: List[int] = []
    for p in range(start_prefix, end_prefix + 1):
        if p > 0:
            all_folders.extend(_list_folders_with_prefix(bucket, hls_prefix, str(p)))

    folder_epoch, _ = find_folder_for_timestamp(
        bucket, hydrophone_id, start_unix, prefix_length
    )
    folders = {f for f in all_folders if start_unix <= f <= end_unix}
    if folder_epoch is not None:
        folders.add(folder_epoch)
    return sorted(folders)


def m3u8_url(bucket: str, hydrophone_id: str, folder_epoch: int) -> str:
    return f"{S3_BASE_URL}/{bucket}/{hydrophone_id}/hls/{folder_epoch}/live.m3u8"


def m3u8_exists(bucket: str, hydrophone_id: str, folder_epoch: int) -> bool:
    s3 = _s3_client()
    prefix = f"{hydrophone_id}/hls/{folder_epoch}/live.m3u8"
    resp = s3.list_objects_v2(Bucket=bucket, Prefix=prefix, MaxKeys=1, Delimiter="/")
    return resp.get("KeyCount", 0) > 0


# --- Playlist helpers ---


def load_hls_playlist(bucket: str, hydrophone_id: str, folder_epoch: int):
    """Load an M3U8 playlist and return its segment list."""
    url = m3u8_url(bucket, hydrophone_id, folder_epoch)
    # Explicit timeout: m3u8.load defaults to None (infinite)
    stream_obj = m3u8.load(url, timeout=HLS_DOWNLOAD_TIMEOUT_S)
    return stream_obj.segments


def fetch_latest_folder_epoch(stream_base_url: str) -> float:
    """Fetch ``{stream_base_url}/latest.txt`` and return the folder epoch.

    Raises ``RuntimeError`` on network or parse failure.
    """
    latest_url = f"{stream_base_url}/latest.txt"
    try:
        with urllib.request.urlopen(latest_url, timeout=10) as resp:
            return int(resp.read().decode("utf-8").strip())
    except (urllib.error.URLError, ValueError) as exc:
        raise RuntimeError(
            f"Failed to fetch latest.txt from {latest_url}: {exc}"
        ) from exc
