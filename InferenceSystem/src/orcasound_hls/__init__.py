"""Orcasound HLS streaming utilities.

Replacement for orca-hls-utils that computes timestamps deterministically
from S3 folder epochs and M3U8 segment metadata rather than system time.

Example usage::

    from orcasound_hls import date_range_segments, HLSSegment

    for segment in date_range_segments("audio-orcasound-net", "rpi_orcasound_lab",
                                        start_unix=1598998380, end_unix=1599003900):
        wav_path = segment.download_as_wav("output_dir/")
        print(segment.start_utc, wav_path)
"""

from .segment import HLSSegment
from .iterators import date_range_segments, live_segments

__all__ = ["HLSSegment", "date_range_segments", "live_segments"]
