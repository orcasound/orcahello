import asyncio
import base64
import io
import sys
import threading
from types import ModuleType
from pathlib import Path
from types import SimpleNamespace

import numpy as np
import pytest
import soundfile as sf
import torch
from fastapi.testclient import TestClient

import api.app as api_module
import model.prediction as prediction_module
from api.engine import (
    ONNXInferenceEngine,
    load_inference_config,
    load_inference_engine,
    load_onnx_engine,
)
from api.app import Settings, _run_bounded, create_app
from api.guards import (
    InferenceBusy,
    RequestGuardMiddleware,
    SlidingWindowRateLimiter,
    client_ip_for_scope,
)


class FakeEngine:
    engine = "mock"

    def __init__(self, error: Exception | None = None):
        self.error = error
        self.paths: list[str] = []

    def analyze(self, file_path: str):
        self.paths.append(file_path)
        if self.error is not None:
            raise self.error
        segments = [
            SimpleNamespace(start_time_s=0.0, duration_s=3.0, confidence=0.2),
            SimpleNamespace(start_time_s=2.0, duration_s=3.0, confidence=0.91),
            SimpleNamespace(start_time_s=4.0, duration_s=3.0, confidence=0.4),
        ]
        return SimpleNamespace(
            local_predictions=[0, 1, 0],
            segment_predictions=segments,
            global_prediction=1,
            global_confidence=0.84,
        )


def audio_bytes(fmt: str = "WAV", duration: float = 4.0) -> bytes:
    buffer = io.BytesIO()
    waveform = np.zeros(int(8000 * duration), dtype=np.float32)
    sf.write(buffer, waveform, 8000, format=fmt, subtype="PCM_16")
    return buffer.getvalue()


def make_settings(**overrides) -> Settings:
    values = {
        "max_upload_bytes": 1024 * 1024,
        "max_request_body_bytes": 2 * 1024 * 1024,
        "min_duration_seconds": 3.0,
        "max_duration_seconds": 10.0,
        "rate_limit_per_minute": 100,
        "max_concurrent_inferences": 1,
    }
    values.update(overrides)
    return Settings(**values)


def client_for(settings: Settings | None = None, engine: FakeEngine | None = None):
    if engine is None:
        engine = FakeEngine()
    app = create_app(settings or make_settings(), engine_loader=lambda _: engine)
    return TestClient(app), engine


def analyze(
    client: TestClient,
    content: bytes,
    filename: str = "sample.wav",
    *,
    data: dict | None = None,
    headers: dict | None = None,
):
    return client.post(
        "/analyze",
        files={"file": (filename, content, "application/octet-stream")},
        data=data,
        headers=headers,
    )


@pytest.mark.parametrize(
    ("fmt", "filename"), [("WAV", "sample.wav"), ("FLAC", "sample.flac")]
)
def test_analyze_accepts_wav_and_flac(fmt, filename):
    client, engine = client_for()
    with client:
        response = analyze(client, audio_bytes(fmt), filename)

    assert response.status_code == 200
    payload = response.json()
    assert payload["duration_seconds"] == pytest.approx(4.0)
    assert payload["has_detection"] is True
    assert payload["global_confidence"] == pytest.approx(0.84)
    assert payload["detections"] == [
        {
            "timestamp_seconds": 2.0,
            "duration_seconds": 3.0,
            "confidence": 0.91,
        }
    ]
    assert payload["spectrogram_png_base64"] is None
    assert len(engine.paths) == 1
    assert not Path(engine.paths[0]).exists()


def test_health_reports_ready_engine_once_loaded():
    loads = []

    def load_engine(_):
        loads.append(1)
        return FakeEngine()

    app = create_app(make_settings(), engine_loader=load_engine)
    with TestClient(app) as client:
        assert client.get("/health").json() == {
            "status": "ok",
            "model_ready": True,
            "engine": "mock",
        }
        assert client.get("/health").status_code == 200
    assert len(loads) == 1


def test_include_spectrogram_returns_base64_png(monkeypatch):
    monkeypatch.setattr(api_module, "_render_spectrogram_png", lambda _: b"png-bytes")
    client, _ = client_for()
    with client:
        response = analyze(
            client,
            audio_bytes(),
            data={"include_spectrogram": "true"},
        )

    assert response.status_code == 200
    assert (
        response.json()["spectrogram_png_base64"]
        == base64.b64encode(b"png-bytes").decode()
    )


def test_rejects_audio_over_duration_limit():
    client, engine = client_for(make_settings(max_duration_seconds=3.5))
    with client:
        response = analyze(client, audio_bytes(duration=4.0))

    assert response.status_code == 413
    assert response.json() == {
        "detail": "audio duration exceeds the configured maximum"
    }
    assert engine.paths == []


