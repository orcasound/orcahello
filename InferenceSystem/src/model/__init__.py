"""
model - Orca Detection Model without fastai dependencies

This module provides a fastai-free implementation of the OrcaHello SRKW detection model.
It uses pure PyTorch and torchaudio for audio processing and model inference.

Usage:
    from model import OrcaHelloSRKWDetectorV1

    model = OrcaHelloSRKWDetectorV1.from_pretrained("orcasound/orcahello-srkw-detector-v1")
    result = model.detect_srkw_from_file("path/to/audio.wav")
"""

__version__ = "1.1.0"

from .inference import OrcaHelloSRKWDetectorV1
from .types import (
    DetectorInferenceConfig,
    DetectionResult,
)

__all__ = [
    "OrcaHelloSRKWDetectorV1",
    "DetectorInferenceConfig",
    "DetectionResult",
]
