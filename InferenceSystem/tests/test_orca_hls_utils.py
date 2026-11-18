import logging
import sys
from datetime import datetime, timezone
from pathlib import Path
from types import SimpleNamespace
from urllib.error import HTTPError, URLError

import ffmpeg
import pytest

sys.path.insert(0, str(Path(__file__).parent.parent / "src"))

import LiveInferenceOrchestrator as orchestrator


def test_normalize_hls_timestamp():
    """Verify ISO and local HLS timestamps normalize to UTC."""
    assert (
        orchestrator._normalize_hls_timestamp("2026-11-16T23:00:00-08:00")
        == "2026-11-17T07:00:00Z"
    )

    epoch = 1_700_000_000
    local_timestamp = datetime.fromtimestamp(epoch).strftime("%Y_%m_%d_%H_%M_%S")
    expected = datetime.fromtimestamp(epoch, timezone.utc).strftime(
        "%Y-%m-%dT%H:%M:%SZ"
    )
    assert orchestrator._normalize_hls_timestamp(local_timestamp) == expected


def test_run_loop_uses_orca_hls_stream_for_date_range(monkeypatch, tmp_path):
    """Verify date-range inference uses the shared stream and UTC metadata."""
    monkeypatch.chdir(tmp_path)
    stream_arguments = {}
    uploaded = {}

    class FakeDateRangeHLSStream:
        def __init__(self, **kwargs):
            """Record stream arguments and initialize a single-clip stream."""
            stream_arguments.update(kwargs)
            self.valid_folders = [1]
            self.current_folder_index = 0
            self.current_clip_start_time = kwargs["start_unix_time"]
            self.calls = 0

        def is_stream_over(self):
            """Report completion after the single clip has been retrieved."""
            return self.calls > 0

        def get_next_clip(self):
            """Create a fake audio clip and return its local HLS timestamp."""
            self.calls += 1
            clip_path = Path("wav_dir/clip.wav")
            clip_path.write_bytes(b"audio")
            return str(clip_path), "2026_11_16_23_00_00", None

    class FakeModel:
        def detect_srkw_from_file(self, clip_path, _model_config):
            """Verify the clip exists and return a positive detection."""
            assert Path(clip_path).exists()
            return SimpleNamespace(
                global_prediction=1,
                global_confidence=0.9,
                local_predictions=[1],
                segment_predictions=[object()],
                print_summary=lambda **_kwargs: None,
            )

    def fake_write_spectrogram(clip_path):
        """Create a placeholder spectrogram alongside the audio clip."""
        image_path = Path(clip_path).with_suffix(".png")
        image_path.write_bytes(b"image")
        return str(image_path)

    def fake_upload(
        _clip_path, _image_path, _result, timestamp, *_args
    ):
        """Capture the uploaded timestamp and return placeholder identifiers."""
        uploaded["timestamp"] = timestamp
        return ("clip.wav", "clip.png", "metadata-id")

    monkeypatch.setattr(
        orchestrator, "DateRangeHLSStream", FakeDateRangeHLSStream
    )
    monkeypatch.setattr(
        orchestrator.spectrogram_visualizer,
        "write_spectrogram",
        fake_write_spectrogram,
    )
    monkeypatch.setattr(orchestrator, "upload_detection_to_azure", fake_upload)

    orchestrator.run_loop(
        "https://s3-us-west-2.amazonaws.com/audio-orcasound-net/rpi_test",
        FakeModel(),
        object(),
        {
            "hls_stream_type": "DateRangeHLS",
            "hls_start_time_pst": "2026-11-16 23:00",
            "hls_end_time_pst": "2026-11-16 23:01",
            "hls_hydrophone_id": "rpi_test",
            "inference_segment_size": 60,
            "delete_local_wavs": True,
            "upload_to_azure": True,
        },
        None,
        None,
        logging.getLogger("test"),
        "model-id",
        max_segments=1,
    )

    assert stream_arguments["stream_base"].endswith("/rpi_test")
    assert stream_arguments["polling_interval"] == 60
    assert stream_arguments["overwrite_output"] is True
    expected_timestamp = datetime.strptime(
        "2026_11_16_23_00_00", "%Y_%m_%d_%H_%M_%S"
    ).astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    assert uploaded["timestamp"] == expected_timestamp