def test_rejects_audio_shorter_than_model_window():
    client, engine = client_for(make_settings(min_duration_seconds=3.0))
    with client:
        response = analyze(client, audio_bytes(duration=2.5))

    assert response.status_code == 422
    assert response.json() == {"detail": "audio is shorter than the minimum duration"}
    assert engine.paths == []


def test_rejects_file_over_upload_limit():
    client, engine = client_for(make_settings(max_upload_bytes=64))
    with client:
        response = analyze(client, audio_bytes())

    assert response.status_code == 413
    assert response.json() == {"detail": "file too large"}
    assert engine.paths == []


def test_rejects_request_body_before_multipart_parsing():
    client, engine = client_for(make_settings(max_request_body_bytes=64))
    with client:
        response = analyze(client, audio_bytes())

    assert response.status_code == 413
    assert response.json() == {"detail": "request body too large"}
    assert engine.paths == []


def test_authentication_runs_before_request_body_parsing():
    client, engine = client_for(
        make_settings(api_key="secret", max_request_body_bytes=64)
    )
    with client:
        response = analyze(client, audio_bytes())

    assert response.status_code == 401
    assert response.json() == {"detail": "invalid or missing API key"}
    assert engine.paths == []


def test_streaming_body_guard_enforces_limit_without_content_length():
    messages = [{"type": "http.request", "body": b"x" * 11, "more_body": False}]
    sent = []

    async def receive():
        return messages.pop(0) if messages else {"type": "http.disconnect"}

    async def send(message):
        sent.append(message)

    async def consume_body(scope, limited_receive, send):
        await limited_receive()

    middleware = RequestGuardMiddleware(
        consume_body, SlidingWindowRateLimiter(10), max_body_bytes=10
    )
    scope = {
        "type": "http",
        "method": "POST",
        "path": "/analyze",
        "client": ("198.51.100.10", 1234),
        "headers": [],
    }
    asyncio.run(middleware(scope, receive, send))

    response_start = next(
        message for message in sent if message["type"] == "http.response.start"
    )
    response_body = next(
        message for message in sent if message["type"] == "http.response.body"
    )
    assert response_start["status"] == 413
    assert response_body["body"] == b'{"detail":"request body too large"}'


def test_rate_limit_returns_429_and_retry_after():
    client, _ = client_for(make_settings(rate_limit_per_minute=1))
    with client:
        first = analyze(client, audio_bytes())
        second = analyze(client, audio_bytes())

    assert first.status_code == 200
    assert second.status_code == 429
    assert second.json() == {"detail": "rate limit exceeded"}
    assert int(second.headers["retry-after"]) >= 1


def test_api_key_is_optional_but_enforced_when_configured():
    client, _ = client_for(make_settings(api_key="secret"))
    with client:
        missing = analyze(client, audio_bytes())
        wrong = analyze(client, audio_bytes(), headers={"X-API-Key": "wrong"})
        valid = analyze(client, audio_bytes(), headers={"X-API-Key": "secret"})

    assert missing.status_code == 401
    assert wrong.status_code == 401
    assert valid.status_code == 200
    assert missing.json() == {"detail": "invalid or missing API key"}


def test_unconfigured_api_key_allows_local_requests():
    client, _ = client_for(make_settings(api_key=None))
    with client:
        response = analyze(client, audio_bytes())
    assert response.status_code == 200


def test_missing_file_uses_standard_validation_error_schema():
    client, _ = client_for()
    with client:
        response = client.post("/analyze")

    assert response.status_code == 422
    assert isinstance(response.json()["detail"], list)


@pytest.mark.parametrize(
    ("filename", "content", "status"),
    [
        ("sample.mp3", audio_bytes(), 415),
        ("sample.wav", b"not audio", 422),
        ("sample.flac", audio_bytes("WAV"), 415),
    ],
    ids=["unsupported-extension", "invalid-audio", "mismatched-format"],
)
def test_invalid_format_errors(filename, content, status):
    client, engine = client_for()
    with client:
        response = analyze(client, content, filename)

    assert response.status_code == status
    assert engine.paths == []


def test_invalid_audio_removes_temporary_upload(monkeypatch):
    inspected_paths = []

    def reject_audio(path, *_):
        inspected_paths.append(path)
        raise api_module.InvalidAudio

    monkeypatch.setattr(api_module, "inspect_audio", reject_audio)
    client, _ = client_for()
    with client:
        response = analyze(client, audio_bytes())

    assert response.status_code == 422
    assert len(inspected_paths) == 1
    assert not Path(inspected_paths[0]).exists()


