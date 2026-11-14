"""OrcasoundHLSClient — synchronous client for fetching HLS segments.

How a time window is mapped to .ts files
----------------------------------------
Each S3 folder ``<hydrophone>/hls/<folder_epoch>/`` holds ``liveNNN.ts`` files,
where NNN is the HLS media sequence number. For every folder that could cover
the window, the client builds a *timeline*: (sequence number, URL, start time,
duration) for each .ts it needs.

* **Playlists with EXT-X-PROGRAM-DATE-TIME** (node_2026 nodes, and folders
  fixed by orcanode's repair_manifests.py): the start time of each listed .ts
  is its PDT, and its sequence number is ``EXT-X-MEDIA-SEQUENCE`` + position.
  node_2026 keeps ``live.m3u8`` to a short rolling window (~10 segments), so:

  - if the window is in or after the rolling window, only live.m3u8 is read;
  - if the window starts a little before it (the normal LiveHLS case with a
    60 s delay buffer), the missing .ts are addressed directly by sequence
    number, timed by stepping back from the earliest PDT with the measured
    mean EXTINF, and checked with a HEAD request;
  - if the window is further back, the folder's archive.m3u8 (full session,
    uploaded every ~5 min) supplies exact PDTs for those segments.

* **Legacy playlists without PDT** (full live.m3u8 starting at live000.ts):
  start = folder_epoch + FOLDER_TO_AUDIO_OFFSET + cumulative EXTINF durations,
  as before.

The timeline is then cut into clips of ~``segment_size`` seconds. A clip never
spans a missing sequence number; partial clips at gaps and at the end of the
window are dropped, as before.
"""

from __future__ import annotations

import logging
import math
import re
from dataclasses import dataclass
from typing import Dict, List, Optional

from .types import FOLDER_TO_AUDIO_OFFSET, OrcasoundHLSSegment
from .utils import (
    ARCHIVE_MANIFEST,
    LIVE_MANIFEST,
    S3_BASE_URL,
    fetch_latest_folder_epoch,
    list_folders_in_range,
    load_playlist,
    url_exists,
)

logger = logging.getLogger(__name__)

# If the window starts at most this far before the earliest segment in the
# rolling live.m3u8, address the missing .ts by sequence number instead of
# downloading the (large, up to ~5 min stale) archive.m3u8.
MAX_EXTRAPOLATION_S = 180.0

_TS_NAME = re.compile(r"^(?P<prefix>.*?)(?P<seq>\d+)(?P<suffix>\.ts)$")


