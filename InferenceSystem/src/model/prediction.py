"""Backend-independent audio preprocessing and detection aggregation."""

import time
from typing import Any, Callable

import numpy as np
import torch

from .types import (
    DetectionMetadata,
    DetectionResult,
    DetectorInferenceConfig,
    GlobalPredictionConfig,
    SegmentPrediction,
)


def _audio_preprocessor(config):
    from .audio_frontend import AudioPreprocessor

    return AudioPreprocessor(config)


def aggregate_predictions(
    segment_preds: list[SegmentPrediction], config: GlobalPredictionConfig
) -> tuple[list[int], int, float]:
    """Aggregate segment probabilities into local and file-level detections."""
    if not segment_preds:
        return [], 0, 0.0

    local_predictions = [
        1 if segment.confidence > config.pred_local_threshold else 0
        for segment in segment_preds
    ]

    if config.aggregation_strategy == "mean_thresholded":
        positive_confidences = [
            segment.confidence
            for segment, prediction in zip(segment_preds, local_predictions)
            if prediction == 1
        ]
        global_confidence = (
            sum(positive_confidences) / len(positive_confidences)
            if positive_confidences
            else 0.0
        )
    elif config.aggregation_strategy == "mean_top_k":
        top_k = max(1, min(config.mean_top_k, len(segment_preds)))
        top_segments = sorted(
            segment_preds, key=lambda segment: segment.confidence, reverse=True
        )[:top_k]
        global_confidence = sum(segment.confidence for segment in top_segments) / len(
            top_segments
        )
    else:
        raise ValueError(f"Unknown aggregation_strategy: {config.aggregation_strategy}")

    global_prediction = int(global_confidence >= config.pred_global_threshold)
    return local_predictions, global_prediction, global_confidence


def _call_probabilities(
    outputs: Any, call_class_index: int, output_type: str
) -> np.ndarray:
    """Extract call-class probabilities from logits or declared probabilities."""
    if torch.is_tensor(outputs):
        outputs = outputs.detach().cpu().numpy()
    values = np.asarray(outputs)

    if values.ndim == 1:
        if output_type != "probabilities":
            raise ValueError("one-dimensional model output must be probabilities")
        if not np.isfinite(values).all() or (values < 0).any() or (values > 1).any():
            raise ValueError("one-dimensional model output must contain probabilities")
        return values.astype(np.float64, copy=False)
    if values.ndim != 2 or values.shape[0] == 0:
        raise ValueError(
            f"expected model output with shape (batch, classes), got {values.shape}"
        )

    if output_type == "probabilities":
        if not np.isfinite(values).all() or (values < 0).any() or (values > 1).any():
            raise ValueError("model output probabilities must be finite and in [0, 1]")
        if values.shape[1] == 1:
            probabilities = values[:, 0]
        elif 0 <= call_class_index < values.shape[1]:
            probabilities = values[:, call_class_index]
        else:
            raise ValueError("call_class_index is outside the model output shape")
    else:
        if not 0 <= call_class_index < values.shape[1]:
            raise ValueError("call_class_index is outside the model output shape")
        values = values.astype(np.float32, copy=False)
        if not np.isfinite(values).all():
            raise ValueError("model logits must be finite")
        shifted = values - values.max(axis=1, keepdims=True)
        exponentials = np.exp(shifted)
        probabilities = (exponentials / exponentials.sum(axis=1, keepdims=True))[
            :, call_class_index
        ]

    return probabilities.astype(np.float64, copy=False)


def run_file_inference(
    audio_file_path: str,
    config: DetectorInferenceConfig,
    predict_batch: Callable[[torch.Tensor], Any],
    output_type: str = "logits",
) -> DetectionResult:
    """Run shared segmentation, batching, probability conversion, and aggregation."""
    if output_type not in {"logits", "probabilities"}:
        raise ValueError(f"unsupported model output type: {output_type}")

    start_time = time.perf_counter()
    spectrograms = []
    segment_info = []

    for mel_spec, start_s, duration_s in _audio_preprocessor(config).process_segments(
        audio_file_path
    ):
        spectrograms.append(mel_spec)
        segment_info.append((start_s, duration_s))

    if not spectrograms:
        return DetectionResult(
            local_predictions=[],
            local_confidences=[],
            global_prediction=0,
            global_confidence=0.0,
            segment_predictions=[],
            metadata=DetectionMetadata(
                wav_file_path=audio_file_path,
                file_duration_s=0.0,
                processing_time_s=time.perf_counter() - start_time,
            ),
        )

    confidences = []
    max_batch_size = config.inference.max_batch_size
    for batch_start in range(0, len(spectrograms), max_batch_size):
        batch_end = min(batch_start + max_batch_size, len(spectrograms))
        batch = torch.stack(spectrograms[batch_start:batch_end])
        with torch.inference_mode():
            outputs = predict_batch(batch)
        batch_confidences = _call_probabilities(
            outputs, config.model.call_class_index, output_type
        )
        if len(batch_confidences) != batch_end - batch_start:
            raise ValueError("model output batch size does not match input batch size")
        confidences.extend(batch_confidences.tolist())

    segment_predictions = [
        SegmentPrediction(
            start_time_s=start_s,
            duration_s=duration_s,
            confidence=confidence,
        )
        for (start_s, duration_s), confidence in zip(segment_info, confidences)
    ]
    local_predictions, global_prediction, global_confidence = aggregate_predictions(
        segment_predictions, config.global_prediction
    )
    last_start, last_duration = segment_info[-1]

    return DetectionResult(
        local_predictions=local_predictions,
        local_confidences=confidences,
        global_prediction=global_prediction,
        global_confidence=global_confidence,
        segment_predictions=segment_predictions,
        metadata=DetectionMetadata(
            wav_file_path=audio_file_path,
            file_duration_s=last_start + last_duration,
            processing_time_s=time.perf_counter() - start_time,
        ),
    )