def test_inference_error_is_sanitized_and_tempfile_is_removed():
    engine = FakeEngine(RuntimeError("private implementation detail"))
    client, _ = client_for(engine=engine)
    with client:
        response = analyze(client, audio_bytes())

    assert response.status_code == 500
    assert response.json() == {"detail": "inference failed"}
    assert "private implementation detail" not in response.text
    assert len(engine.paths) == 1
    assert not Path(engine.paths[0]).exists()


def test_sliding_window_is_per_ip_and_expires():
    now = [0.0]
    limiter = SlidingWindowRateLimiter(1, window_seconds=60, clock=lambda: now[0])

    async def check():
        first = await limiter.check("ip-a")
        blocked = await limiter.check("ip-a")
        other_ip = await limiter.check("ip-b")
        now[0] = 61.0
        expired = await limiter.check("ip-a")
        return first, blocked, other_ip, expired

    first, blocked, other_ip, expired = asyncio.run(check())
    assert first is None
    assert blocked == pytest.approx(60.0)
    assert other_ip is None
    assert expired is None


def test_forwarded_ip_is_used_only_from_trusted_proxy():
    headers = [(b"x-forwarded-for", b"198.51.100.99, 203.0.113.25")]
    trusted_proxy_scope = {"client": ("10.0.0.8", 1234), "headers": headers}
    untrusted_peer_scope = {"client": ("192.0.2.8", 1234), "headers": headers}

    assert (
        client_ip_for_scope(trusted_proxy_scope, ("10.0.0.0/8", "203.0.113.0/24"))
        == "198.51.100.99"
    )
    assert client_ip_for_scope(untrusted_peer_scope, ("10.0.0.0/8",)) == "192.0.2.8"


def test_semaphore_rejects_concurrent_work_and_releases_after_completion():
    started = threading.Event()
    release = threading.Event()

    def blocking_work():
        started.set()
        assert release.wait(timeout=5)
        return "complete"

    async def run():
        semaphore = asyncio.Semaphore(1)
        first = asyncio.create_task(_run_bounded(semaphore, blocking_work))
        assert await asyncio.to_thread(started.wait, 5)
        with pytest.raises(InferenceBusy):
            await _run_bounded(semaphore, blocking_work)
        release.set()
        assert await first == "complete"
        assert not semaphore.locked()

    asyncio.run(run())


def test_cancelled_inference_waits_for_worker_before_releasing_semaphore():
    started = threading.Event()
    release = threading.Event()

    def blocking_work():
        started.set()
        assert release.wait(timeout=5)

    async def run():
        semaphore = asyncio.Semaphore(1)
        task = asyncio.create_task(_run_bounded(semaphore, blocking_work))
        assert await asyncio.to_thread(started.wait, 5)
        task.cancel()
        await asyncio.sleep(0)
        assert not task.done()
        release.set()
        with pytest.raises(asyncio.CancelledError):
            await task
        assert not semaphore.locked()

    asyncio.run(run())


def test_inference_error_releases_semaphore():
    async def run():
        semaphore = asyncio.Semaphore(1)

        def fail():
            raise RuntimeError("inference failed")

        with pytest.raises(RuntimeError, match="inference failed"):
            await _run_bounded(semaphore, fail)
        assert not semaphore.locked()

    asyncio.run(run())


def test_auto_backend_falls_back_to_pytorch_when_onnx_is_unavailable(caplog):
    expected_engine = FakeEngine()
    settings = make_settings(model_backend="auto", onnx_model_path=None)

    selected = load_inference_engine(
        settings,
        onnx_loader=lambda _: pytest.fail("ONNX loader should not run without a path"),
        pytorch_loader=lambda _: expected_engine,
    )

    assert selected is expected_engine
    assert "falling back to PyTorch" in caplog.text


def test_auto_backend_falls_back_when_onnx_artifact_cannot_load(tmp_path, caplog):
    expected_engine = FakeEngine()
    settings = make_settings(
        model_backend="auto", onnx_model_path=str(tmp_path / "missing.onnx")
    )

    def fail_onnx(_):
        raise FileNotFoundError("missing ONNX test artifact")

    selected = load_inference_engine(
        settings,
        onnx_loader=fail_onnx,
        pytorch_loader=lambda _: expected_engine,
    )

    assert selected is expected_engine
    assert "missing ONNX test artifact" in caplog.text


def test_explicit_onnx_backend_fails_when_artifact_path_is_missing():
    settings = make_settings(model_backend="onnx", onnx_model_path=None)

    with pytest.raises(RuntimeError, match="requires ONNX_MODEL_PATH"):
        load_inference_engine(settings)


