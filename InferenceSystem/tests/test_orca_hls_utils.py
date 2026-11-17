import logging
import sys
from datetime import datetime, timezone
from pathlib import Path
from types import SimpleNamespace

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
