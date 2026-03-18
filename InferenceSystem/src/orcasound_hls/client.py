"""OrcasoundHLSClient — synchronous client for fetching HLS segments."""

from __future__ import annotations

import logging
import math
from typing import List

from .types import OrcasoundHLSSegment
from .utils import (
    S3_BASE_URL,
    DEFAULT_AUDIO_OFFSET,
    build_segment,
    fetch_latest_folder_epoch,
    list_folders_in_range,
    load_playlist,
)

logger = logging.getLogger(__name__)


class OrcasoundHLSClient:
    """Synchronous client for fetching Orcasound HLS audio segments.

    Polling/sleep logic is intentionally left to the caller so that
    multiple hydrophones can be serviced in a single loop.
    """

    def __init__(self, bucket: str, hydrophone_id: str):
        self.bucket = bucket
        self.hydrophone_id = hydrophone_id
        self._stream_base_url = f"{S3_BASE_URL}/{bucket}/{hydrophone_id}"

    def latest_stream_start(self) -> float:
        """Latest folder unix timestamp from latest.txt. Raises on failure."""
        return fetch_latest_folder_epoch(self._stream_base_url)

    def get_segments(
        self,
        start_unix: float,
        end_unix: float,
        segment_size: float = 60.0,
        audio_offset: float = DEFAULT_AUDIO_OFFSET,
    ) -> List[OrcasoundHLSSegment]:
        """Return segments covering [start_unix, end_unix). Synchronous, no sleeping.

        Parameters
        ----------
        start_unix, end_unix : float
            Unix-timestamp boundaries.
        segment_size : float
            Target clip length in seconds (default 60).
        audio_offset : float
            Seconds after folder epoch before audio starts (default 2).

        Returns
        -------
        list[OrcasoundHLSSegment]
            May be empty if no data is available in the range.
        """
        folders = list_folders_in_range(
            self.bucket, self.hydrophone_id, int(start_unix), int(end_unix)
        )
        if not folders:
            logger.warning(
                "No HLS folders for %s in [%d, %d]",
                self.hydrophone_id, int(start_unix), int(end_unix),
            )
            return []

        logger.info("Found %d folders in date range", len(folders))
        result: List[OrcasoundHLSSegment] = []
        cursor = start_unix

        for folder_epoch in folders:
            if cursor >= end_unix:
                break

            try:
                segments, cum = load_playlist(self.bucket, self.hydrophone_id, folder_epoch)
            except Exception as exc:
                logger.warning("Failed to load M3U8 for folder %d: %s", folder_epoch, exc)
                continue

            if not segments:
                continue

            n = len(segments)
            avg_dur = cum[-1] / n

            while cursor < end_unix:
                offset = cursor - folder_epoch - audio_offset
                start_idx = math.ceil(max(0.0, offset) / avg_dur) if offset >= 0 else 0
                num_segs = math.ceil(segment_size / avg_dur)
                end_idx = start_idx + num_segs

                if end_idx > n:
                    # This folder is exhausted — advance to next.
                    break

                seg = build_segment(
                    self.bucket, self.hydrophone_id, folder_epoch,
                    segments, cum, start_idx, end_idx, audio_offset,
                )
                cursor = int(seg.end_unix)
                result.append(seg)

        return result
