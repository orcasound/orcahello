"""Offline tests: orcasound_hls against node_2026-style S3 folders.

node_2026 orcanodes write a short rolling live.m3u8 (-hls_list_size 10, with
EXT-X-PROGRAM-DATE-TIME) plus a full-session archive.m3u8 uploaded every ~5 min.
S3 is faked, so these run without network access.
"""
from datetime import datetime, timedelta, timezone
from unittest import mock

import m3u8
import pytest

from orcasound_hls import OrcasoundHLSClient
from orcasound_hls import client as C

BUCKET, HYD = "audio-orcasound-net", "rpi_orcasound_lab"
PST = timezone(timedelta(hours=-8))
SEG = 10.005333


class FakeNode:
    """One or more folders; each folder = ffmpeg session started at folder+2 s."""

    def __init__(self, folders, now, list_size=10, archive_lag=300.0, missing=(), pdt=True,
                 ended=None):
        self.folders = sorted(folders)
        self.now = now
        self.list_size = list_size
        self.archive_lag = archive_lag
        self.missing = set(missing)        # (folder, seq) not on S3
        self.pdt = pdt
        self.ended = ended or {}           # folder -> last seq (session stopped)
        self.heads = []

    def start(self, folder, n):
        return folder + 2.0 + n * SEG

    def last_seq(self, folder, at):
        last = int((at - folder - 2.0) // SEG) - 1
        return min(last, self.ended.get(folder, last))

    def url(self, folder, n):
        return f"https://s3-us-west-2.amazonaws.com/{BUCKET}/{HYD}/hls/{folder}/live{n:03d}.ts"

    def _playlist(self, folder, first, last):
        out = ["#EXTM3U", "#EXT-X-VERSION:3", "#EXT-X-TARGETDURATION:10",
               f"#EXT-X-MEDIA-SEQUENCE:{first}"]
        for n in range(first, last + 1):
            if self.pdt:
                pdt = datetime.fromtimestamp(self.start(folder, n), tz=PST).isoformat(timespec="milliseconds")
                out.append(f"#EXT-X-PROGRAM-DATE-TIME:{pdt}")
            out += [f"#EXTINF:{SEG:.6f},", f"live{n:03d}.ts"]
        return "\n".join(out) + "\n"

    def load_playlist(self, bucket, hyd, folder, name):
        base = f"https://s3-us-west-2.amazonaws.com/{bucket}/{hyd}/hls/{folder}/{name}"
        last = self.last_seq(folder, self.now)
        if last < 0:
            return None
        if name == "live.m3u8":
            first = max(0, last - self.list_size + 1) if self.list_size else 0
            text = self._playlist(folder, first, last)
        elif name == "archive.m3u8":
            if self.archive_lag is None:
                return None
            alast = self.last_seq(folder, self.now - self.archive_lag)
            if alast < 0:
                return None
            text = self._playlist(folder, 0, alast)
        else:
            return None
        return m3u8.loads(text, uri=base)

    def url_exists(self, url):
        self.heads.append(url)
        folder = int(url.split("/hls/")[1].split("/")[0])
        n = int(url.rsplit("live", 1)[1].split(".")[0])
        return 0 <= n <= self.last_seq(folder, self.now) and (folder, n) not in self.missing

    def list_folders(self, bucket, hyd, s, e):
        return [f for f in self.folders if s - 86400 <= f <= e]

    def patch(self):
        return mock.patch.multiple(
            C,
            load_playlist=self.load_playlist,
            url_exists=self.url_exists,
            list_folders_in_range=self.list_folders,
        )


MIDNIGHT = int(datetime(2026, 11, 12, 0, 0, tzinfo=PST).timestamp())
YESTERDAY = MIDNIGHT - 86400


def live_window(now, size=60, delay=60):
    aligned = (now // size) * size
    end = aligned - delay
    return end - size, end


def get(node, s, e, size=60):
    with node.patch():
        return OrcasoundHLSClient(BUCKET, HYD).get_segments(s, e, segment_size=size)


def assert_exact_times(node, segs):
    for sg in segs:
        assert sg.start_unix == pytest.approx(node.start(sg.folder_epoch, sg.start_index), abs=1e-3)
        assert sg.end_unix == pytest.approx(node.start(sg.folder_epoch, sg.end_index), abs=1e-3)
        assert sg.segment_urls == [node.url(sg.folder_epoch, n) for n in range(sg.start_index, sg.end_index)]


def test_live_poll_returns_one_clip_with_exact_times():
    now = MIDNIGHT + 13 * 3600 + 50 * 60 + 27          # 21:50:27 PST
    node = FakeNode([YESTERDAY, MIDNIGHT], now)
    s, e = live_window(now)
    segs = get(node, s, e)
    assert len(segs) == 1
    sg = segs[0]
    assert 55 <= sg.duration_s <= 61
    assert s - SEG < sg.start_unix <= s + SEG
    assert_exact_times(node, segs)
    assert sg.approximate            # window starts before the 10-segment live.m3u8
    # window reaches ~147 s back; a 10-segment live.m3u8 covers ~110 s -> ~4 probes, no archive read
    assert 1 <= len(node.heads) <= 6


def test_consecutive_live_polls_are_contiguous():
    t = MIDNIGHT + 15 * 3600 + 3
    clips = []
    for _ in range(30):
        node = FakeNode([YESTERDAY, MIDNIGHT], t)
        s, e = live_window(t)
        clips += get(node, s, e)
        t += 60
    assert len(clips) >= 28
    for a, b in zip(clips, clips[1:]):
        assert b.start_index >= a.end_index, "overlapping clips"
        assert b.start_index - a.end_index <= 1, "more than one .ts skipped between polls"


def test_date_range_hours_back_uses_archive_exactly():
    now = MIDNIGHT + 21 * 3600
    node = FakeNode([YESTERDAY, MIDNIGHT], now)
    s = MIDNIGHT + 10 * 3600
    segs = get(node, s, s + 600)
    assert len(segs) >= 9
    assert_exact_times(node, segs)
    assert not any(sg.approximate for sg in segs)
    assert node.heads == []


def test_recent_range_bridges_stale_archive_and_live():
    now = MIDNIGHT + 21 * 3600
    node = FakeNode([MIDNIGHT], now, archive_lag=300)
    s, e = now - 290, now - 20          # starts in archive, crosses lag hole, ends in live
    segs = get(node, s, e)
    assert_exact_times(node, segs)
    idx = [n for sg in segs for n in range(sg.start_index, sg.end_index)]
    assert idx == list(range(idx[0], idx[-1] + 1))


def test_missing_ts_splits_clip():
    now = MIDNIGHT + 21 * 3600 + 30
    s, e = live_window(now, size=60, delay=60)
    probe = FakeNode([MIDNIGHT], now)
    first_needed = int((s - MIDNIGHT - 2) // SEG)
    node = FakeNode([MIDNIGHT], now, missing={(MIDNIGHT, first_needed + 1)})
    segs = get(node, s - 120, e)
    for sg in segs:
        assert not (sg.start_index <= first_needed + 1 < sg.end_index)


def test_midnight_rollover():
    now = MIDNIGHT + 20 * 60
    last_y = int((86400 - 3 - 2) // SEG)        # yesterday's session stopped just before midnight
    node = FakeNode([YESTERDAY, MIDNIGHT], now, ended={YESTERDAY: last_y})
    segs = get(node, MIDNIGHT - 300, MIDNIGHT + 300)
    folders = {sg.folder_epoch for sg in segs}
    assert folders == {YESTERDAY, MIDNIGHT}
    for a, b in zip(segs, segs[1:]):
        assert b.start_unix >= a.end_unix - 2e-3   # PDT has ms resolution
    assert_exact_times(node, segs)


def test_extrapolated_names_across_999_to_1000():
    folder = MIDNIGHT
    now = folder + 2 + SEG * 1011               # live.m3u8 lists live1000..live1009
    node = FakeNode([folder], now)
    s = node.start(folder, 995)
    segs = get(node, s, node.start(folder, 1009))
    urls = [u for sg in segs for u in sg.segment_urls]
    assert urls[0].endswith("/live995.ts")
    assert any(u.endswith("/live999.ts") for u in urls) and any(u.endswith("/live1000.ts") for u in urls)


def test_legacy_full_playlist_without_pdt_unchanged():
    """Legacy folders: live.m3u8 from live000.ts, no PDT -> folder + 2.0 + cum durations."""
    folder = YESTERDAY
    node = FakeNode([folder], folder + 3 * 3600, list_size=0, pdt=False, archive_lag=None)
    s = folder + 502
    segs = get(node, s, s + 65)
    assert len(segs) == 1
    sg = segs[0]
    assert sg.folder_to_audio_offset_s == 2.0
    assert sg.start_unix == pytest.approx(node.start(folder, sg.start_index), abs=1e-4)
    assert not sg.approximate


def test_live_poll_with_longer_live_window_needs_no_probes():
    """With LIVE_LIST_SIZE=20 on the node, the live window covers the poll entirely."""
    now = MIDNIGHT + 13 * 3600 + 50 * 60 + 27
    node = FakeNode([YESTERDAY, MIDNIGHT], now, list_size=20)
    s, e = live_window(now)
    segs = get(node, s, e)
    assert len(segs) == 1 and not segs[0].approximate
    assert node.heads == []
