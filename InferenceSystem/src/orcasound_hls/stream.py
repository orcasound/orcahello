"""HLS stream classes that compute timestamps deterministically from S3 metadata.

Replaces orca_hls_utils.HLSStream and orca_hls_utils.DateRangeHLSStream.
Key improvement: clip timestamps are derived from the S3 folder epoch +
actual M3U8 segment durations, not from system time or rounded averages.
"""

import logging
import math
import os
import shutil
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path
from tempfile import TemporaryDirectory

import ffmpeg
import m3u8

from . import hls_locator

logger = logging.getLogger(__name__)

# Seconds after folder epoch before first audio segment is available.
DEFAULT_AUDIO_OFFSET = 2


def _download_file(url: str, dest_dir: str) -> str:
    """Download a file to dest_dir, returning the local filename."""
    filename = os.path.basename(url)
    dest = os.path.join(dest_dir, filename)
    if not os.path.isfile(dest):
        urllib.request.urlretrieve(url, dest)
    return filename


def _concat_ts_to_wav(ts_dir: str, filenames: list, clip_name: str, wav_dir: str) -> str:
    """Concatenate .ts segment files and convert to .wav."""
    hls_path = os.path.join(ts_dir, clip_name + ".ts")
    wav_path = os.path.join(wav_dir, clip_name + ".wav")

    with open(hls_path, "wb") as out:
        for fname in filenames:
            with open(os.path.join(ts_dir, fname), "rb") as inp:
                out.write(inp.read())

    try:
        stream = ffmpeg.input(hls_path)
        stream = ffmpeg.output(stream, wav_path)
        ffmpeg.run(stream, quiet=True)
    except ffmpeg.Error as e:
        logger.error("FFmpeg failed: %s", e.stderr.decode("utf8", errors="ignore") if e.stderr else "")
        raise

    return wav_path


def _clipname_from_utc(hydrophone_id: str, utc_dt: datetime) -> str:
    """Generate a human-readable clip name in Pacific time."""
    from pytz import timezone as pytz_tz

    pst = utc_dt.astimezone(pytz_tz("US/Pacific"))
    return hydrophone_id + "_" + pst.strftime("%Y_%m_%d_%H_%M_%S_%Z")


def _compute_segment_timestamp(folder_epoch: int, segment_start_offset_s: float) -> str:
    """Compute a deterministic ISO-8601 UTC timestamp from folder epoch + segment offset."""
    ts = folder_epoch + segment_start_offset_s
    dt = datetime.fromtimestamp(ts, tz=timezone.utc)
    return dt.strftime("%Y-%m-%dT%H:%M:%SZ")


