from typing import Any

from pydantic import BaseModel, Field


class DetectionResponse(BaseModel):
    timestamp_seconds: float = Field(ge=0, allow_inf_nan=False)
    duration_seconds: float = Field(gt=0, allow_inf_nan=False)
    confidence: float = Field(ge=0, le=1, allow_inf_nan=False)


class AnalyzeResponse(BaseModel):
    duration_seconds: float = Field(ge=0, allow_inf_nan=False)
    has_detection: bool
    global_confidence: float = Field(ge=0, le=1, allow_inf_nan=False)
    detections: list[DetectionResponse]
    spectrogram_png_base64: str | None = None


class HealthResponse(BaseModel):
    status: str
    model_ready: bool
    engine: str


class ErrorResponse(BaseModel):
    detail: str | list[dict[str, Any]]
