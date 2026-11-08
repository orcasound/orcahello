import asyncio
import base64
import logging
import os
from contextlib import asynccontextmanager
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

from fastapi import (
    FastAPI,
    File,
    Form,
    HTTPException,
    Request,
    UploadFile,
)

from api.engine import BaseInferenceEngine, load_inference_engine
from api.guards import (
    AudioTooLong,
    AudioTooShort,
    InferenceBusy,
    InvalidAudio,
    PayloadTooLarge,
    RequestGuardMiddleware,
    SlidingWindowRateLimiter,
    UnsupportedAudioFormat,
    allowed_audio_suffix,
    copy_upload_to_temp,
    inspect_audio,
    run_in_thread_until_complete,
)
from api.schemas import (
    AnalyzeResponse,
    DetectionResponse,
    ErrorResponse,
    HealthResponse,
)
from model.types import DetectionResult

logger = logging.getLogger(__name__)


@dataclass(frozen=True)
class Settings:
    api_key: str | None = None
    max_upload_bytes: int = 20 * 1024 * 1024
    max_request_body_bytes: int = 20 * 1024 * 1024 + 64 * 1024
    min_duration_seconds: float = 3.0
    max_duration_seconds: float = 60.0
    rate_limit_per_minute: int = 5
    max_concurrent_inferences: int = 1
    trusted_proxy_cidrs: tuple[str, ...] = ()
    model_backend: str = "auto"
    onnx_model_path: str | None = None
    onnx_output_type: str = "logits"
    model_repo_id: str = "orcasound/orcahello-srkw-detector-v1"
    model_config_path: str | None = None

    def __post_init__(self):
        if self.max_upload_bytes < 1 or self.max_request_body_bytes < 1:
            raise ValueError("upload and request-body limits must be positive")
        if self.rate_limit_per_minute < 1 or self.max_concurrent_inferences < 1:
            raise ValueError("rate and concurrency limits must be positive")
        if self.model_backend.lower() not in {"auto", "onnx", "pytorch"}:
            raise ValueError("model_backend must be auto, onnx, or pytorch")
        if self.onnx_output_type not in {"logits", "probabilities"}:
            raise ValueError("onnx_output_type must be logits or probabilities")
        if (
            self.min_duration_seconds < 0
            or self.max_duration_seconds <= 0
            or self.min_duration_seconds > self.max_duration_seconds
        ):
            raise ValueError("audio duration limits are invalid")

    @classmethod
    def from_env(cls) -> "Settings":
        max_upload_bytes = int(os.getenv("MAX_UPLOAD_BYTES", str(20 * 1024 * 1024)))
        return cls(
            api_key=os.getenv("ORCAHELLO_API_KEY") or None,
            max_upload_bytes=max_upload_bytes,
            max_request_body_bytes=int(
                os.getenv("MAX_REQUEST_BODY_BYTES", str(max_upload_bytes + 64 * 1024))
            ),
            min_duration_seconds=float(os.getenv("MIN_AUDIO_DURATION_SECONDS", "3")),
            max_duration_seconds=float(os.getenv("MAX_AUDIO_DURATION_SECONDS", "60")),
            rate_limit_per_minute=int(os.getenv("RATE_LIMIT_PER_MINUTE", "5")),
            max_concurrent_inferences=int(os.getenv("MAX_CONCURRENT_INFERENCES", "1")),
            trusted_proxy_cidrs=tuple(
                cidr.strip()
                for cidr in os.getenv("TRUSTED_PROXY_CIDRS", "").split(",")
                if cidr.strip()
            ),
            model_backend=os.getenv("MODEL_BACKEND", "auto").lower(),
            onnx_model_path=os.getenv("ONNX_MODEL_PATH") or None,
            onnx_output_type=os.getenv("ONNX_OUTPUT_TYPE", "logits").lower(),
            model_repo_id=os.getenv(
                "MODEL_HF_REPO_ID", "orcasound/orcahello-srkw-detector-v1"
            ),
            model_config_path=os.getenv("MODEL_CONFIG_PATH") or None,
        )


def _render_spectrogram_png(file_path: str) -> bytes:
    from spectrogram_visualizer import write_spectrogram

    png_path = Path(write_spectrogram(file_path))
    try:
        return png_path.read_bytes()
    finally:
        png_path.unlink(missing_ok=True)


def _analyze_sync(
    engine: BaseInferenceEngine, file_path: str, include_spectrogram: bool
) -> tuple[DetectionResult, bytes | None]:
    result = engine.analyze(file_path)
    png_bytes = _render_spectrogram_png(file_path) if include_spectrogram else None
    return result, png_bytes