class LiveHLSStream:
    """Stream that follows the live HLS feed from an Orcasound hydrophone.

    Drop-in replacement for orca_hls_utils.HLSStream with deterministic timestamps.
    """

    def __init__(self, stream_base: str, polling_interval: int, wav_dir: str, audio_offset: int = DEFAULT_AUDIO_OFFSET):
        self.stream_base = stream_base
        self.polling_interval = polling_interval
        self.wav_dir = wav_dir
        self.audio_offset = audio_offset

        bucket_folder = stream_base.split("https://s3-us-west-2.amazonaws.com/")[1]
        tokens = bucket_folder.split("/")
        self.s3_bucket = tokens[0]
        self.hydrophone_id = tokens[1]

    def _get_latest_folder_epoch(self):
        url = f"{self.stream_base}/latest.txt"
        try:
            with urllib.request.urlopen(url) as resp:
                return resp.read().decode("utf-8").strip()
        except (urllib.error.HTTPError, urllib.error.URLError) as e:
            logger.warning("Failed to fetch latest.txt: %s", e)
            return None

    def get_next_clip(self, current_clip_end_time):
        """Fetch the next audio clip.

        Parameters
        ----------
        current_clip_end_time : datetime
            Naive UTC datetime cursor (for API compat with orca-hls-utils).

        Returns
        -------
        (wav_path, start_timestamp_iso, new_clip_end_time) or (None, None, current_clip_end_time)
        """
        # Sleep until enough data should be available
        now = datetime.utcnow()
        sleep_s = (current_clip_end_time - now).total_seconds() + 10
        if sleep_s > 0:
            time.sleep(sleep_s)

        folder_epoch_str = self._get_latest_folder_epoch()
        if folder_epoch_str is None:
            return None, None, current_clip_end_time

        folder_epoch = int(folder_epoch_str)

        # Check m3u8 exists
        if not hls_locator.m3u8_exists(self.s3_bucket, self.hydrophone_id, folder_epoch):
            logger.info(".m3u8 file does not exist, will retry after some time")
            return None, None, current_clip_end_time

        # Load playlist
        playlist_url = hls_locator.m3u8_url(self.s3_bucket, self.hydrophone_id, folder_epoch)
        stream_obj = m3u8.load(playlist_url)
        segments = stream_obj.segments
        num_segments = len(segments)
        if num_segments == 0:
            return None, None, current_clip_end_time

        # Build cumulative duration array from actual segment durations
        cum_durations = [0.0]
        for seg in segments:
            cum_durations.append(cum_durations[-1] + seg.duration)

        total_playlist_duration = cum_durations[-1]
        avg_duration = total_playlist_duration / num_segments

        # Time offset from folder start to our cursor
        cursor_unix = current_clip_end_time.replace(tzinfo=timezone.utc).timestamp()
        time_since_folder_start = cursor_unix - folder_epoch - self.audio_offset

        if time_since_folder_start < self.polling_interval + 20:
            logger.info("not enough data for a 1 minute clip + 20 second buffer")
            return None, None, current_clip_end_time

        # Find segment indices using cumulative durations
        num_clip_segments = math.ceil(self.polling_interval / avg_duration)
        end_seg_idx = math.ceil(time_since_folder_start / avg_duration)
        start_seg_idx = end_seg_idx - num_clip_segments

        if start_seg_idx < 0:
            start_seg_idx = 0
        if end_seg_idx > num_segments:
            return None, None, current_clip_end_time

        # Compute deterministic timestamps from actual segment positions
        clip_start_offset = cum_durations[start_seg_idx] + self.audio_offset
        clip_end_offset = cum_durations[end_seg_idx] + self.audio_offset
        start_timestamp = _compute_segment_timestamp(folder_epoch, clip_start_offset)

        # New cursor: the actual end time of the segments we consumed
        end_utc = datetime.utcfromtimestamp(folder_epoch + clip_end_offset)

        # Download segments
        tmp_path = "tmp_path"
        os.makedirs(tmp_path, exist_ok=True)
        try:
            file_names = []
            for i in range(start_seg_idx, end_seg_idx):
                seg = segments[i]
                url = seg.base_uri + seg.uri
                try:
                    fname = _download_file(url, tmp_path)
                    file_names.append(fname)
                except Exception:
                    logger.warning("Skipping segment %s: download error", seg.uri)

            if not file_names:
                return None, None, current_clip_end_time

            clip_start_utc = datetime.utcfromtimestamp(folder_epoch + clip_start_offset)
            clipname = _clipname_from_utc(self.hydrophone_id, clip_start_utc.replace(tzinfo=timezone.utc))
            wav_path = _concat_ts_to_wav(tmp_path, file_names, clipname, self.wav_dir)
        finally:
            shutil.rmtree(tmp_path, ignore_errors=True)

        return wav_path, start_timestamp, end_utc

    def is_stream_over(self):
        return False


