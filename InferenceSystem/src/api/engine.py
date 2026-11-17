"""Inference engine adapters and backend selection for the HTTP API."""

import logging
from abc import ABC, abstractmethod
from pathlib import Path
from typing import Any, Callable

import numpy as np

from model.prediction import run_file_inference
from model.types import DetectionResult, DetectorInferenceConfig

logger = logging.getLogger(__name__)


class BaseInferenceEngine(ABC):
    """Common API contract implemented by all inference backends."""

    engine = "unknown"

    @abstractmethod
    def analyze(self, file_path: str) -> DetectionResult:
        """Analyze one validated audio file and return a DetectionResult."""


class PyTorchInferenceEngine(BaseInferenceEngine):
    engine = "pytorch"

    def __init__(self, model: Any, config: DetectorInferenceConfig):
        self.model = model
        self.config = config

    def analyze(self, file_path: str) -> DetectionResult:
        return self.model.detect_srkw_from_file(file_path, self.config)


class ONNXInferenceEngine(BaseInferenceEngine):
    engine = "onnx"

    def __init__(
        self,
        session: Any,
        config: DetectorInferenceConfig,
        output_type: str = "logits",
    ):
        self.session = session
        self.config = config
        inputs = session.get_inputs()
        if not inputs:
            raise ValueError("ONNX model has no inputs")
        self.input_name = inputs[0].name
        if output_type not in {"logits", "probabilities"}:
            raise ValueError("ONNX output_type must be logits or probabilities")
        self.output_type = output_type

    def _predict_batch(self, batch):
        batch_array = np.ascontiguousarray(
            batch.detach().cpu().numpy(), dtype=np.float32
        )
        outputs = self.session.run(None, {self.input_name: batch_array})
        if not outputs:
            raise ValueError("ONNX model returned no outputs")
        return outputs[0]

    def analyze(self, file_path: str) -> DetectionResult:
        return run_file_inference(
            file_path, self.config, self._predict_batch, self.output_type
        )


def load_inference_config(settings: Any) -> DetectorInferenceConfig:
    inference_root = Path(__file__).resolve().parents[2]
    config_path = (
        Path(settings.model_config_path)
        if settings.model_config_path
        else inference_root / "model" / "config.yaml"
    )
    config = DetectorInferenceConfig.from_yaml(str(config_path))
    config.model.device = "cpu"
    config.model.precision = "float32"
    return config


def load_pytorch_engine(settings: Any) -> PyTorchInferenceEngine:
    from model.inference import OrcaHelloSRKWDetectorV1

    config = load_inference_config(settings)
    model = OrcaHelloSRKWDetectorV1.from_pretrained(
        settings.model_repo_id, config=config.as_dict()
    )
    model.eval()
    return PyTorchInferenceEngine(model, config)


def load_onnx_engine(settings: Any) -> ONNXInferenceEngine:
    model_path = Path(settings.onnx_model_path or "")
    if not model_path.is_file():
        raise FileNotFoundError(f"ONNX model artifact not found: {model_path}")

    import onnxruntime as ort

    session = ort.InferenceSession(str(model_path), providers=["CPUExecutionProvider"])
    return ONNXInferenceEngine(
        session, load_inference_config(settings), output_type=settings.onnx_output_type
    )


def load_inference_engine(
    settings: Any,
    *,
    onnx_loader: Callable[[Any], BaseInferenceEngine] | None = None,
    pytorch_loader: Callable[[Any], BaseInferenceEngine] | None = None,
) -> BaseInferenceEngine:
    """Select the configured backend, preferring ONNX in automatic mode."""
    backend = settings.model_backend.lower()
    if onnx_loader is None:
        onnx_loader = load_onnx_engine
    if pytorch_loader is None:
        pytorch_loader = load_pytorch_engine

    if backend == "pytorch":
        return pytorch_loader(settings)

    if backend == "onnx":
        if not settings.onnx_model_path:
            raise RuntimeError("MODEL_BACKEND=onnx requires ONNX_MODEL_PATH")
        return onnx_loader(settings)

    if backend != "auto":
        raise ValueError(f"unsupported MODEL_BACKEND: {settings.model_backend}")

    if settings.onnx_model_path:
        try:
            return onnx_loader(settings)
        except Exception as exc:
            logger.warning("ONNX backend unavailable; falling back to PyTorch: %s", exc)
    else:
        logger.warning(
            "MODEL_BACKEND=auto but ONNX_MODEL_PATH is unset; falling back to PyTorch"
        )
    return pytorch_loader(settings)
