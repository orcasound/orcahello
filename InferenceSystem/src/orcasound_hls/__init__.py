"""Orcasound HLS streaming utilities.

Replacement for orca-hls-utils that computes timestamps deterministically
from S3 folder epochs and M3U8 segment metadata rather than system time.
"""

from .stream import LiveHLSStream, DateRangeHLSStream

__all__ = ["LiveHLSStream", "DateRangeHLSStream"]