class DateRangeHLSStream:
    """Stream that iterates over a historical date range of HLS audio.

    Drop-in replacement for orca_hls_utils.DateRangeHLSStream with:
    - Prefix-filtered S3 listing (no full bucket scan)
    - Deterministic timestamps from segment metadata
    """

    def __init__(
        self,
        stream_base: str,
        polling_interval: int,
        start_unix_time: int,
        end_unix_time: int,
        wav_dir: str,
        overwrite_output: bool = False,
        real_time: bool = False,
        audio_offset: int = DEFAULT_AUDIO_OFFSET,
    ):
        self.stream_base = stream_base
        self.polling_interval = polling_interval
        self.start_unix_time = start_unix_time
        self.end_unix_time = end_unix_time
        self.wav_dir = wav_dir
        self.overwrite_output = overwrite_output
        self.real_time = real_time
        self.audio_offset = audio_offset

        bucket_folder = stream_base.split("https://s3-us-west-2.amazonaws.com/")[1]
        tokens = bucket_folder.split("/")
        self.s3_bucket = tokens[0]
        self.hydrophone_id = tokens[1]

        Path(self.wav_dir).mkdir(parents=True, exist_ok=True)

        logger.info(
            "DateRangeHLSStream: listing folders for %s in [%d, %d]",
            self.hydrophone_id, start_unix_time, end_unix_time,
        )
        self.valid_folders = hls_locator.list_folders_in_range(
            self.s3_bucket, self.hydrophone_id, start_unix_time, end_unix_time,
        )
        logger.info("Found %d folders in date range", len(self.valid_folders))
        if not self.valid_folders:
            raise IndexError(
                f"No HLS folders found for {self.hydrophone_id} "
                f"between {start_unix_time} and {end_unix_time}"
            )

        self.current_folder_index = 0
        self.current_clip_start_time = start_unix_time

    def get_next_clip(self, current_clip_name=None):
        """Fetch the next audio clip from the date range.

        Parameters
        ----------
        current_clip_name : datetime or None
            When running in "demo" mode, a datetime to relabel timestamps.
            Normal usage passes the naive UTC cursor from the orchestrator;
            this is accepted but unused for non-demo mode.

        Returns
        -------
        (wav_path, start_timestamp_iso, current_clip_name) or (None, None, None)
        """
        if self.current_folder_index >= len(self.valid_folders):
            return None, None, None

        folder_epoch = int(self.valid_folders[self.current_folder_index])

        # Load playlist for this folder
        playlist_url = hls_locator.m3u8_url(self.s3_bucket, self.hydrophone_id, folder_epoch)
        stream_obj = m3u8.load(playlist_url)
        segments = stream_obj.segments
        num_segments = len(segments)

        if num_segments == 0:
            self.current_folder_index += 1
            if self.current_folder_index < len(self.valid_folders):
                self.current_clip_start_time = self.valid_folders[self.current_folder_index]
            return None, None, None

        # Build cumulative duration array from actual segment durations
        cum_durations = [0.0]
        for seg in segments:
            cum_durations.append(cum_durations[-1] + seg.duration)

        total_duration = cum_durations[-1]
        avg_duration = total_duration / num_segments

        # Time offset from folder start to our clip start
        time_since_folder_start = self.current_clip_start_time - folder_epoch - self.audio_offset

        start_seg_idx = math.ceil(max(0, time_since_folder_start) / avg_duration) if time_since_folder_start >= 0 else 0
        num_clip_segments = math.ceil(self.polling_interval / avg_duration)
        end_seg_idx = start_seg_idx + num_clip_segments

        if end_seg_idx > num_segments:
            # Not enough segments in this folder, move to next
            self.current_folder_index += 1
            if self.current_folder_index < len(self.valid_folders):
                # Keep cursor at the later of the next folder start or our
                # current position so we don't jump backwards in time.
                self.current_clip_start_time = max(
                    self.valid_folders[self.current_folder_index],
                    self.current_clip_start_time,
                )
            return None, None, None

        # Compute deterministic clip timestamp
        clip_start_offset = cum_durations[start_seg_idx] + self.audio_offset
        clip_end_offset = cum_durations[end_seg_idx] + self.audio_offset
        clip_start_time_iso = _compute_segment_timestamp(folder_epoch, clip_start_offset)

        # Advance cursor for next call: use actual segment end time
        self.current_clip_start_time = int(folder_epoch + clip_end_offset)

        if self.real_time and current_clip_name:
            now = datetime.utcnow()
            sleep_s = (current_clip_name - now).total_seconds()
            if sleep_s > 0:
                time.sleep(sleep_s)

        # Download segments
        with TemporaryDirectory() as tmp_path:
            file_names = []
            for i in range(start_seg_idx, end_seg_idx):
                seg = segments[i]
                url = seg.base_uri + seg.uri
                try:
                    fname = _download_file(url, tmp_path)
                    file_names.append(fname)
                except Exception:
                    logger.warning("Skipping segment %s: download error", seg.uri)

            if not file_names:
                return None, None, None

            # Generate clip name from deterministic timestamp
            clip_start_utc = datetime.fromtimestamp(folder_epoch + clip_start_offset, tz=timezone.utc)
            clipname = _clipname_from_utc(
                self.hydrophone_id.replace("_", "-"),
                clip_start_utc,
            )
            wav_path = os.path.join(self.wav_dir, clipname + ".wav")

            # Concatenate .ts files
            hls_path = os.path.join(tmp_path, clipname + ".ts")
            with open(hls_path, "wb") as out:
                for fname in file_names:
                    with open(os.path.join(tmp_path, fname), "rb") as inp:
                        shutil.copyfileobj(inp, out)

            stream = ffmpeg.input(hls_path)
            stream = ffmpeg.output(stream, wav_path)
            ffmpeg.run(stream, overwrite_output=self.overwrite_output, quiet=True)

        return wav_path, clip_start_time_iso, current_clip_name

    def is_stream_over(self):
        return int(self.current_clip_start_time) >= int(self.end_unix_time)