@dataclass(frozen=True)
class _TimelineEntry:
    seq: int
    url: str
    start_unix: float
    duration: float
    offset: float  # folder_to_audio_offset_s convention for this entry (see types.py)
    approximate: bool = False

    @property
    def end_unix(self) -> float:
        return self.start_unix + self.duration


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

    # ------------------------------------------------------------------ #
    # Timeline construction
    # ------------------------------------------------------------------ #

    @staticmethod
    def _playlist_timeline(playlist, folder_epoch: int) -> List[_TimelineEntry]:
        """Timeline for every segment listed in one playlist."""
        segs = playlist.segments
        if not segs:
            return []
        seq0 = playlist.media_sequence or 0
        urls = [s.base_uri + s.uri for s in segs]
        durs = [float(s.duration) for s in segs]

        # m3u8 fills current_program_date_time forward from the last PDT tag.
        pdts = [getattr(s, "current_program_date_time", None) or s.program_date_time for s in segs]
        first_pdt = next((i for i, p in enumerate(pdts) if p is not None), None)

        entries: List[_TimelineEntry] = []
        if first_pdt is not None:
            # Segments before the first PDT tag (rare): step back from it.
            starts = [0.0] * len(segs)
            starts[first_pdt] = pdts[first_pdt].timestamp()
            for i in range(first_pdt - 1, -1, -1):
                starts[i] = starts[i + 1] - durs[i]
            for i in range(first_pdt + 1, len(segs)):
                starts[i] = pdts[i].timestamp() if pdts[i] is not None else starts[i - 1] + durs[i - 1]
            for i in range(len(segs)):
                entries.append(_TimelineEntry(seq0 + i, urls[i], starts[i], durs[i], 0.0))
        elif seq0 == 0:
            # Legacy full playlist from live000.ts, no PDT.
            t = folder_epoch + FOLDER_TO_AUDIO_OFFSET
            for i in range(len(segs)):
                entries.append(_TimelineEntry(i, urls[i], t, durs[i], FOLDER_TO_AUDIO_OFFSET))
                t += durs[i]
        else:
            # Rolling window without PDT (pre-2026 hls_list_size 5 era, unrepaired):
            # best available guess is sequence number x mean duration.
            mean = sum(durs) / len(durs)
            for i in range(len(segs)):
                t = folder_epoch + FOLDER_TO_AUDIO_OFFSET + (seq0 + i) * mean
                entries.append(
                    _TimelineEntry(seq0 + i, urls[i], t, durs[i], FOLDER_TO_AUDIO_OFFSET, approximate=True)
                )
        return entries

    @staticmethod
    def _extrapolate_before(
        anchor: _TimelineEntry, mean_dur: float, t0: float, min_seq: int
    ) -> List[_TimelineEntry]:
        """Entries for sequence numbers just before *anchor*, back to time *t0*.

        Stops above *min_seq* (the last sequence number already known, or -1).
        """
        m = _TS_NAME.match(anchor.url.rsplit("/", 1)[-1])
        if not m or mean_dur <= 0:
            return []
        base = anchor.url.rsplit("/", 1)[0] + "/"
        digits = m.group("seq")
        # orcanode uses ffmpeg's live%03d.ts: padded to 3 digits, wider when needed.
        pad = len(digits) if digits.startswith("0") and len(digits) > 1 else min(len(digits), 3)
        n_back = math.ceil((anchor.start_unix - t0) / mean_dur - 1e-6)
        out: List[_TimelineEntry] = []
        for k in range(n_back, 0, -1):
            seq = anchor.seq - k
            if seq <= min_seq:
                continue
            name = f"{m.group('prefix')}{seq:0{pad}d}{m.group('suffix')}"
            out.append(
                _TimelineEntry(
                    seq,
                    base + name,
                    anchor.start_unix - k * mean_dur,
                    mean_dur,
                    anchor.offset,
                    approximate=True,
                )
            )
        return out

    def _folder_timeline(self, folder_epoch: int, t0: float, t1: float) -> List[_TimelineEntry]:
        """Sorted timeline of .ts files in one folder overlapping [t0, t1)."""
        live = load_playlist(self.bucket, self.hydrophone_id, folder_epoch, LIVE_MANIFEST)
        if live is None:
            logger.warning(f"No {LIVE_MANIFEST} in folder {folder_epoch}")
            return []
        live_entries = self._playlist_timeline(live, folder_epoch)
        if not live_entries:
            return []
        known: Dict[int, _TimelineEntry] = {e.seq: e for e in live_entries}
        mean_dur = sum(e.duration for e in live_entries) / len(live_entries)

        if live_entries[-1].end_unix <= t0:
            return []  # folder ended (or has not yet reached) before the window

        earliest = live_entries[0]
        if earliest.start_unix > t0 and earliest.seq > 0:
            # Window starts before the rolling live.m3u8 window. A little way back
            # (the normal LiveHLS case) is cheaper to address by sequence number;
            # further back, read the full-session archive.m3u8.
            if earliest.start_unix - t0 > MAX_EXTRAPOLATION_S:
                archive = load_playlist(self.bucket, self.hydrophone_id, folder_epoch, ARCHIVE_MANIFEST)
                if archive is not None:
                    for e in self._playlist_timeline(archive, folder_epoch):
                        known.setdefault(e.seq, e)  # live.m3u8 entries win
                    logger.info(f"Using {ARCHIVE_MANIFEST} for folder {folder_epoch}")
                else:
                    logger.info(f"No {ARCHIVE_MANIFEST} in folder {folder_epoch}; extrapolating")

        # Holes in [t0, t1) not covered by any playlist: before the first known
        # entry, and between known entries (e.g. archive.m3u8 lags live.m3u8 by
        # up to ~5 min). Address them by sequence number and HEAD-check each.
        seqs = sorted(known)
        holes = []  # (last known seq before hole or -1, anchor entry after hole, earliest time)
        first = known[seqs[0]]
        if first.seq > 0 and first.start_unix > t0:
            holes.append((-1, first, t0))
        for a, b in zip(seqs, seqs[1:]):
            if b - a > 1 and known[b].start_unix > t0 and known[a].end_unix < t1:
                holes.append((a, known[b], max(t0, known[a].end_unix)))
        for min_seq, anchor, lo_t in holes:
            candidates = [
                c for c in self._extrapolate_before(anchor, mean_dur, lo_t, min_seq)
                if c.start_unix < t1
            ]
            found = [c for c in candidates if url_exists(c.url)]
            if len(found) < len(candidates):
                logger.info(
                    f"{len(candidates) - len(found)} of {len(candidates)} unlisted .ts "
                    f"not found on S3 in folder {folder_epoch}"
                )
            for c in found:
                known[c.seq] = c

        return [known[s] for s in sorted(known) if known[s].end_unix > t0 and known[s].start_unix < t1]

    # ------------------------------------------------------------------ #
    # Public API
    # ------------------------------------------------------------------ #

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
                timeline = self._folder_timeline(folder_epoch, cursor, end_unix)
            except Exception as exc:
                logger.warning(f"Failed to build timeline for folder {folder_epoch}: {exc}")
                continue
            if not timeline:
                continue

            segs = self._chunk(folder_epoch, timeline, segment_size)
            result.extend(segs)
            if segs:
                cursor = segs[-1].end_unix

        return result

    def _chunk(
        self, folder_epoch: int, timeline: List[_TimelineEntry], segment_size: float
    ) -> List[OrcasoundHLSSegment]:
        """Group consecutive timeline entries into clips of ~segment_size seconds."""
        out: List[OrcasoundHLSSegment] = []
        tolerance = 0.01 * segment_size  # avoid early trigger e.g. accumulated: 50.1, next_dur: 10.0
        chunk: List[_TimelineEntry] = []

        def drop(reason: str) -> None:
            if chunk:
                logger.info(
                    f"Dropping {sum(e.duration for e in chunk):.1f}s {reason} audio "
                    f"({len(chunk)} ts_segments)"
                )

        for k, e in enumerate(timeline):
            if chunk and e.seq != chunk[-1].seq + 1:
                drop("pre-gap")
                chunk = []
            chunk.append(e)

            accumulated = sum(c.duration for c in chunk)
            nxt = timeline[k + 1] if k + 1 < len(timeline) else None
            next_dur = nxt.duration if nxt is not None and nxt.seq == e.seq + 1 else 0.0

            current_exceeds = (accumulated - segment_size) > 0.0
            next_exceeds = (accumulated + next_dur - segment_size) > tolerance
            if current_exceeds or next_exceeds:
                first = chunk[0]
                offset = first.offset
                start_cum = first.start_unix - folder_epoch - offset
                out.append(
                    OrcasoundHLSSegment(
                        bucket=self.bucket,
                        hydrophone_id=self.hydrophone_id,
                        folder_epoch=folder_epoch,
                        segment_urls=[c.url for c in chunk],
                        start_index=first.seq,
                        end_index=chunk[-1].seq + 1,
                        start_cum_dur_s=start_cum,
                        end_cum_dur_s=start_cum + accumulated,
                        folder_to_audio_offset_s=offset,
                        approximate=any(c.approximate for c in chunk),
                    )
                )
                chunk = []

        drop("tail")
        return out
