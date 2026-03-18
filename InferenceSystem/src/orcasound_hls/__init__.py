"""Orcasound HLS streaming utilities.

Replacement for orca-hls-utils that computes timestamps deterministically
from S3 folder epochs and M3U8 segment metadata rather than system time.

Example usage::

    from orcasound_hls import OrcasoundHLSClient, OrcasoundHLSSegment

    client = OrcasoundHLSClient("audio-orcasound-net", "rpi_orcasound_lab")
    for segment in client.get_segments(start_unix=1598998380, end_unix=1599003900):
        wav_path = segment.download_as_wav("output_dir/")
        print(segment.start_utc, wav_path)
"""

from .client import OrcasoundHLSClient
from .types import OrcasoundHLSSegment

__all__ = ["OrcasoundHLSClient", "OrcasoundHLSSegment"]