@pytest.mark.parametrize("stream_type", ["DateRangeHLS", "LiveHLS"])
@pytest.mark.parametrize(
    "failure",
    [
        RuntimeError("download failed"),
        URLError("network failed"),
        HTTPError("https://example.test/live.m3u8", 503, "unavailable", None, None),
        ffmpeg.Error("ffmpeg", b"", b"conversion failed"),
    ],
)
def test_run_loop_skips_failed_download_and_processes_later_clip(
    monkeypatch, tmp_path, caplog, stream_type, failure
):
    """Verify failed clip and playlist requests cannot stall either inference loop."""
    monkeypatch.chdir(tmp_path)
    calls = []
    windows = []
    processed = []

    class FakeDateRangeHLSStream:
        def __init__(self, **kwargs):
            """Initialize the requested stream window."""
            self.current_clip_start_time = kwargs["start_unix_time"]
            self.end = kwargs["end_unix_time"]
            windows.append((self.current_clip_start_time, self.end))
            self.valid_folders = [1]
            self.current_folder_index = 0

        def is_stream_over(self):
            """Stop when the requested window has been consumed."""
            return self.current_clip_start_time >= self.end

        def get_next_clip(self):
            """Fail once, then return a clip from the next interval."""
            calls.append(self.current_clip_start_time)
            assert len(calls) <= 3
            if len(calls) == 1:
                raise failure
            self.current_clip_start_time += 60
            return "clip.wav", "2026_11_16_23_00_00", None

    monkeypatch.setattr(orchestrator, "DateRangeHLSStream", FakeDateRangeHLSStream)
    monkeypatch.setattr(
        orchestrator, "_process_clip", lambda *args: processed.append(args[0])
    )
    monkeypatch.setattr(orchestrator.time, "sleep", lambda _: None)
    config = {
        "hls_stream_type": stream_type,
        "hls_start_time_pst": "2026-11-16 23:00",
        "hls_end_time_pst": "2026-11-16 23:02",
        "hls_hydrophone_id": "rpi_test",
        "inference_segment_size": 60,
    }

    with caplog.at_level(logging.ERROR):
        orchestrator.run_loop(
            "https://s3-us-west-2.amazonaws.com/audio-orcasound-net/rpi_test",
            None, None, config, None, None, logging.getLogger("test"), "model-id",
            max_live_iterations=2, max_segments=1,
        )

    assert len(calls) == 2
    if stream_type == "DateRangeHLS":
        assert calls[1] == calls[0] + 60
        assert windows[0][1] - windows[0][0] == 120
    else:
        assert len(windows) == 2
        assert all(end - start == 60 for start, end in windows)
    assert processed == ["clip.wav"]
    assert "Failed to retrieve HLS clip" in caplog.text


@pytest.mark.parametrize(
    ("max_segments", "expected_count"), [(0, 0), (1, 1), (2, 2), (None, 3)]
)
def test_date_range_respects_clip_limit(monkeypatch, tmp_path, max_segments, expected_count):
    """Verify the clip limit is checked before each date-range fetch."""
    monkeypatch.chdir(tmp_path)
    calls = []
    processed = []

    class FakeDateRangeHLSStream:
        def __init__(self, **kwargs):
            """Initialize a three-clip date range."""
            self.current_clip_start_time = kwargs["start_unix_time"]
            self.end = kwargs["end_unix_time"]
            self.valid_folders = [1]
            self.current_folder_index = 0

        def is_stream_over(self):
            """Report whether the date range has been consumed."""
            return self.current_clip_start_time >= self.end

        def get_next_clip(self):
            """Return the next interval and record the fetch."""
            calls.append(self.current_clip_start_time)
            self.current_clip_start_time += 60
            return "clip.wav", "2026_11_16_23_00_00", None

    monkeypatch.setattr(orchestrator, "DateRangeHLSStream", FakeDateRangeHLSStream)
    monkeypatch.setattr(
        orchestrator, "_process_clip", lambda *args: processed.append(args[0])
    )
    config = {
        "hls_stream_type": "DateRangeHLS",
        "hls_start_time_pst": "2026-11-16 23:00",
        "hls_end_time_pst": "2026-11-16 23:03",
        "hls_hydrophone_id": "rpi_test",
        "inference_segment_size": 60,
    }

    orchestrator.run_loop(
        "https://s3-us-west-2.amazonaws.com/audio-orcasound-net/rpi_test",
        None, None, config, None, None, logging.getLogger("test"), "model-id",
        max_segments=max_segments,
    )

    assert len(calls) == expected_count
    assert processed == ["clip.wav"] * expected_count


def test_playlist_http_failure_advances_stream(monkeypatch, tmp_path):
    """Verify failures from m3u8's HTTP client reach clip recovery."""
    from urllib.request import OpenerDirector

    from orca_hls_utils import s3_utils

    def fail_request(self, request, timeout=None):
        """Simulate an unavailable playlist."""
        raise HTTPError(request, 503, "unavailable", None, None)

    monkeypatch.setattr(OpenerDirector, "open", fail_request)
    monkeypatch.setattr(s3_utils, "get_all_folders", lambda *args, **kwargs: ["100"])
    stream = orchestrator.DateRangeHLSStream(
        stream_base="https://s3-us-west-2.amazonaws.com/audio-orcasound-net/rpi_test",
        polling_interval=60,
        start_unix_time=100,
        end_unix_time=220,
        wav_dir=str(tmp_path),
    )

    assert orchestrator._get_next_clip(stream, 60, logging.getLogger("test")) == (
        None, None, None
    )
    assert stream.current_clip_start_time == 160


def test_get_next_clip_propagates_unrelated_errors():
    """Verify unexpected errors are not hidden by download recovery."""
    class BrokenStream:
        current_clip_start_time = 100

        def get_next_clip(self):
            """Raise an unrelated error."""
            raise ValueError("unexpected failure")

    stream = BrokenStream()
    with pytest.raises(ValueError, match="unexpected failure"):
        orchestrator._get_next_clip(stream, 60, logging.getLogger("test"))
    assert stream.current_clip_start_time == 100
