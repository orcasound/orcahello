"""OrcasoundHLSClient — synchronous client for fetching HLS segments."""

from __future__ import annotations

import logging
from typing import List

from .types import FOLDER_TO_AUDIO_OFFSET, OrcasoundHLSSegment
from .utils import (
    S3_BASE_URL,
    fetch_latest_folder_epoch,
    list_folders_in_range,
    load_hls_playlist,
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
    ) -> List[OrcasoundHLSSegment]:
        """Return segments covering [start_unix, end_unix). Synchronous, no sleeping.

        Parameters
        ----------
        start_unix, end_unix : float
            Unix-timestamp boundaries.
        segment_size : float
            Target clip length in seconds (default 60).

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
                f"No HLS folders for {self.hydrophone_id} in unix timestamp range [{int(start_unix)}, {int(end_unix)}]"
            )
            return []

        logger.info(f"Found {len(folders)} folders in date range")
        result: List[OrcasoundHLSSegment] = []
        cursor = start_unix

        for folder_epoch in folders:
            if cursor >= end_unix:
                break

            try:
                ts_segments = load_hls_playlist(
                    self.bucket, self.hydrophone_id, folder_epoch
                )
            except Exception as exc:
                logger.warning(f"Failed to load M3U8 for folder {folder_epoch}: {exc}")
                continue

            if not ts_segments:
                continue

            # Build cumulative durations: cum_dur[i] = total duration of ts_segments[0:i]
            cum_dur = [0.0]
            for ts_seg in ts_segments:
                cum_dur.append(cum_dur[-1] + ts_seg.duration)

            audio_offset = FOLDER_TO_AUDIO_OFFSET

            # Find the range of ts_segment indices whose audio falls within [cursor, end_unix)
            first = 0
            while (
                first < len(ts_segments)
                and folder_epoch + cum_dur[first + 1] + audio_offset <= cursor
            ):
                first += 1
            last = first
            while (
                last < len(ts_segments)
                and folder_epoch + cum_dur[last] + audio_offset < end_unix
            ):
                last += 1

            # Chunk [first, last) into segments of max ~segment_size each
            chunk_start = first
            for i in range(first, last):
                accumulated = cum_dur[i + 1] - cum_dur[chunk_start]
                next_dur = ts_segments[i + 1].duration if i + 1 < last else 0

                current_exceeds = (accumulated - segment_size) > 0.0
                tolerance = (
                    0.01 * segment_size
                )  # avoid early trigger e.g. accumulated: 50.1, next_dur: 10.0
                next_exceeds = (accumulated + next_dur - segment_size) > tolerance

                if current_exceeds or next_exceeds:
                    urls = [
                        ts_segments[j].base_uri + ts_segments[j].uri
                        for j in range(chunk_start, i + 1)
                    ]
                    seg = OrcasoundHLSSegment(
                        bucket=self.bucket,
                        hydrophone_id=self.hydrophone_id,
                        folder_epoch=folder_epoch,
                        segment_urls=urls,
                        start_index=chunk_start,
                        end_index=i + 1,
                        start_cum_dur_s=cum_dur[chunk_start],
                        end_cum_dur_s=cum_dur[i + 1],
                    )
                    result.append(seg)
                    cursor = seg.end_unix
                    chunk_start = i + 1

            # Log tail audio that didn't fill a full segment
            tail_dur = cum_dur[last] - cum_dur[chunk_start]
            if tail_dur > 0:
                logger.info(
                    f"Dropping {tail_dur:.1f}s tail audio "
                    f"({last - chunk_start} ts_segments)"
                )

        return result