async def _run_bounded(semaphore: asyncio.Semaphore, function, *args):
    if semaphore.locked():
        raise InferenceBusy
    await semaphore.acquire()
    try:
        return await run_in_thread_until_complete(function, *args)
    finally:
        semaphore.release()


def _build_response(
    result: DetectionResult, duration_seconds: float, png_bytes: bytes | None
) -> AnalyzeResponse:
    detections = [
        DetectionResponse(
            timestamp_seconds=segment.start_time_s,
            duration_seconds=segment.duration_s,
            confidence=segment.confidence,
        )
        for prediction, segment in zip(
            result.local_predictions, result.segment_predictions
        )
        if int(prediction) == 1
    ]
    return AnalyzeResponse(
        duration_seconds=duration_seconds,
        has_detection=bool(result.global_prediction),
        global_confidence=float(result.global_confidence),
        detections=detections,
        spectrogram_png_base64=(
            base64.b64encode(png_bytes).decode("ascii") if png_bytes else None
        ),
    )


def create_app(
    settings: Settings | None = None,
    engine_loader: Callable[[Settings], BaseInferenceEngine] | None = None,
) -> FastAPI:
    settings = settings or Settings.from_env()
    if engine_loader is None:
        engine_loader = load_inference_engine
    limiter = SlidingWindowRateLimiter(settings.rate_limit_per_minute)

    @asynccontextmanager
    async def lifespan(app: FastAPI):
        app.state.settings = settings
        app.state.semaphore = asyncio.Semaphore(settings.max_concurrent_inferences)
        app.state.engine = await run_in_thread_until_complete(engine_loader, settings)
        try:
            yield
        finally:
            app.state.engine = None

    app = FastAPI(title="OrcaHello Audio Analysis API", lifespan=lifespan)
    app.add_middleware(
        RequestGuardMiddleware,
        limiter=limiter,
        max_body_bytes=settings.max_request_body_bytes,
        trusted_proxy_cidrs=settings.trusted_proxy_cidrs,
        expected_api_key=settings.api_key,
    )

    @app.get("/health", response_model=HealthResponse)
    async def health(request: Request):
        engine = getattr(request.app.state, "engine", None)
        if engine is None:
            raise HTTPException(status_code=503, detail="model not ready")
        return HealthResponse(
            status="ok",
            model_ready=True,
            engine=engine.engine,
        )

    @app.post(
        "/analyze",
        response_model=AnalyzeResponse,
        responses={
            401: {"model": ErrorResponse},
            413: {"model": ErrorResponse},
            415: {"model": ErrorResponse},
            422: {"model": ErrorResponse},
            429: {"model": ErrorResponse},
            500: {"model": ErrorResponse},
            503: {"model": ErrorResponse},
        },
    )
    async def analyze(
        request: Request,
        file: UploadFile = File(...),
        include_spectrogram: bool = Form(False),
    ):
        temp_path: str | None = None
        try:
            try:
                suffix = allowed_audio_suffix(file.filename)
            except UnsupportedAudioFormat:
                raise HTTPException(
                    status_code=415, detail="only WAV and FLAC are supported"
                )

            try:
                temp_path = await copy_upload_to_temp(
                    file.file, request.app.state.settings.max_upload_bytes, suffix
                )
            except PayloadTooLarge:
                raise HTTPException(status_code=413, detail="file too large")

            try:
                duration_seconds = await run_in_thread_until_complete(
                    inspect_audio,
                    temp_path,
                    suffix,
                    request.app.state.settings.min_duration_seconds,
                    request.app.state.settings.max_duration_seconds,
                )
            except UnsupportedAudioFormat:
                raise HTTPException(
                    status_code=415, detail="audio format does not match file type"
                )
            except AudioTooLong:
                raise HTTPException(
                    status_code=413,
                    detail="audio duration exceeds the configured maximum",
                )
            except AudioTooShort:
                raise HTTPException(
                    status_code=422, detail="audio is shorter than the minimum duration"
                )
            except InvalidAudio:
                raise HTTPException(
                    status_code=422, detail="invalid or unreadable audio file"
                )

            engine = request.app.state.engine
            if engine is None:
                raise HTTPException(status_code=503, detail="model not ready")
            try:
                result, png_bytes = await _run_bounded(
                    request.app.state.semaphore,
                    _analyze_sync,
                    engine,
                    temp_path,
                    include_spectrogram,
                )
            except InferenceBusy:
                raise HTTPException(
                    status_code=503, detail="inference capacity is busy"
                )
            except Exception:
                logger.exception("Audio inference failed")
                raise HTTPException(status_code=500, detail="inference failed")

            return _build_response(result, duration_seconds, png_bytes)
        finally:
            if temp_path:
                await run_in_thread_until_complete(
                    Path(temp_path).unlink, missing_ok=True
                )
            await file.close()

    return app


app = create_app()