def test_explicit_onnx_backend_does_not_fallback_when_engine_initialization_fails(
    tmp_path,
):
    model_path = tmp_path / "detector.onnx"
    model_path.write_bytes(b"test artifact")

    def fail_onnx(_):
        raise ImportError("runtime missing")

    with pytest.raises(ImportError, match="runtime missing"):
        load_inference_engine(
            make_settings(model_backend="onnx", onnx_model_path=str(model_path)),
            onnx_loader=fail_onnx,
            pytorch_loader=lambda _: pytest.fail("explicit ONNX must not fall back"),
        )


def test_auto_backend_discovers_configured_onnx_artifact(tmp_path):
    model_path = tmp_path / "detector.onnx"
    model_path.write_bytes(b"test artifact")
    expected_engine = SimpleNamespace(engine="onnx")
    onnx_calls = []

    selected = load_inference_engine(
        make_settings(model_backend="auto", onnx_model_path=str(model_path)),
        onnx_loader=lambda settings: (
            onnx_calls.append(settings.onnx_model_path) or expected_engine
        ),
        pytorch_loader=lambda _: pytest.fail("PyTorch loader should not run"),
    )

    assert selected is expected_engine
    assert onnx_calls == [str(model_path)]


def test_onnx_loader_uses_cpu_execution_provider(tmp_path, monkeypatch):
    model_path = tmp_path / "detector.onnx"
    model_path.write_bytes(b"test artifact")
    observed = {}
    session = SimpleNamespace(get_inputs=lambda: [SimpleNamespace(name="audio")])

    def create_session(path, providers):
        observed["path"] = path
        observed["providers"] = providers
        return session

    onnxruntime = ModuleType("onnxruntime")
    onnxruntime.InferenceSession = create_session
    monkeypatch.setitem(sys.modules, "onnxruntime", onnxruntime)

    engine = load_onnx_engine(
        make_settings(onnx_model_path=str(model_path), onnx_output_type="logits")
    )

    assert isinstance(engine, ONNXInferenceEngine)
    assert observed == {
        "path": str(model_path),
        "providers": ["CPUExecutionProvider"],
    }


@pytest.mark.parametrize("output_kind", ["logits", "probabilities"])
def test_onnx_engine_uses_numpy_batches_and_shared_detection_logic(
    monkeypatch, output_kind
):
    class FakePreprocessor:
        def __init__(self, config):
            self.config = config

        def process_segments(self, _):
            for index in range(3):
                yield (
                    torch.full((1, 2, 3), index, dtype=torch.float32),
                    index * 1.0,
                    2.0,
                )

    monkeypatch.setattr(prediction_module, "_audio_preprocessor", FakePreprocessor)
    logits = np.asarray([[0.0, 2.0], [2.0, 0.0], [0.0, 1.0]], dtype=np.float32)
    probabilities = torch.softmax(torch.from_numpy(logits), dim=1).numpy()
    session_outputs = logits if output_kind == "logits" else probabilities
    calls = []

    class FakeSession:
        def get_inputs(self):
            return [SimpleNamespace(name="mel_input")]

        def run(self, output_names, inputs):
            assert output_names is None
            batch = inputs["mel_input"]
            calls.append(batch)
            offset = sum(len(previous) for previous in calls[:-1])
            return [session_outputs[offset : offset + len(batch)]]

    config = load_inference_config(make_settings(model_config_path=None))
    config.inference.max_batch_size = 2
    engine = ONNXInferenceEngine(FakeSession(), config, output_type=output_kind)
    onnx_result = engine.analyze("fake.wav")

    expected_offset = [0]

    def torch_predict(batch):
        start = expected_offset[0]
        expected_offset[0] += len(batch)
        return torch.from_numpy(logits[start : start + len(batch)])

    torch_result = prediction_module.run_file_inference(
        "fake.wav", config, torch_predict
    )

    assert [batch.shape for batch in calls] == [(2, 1, 2, 3), (1, 1, 2, 3)]
    assert all(
        batch.dtype == np.float32 and batch.flags.c_contiguous for batch in calls
    )
    assert onnx_result.local_predictions == torch_result.local_predictions
    assert onnx_result.global_prediction == torch_result.global_prediction
    assert onnx_result.global_confidence == pytest.approx(
        torch_result.global_confidence
    )
    assert onnx_result.local_confidences == pytest.approx(
        torch_result.local_confidences
    )
    assert [
        (segment.start_time_s, segment.duration_s)
        for segment in onnx_result.segment_predictions
    ] == [(0.0, 2.0), (1.0, 2.0), (2.0, 2.0)]


def test_settings_discovers_backend_environment(monkeypatch):
    monkeypatch.setenv("MODEL_BACKEND", "onnx")
    monkeypatch.setenv("ONNX_MODEL_PATH", "models/detector.onnx")

    settings = Settings.from_env()

    assert settings.model_backend == "onnx"
    assert settings.onnx_model_path == "models/detector.onnx"
