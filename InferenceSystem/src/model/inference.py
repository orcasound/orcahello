import time

import torch
import torch.nn as nn
import torch.nn.functional as F
from torchvision.models import resnet50
from typing import Dict, Optional, Union, List
from torch import Tensor
from huggingface_hub import PyTorchModelHubMixin

from .audio_frontend import AudioPreprocessor
from .types import (
    DetectorInferenceConfig,
    GlobalPredictionConfig,
    SegmentPrediction,
    DetectionResult,
    DetectionMetadata,
)


def _resolve_device(device_str: str) -> torch.device:
    """Resolve 'auto' to the best available device, or pass through explicit device strings."""
    if device_str == "auto":
        if torch.backends.mps.is_available():
            return torch.device("mps")
        elif torch.cuda.is_available():
            return torch.device("cuda")
        else:
            return torch.device("cpu")
    return torch.device(device_str)


def _resolve_dtype(precision_str: str, device: torch.device) -> torch.dtype:
    """Resolve precision string to torch.dtype.
    'auto' uses float16 on cuda/mps and float32 on cpu."""
    if precision_str == "auto":
        return torch.float16 if device.type in ("cuda", "mps") else torch.float32
    if precision_str == "float16":
        return torch.float16
    return torch.float32


def aggregate_predictions(
    segment_preds: List[SegmentPrediction],
    config: GlobalPredictionConfig
) -> tuple[List[int], int, float]:
    """
    Aggregate segment predictions into local predictions and global prediction/confidence.

    Args:
        segment_preds: List of SegmentPrediction objects with confidence scores
        config: GlobalPredictionConfig with aggregation settings

    Returns:
        Tuple of (local_predictions, global_prediction, global_confidence)
        - local_predictions: List of binary predictions (0/1) per segment
        - global_prediction: Binary prediction (0/1) for the file
        - global_confidence: Aggregated confidence score (0-1)
    """
    if len(segment_preds) == 0:
        return [], 0, 0.0

    # Binary local predictions based on pred_local_threshold
    local_predictions = [
        1 if seg.confidence > config.pred_local_threshold else 0
        for seg in segment_preds
    ]

    if config.aggregation_strategy == "mean_thresholded":
        # Global confidence = mean of positive segment confidences
        num_positive = sum(local_predictions)
        if num_positive > 0:
            positive_confs = [
                seg.confidence for seg, pred in zip(segment_preds, local_predictions)
                if pred == 1
            ]
            global_confidence = sum(positive_confs) / len(positive_confs)
        else:
            global_confidence = 0.0

    elif config.aggregation_strategy == "mean_top_k":
        # Sort segments by confidence descending
        sorted_segs = sorted(segment_preds, key=lambda s: s.confidence, reverse=True)

        # Take top K segments (at least 1, at most len(sorted_segs))
        k = max(1, min(config.mean_top_k, len(sorted_segs)))
        top_segs = sorted_segs[:k]

        # Global confidence = mean of top K confidences
        global_confidence = sum(s.confidence for s in top_segs) / len(top_segs)

    else:
        raise ValueError(f"Unknown aggregation_strategy: {config.aggregation_strategy}")

    # Global prediction based on pred_global_threshold
    global_prediction = 1 if global_confidence >= config.pred_global_threshold else 0

    return local_predictions, global_prediction, global_confidence



class AdaptiveConcatPool2d(nn.Module):
    """
    Adaptive pooling layer that concatenates max and average pooling.

    Matches fastai's AdaptiveConcatPool2d which outputs 2x the input channels.
    NOTE: FastAI concatenates [max_pool, avg_pool] in that order.
    """
    def __init__(self, output_size=1):
        super().__init__()
        self.avg_pool = nn.AdaptiveAvgPool2d(output_size)
        self.max_pool = nn.AdaptiveMaxPool2d(output_size)

    def forward(self, x):
        # FastAI concatenates max first, then avg
        return torch.cat([self.max_pool(x), self.avg_pool(x)], dim=1)


class OrcaHelloSRKWDetectorV1(
    nn.Module,
    PyTorchModelHubMixin,
    library_name="orcahello",
    repo_url="https://github.com/orcasound/aifororcas-livesystem",
    tags=["audio-classification", "bioacoustics", "orca-detection", "srkw"],
    license="other",
):
    """
    Wrapper of ResNet50 model for binary detection of individual orca calls
    from featurized audio segments (mel spectrograms).

    Architecture matches the production fastai model which uses:
    - ResNet50 backbone (3,4,6,3 Bottleneck blocks)
    - Single-channel input (grayscale spectrogram)
    - Custom head: AdaptiveConcatPool2d -> Linear(4096, 512) -> Linear(512, 2)
    """

    def __init__(self, config: Dict):
        super().__init__()

        # `config` must be a Dict, not DetectorInferenceConfig, for serialization with PyTorchModelHubMixin
        self.config = DetectorInferenceConfig.from_dict(config)

        self.num_classes = self.config.model.num_classes
        self.call_class_index = self.config.model.call_class_index
        self._device = _resolve_device(self.config.model.device)
        self._dtype = _resolve_dtype(self.config.model.precision, self._device)
        self.model = resnet50()

        # Modify first conv layer for single-channel input
        # Original: Conv2d(3, 64, kernel_size=7, stride=2, padding=3, bias=False)
        self.model.conv1 = nn.Conv2d(in_channels=1, out_channels=64, kernel_size=7, stride=2, padding=3, bias=False)
        # Replace avgpool and fc with fastai-style head
        # FastAI head structure:
        # [0] AdaptiveConcatPool2d - outputs 2048*2 = 4096 features
        # [1] Flatten
        # [2] BatchNorm1d(4096)
        # [3] Dropout(0.25)
        # [4] Linear(4096, 512)
        # [5] ReLU
        # [6] BatchNorm1d(512)
        # [7] Dropout(0.5)
        # [8] Linear(512, num_classes)
        self.model.avgpool = AdaptiveConcatPool2d(1)
        self.model.fc = nn.Sequential(
            nn.Flatten(),
            nn.BatchNorm1d(4096),
            nn.Dropout(0.25),
            nn.Linear(4096, 512),
            nn.ReLU(inplace=True),
            nn.BatchNorm1d(512),
            nn.Dropout(0.5),
            nn.Linear(512, self.num_classes),
        )

        self.to(device=self._device, dtype=self._dtype)

    def forward(self, x: Tensor  # shape: (batch, 1, n_mels, time_frames)
    ) -> Tensor:                 # shape: (batch, num_classes)
        """
        Forward pass through the model.

        Args:
            x (Tensor): Input tensor of shape (batch, 1, n_mels, time_frames)

        Returns:
            Tensor: Logits tensor of shape (batch, num_classes)
        """
        return self.model(x)

    def predict_call(self, x: Tensor  # shape: (batch, 1, n_mels, time_frames)
    ) -> Tensor:                      # shape: (batch,)
        """
        Get class probabilities using softmax.

        Args:
            x (Tensor): Input tensor of shape (batch, 1, n_mels, time_frames)

        Returns:
            Tensor: Probability tensor of shape (batch,)
        """
        self.eval()
        with torch.no_grad():
            logits = self.forward(x)
            return F.softmax(logits, dim=1)[:, self.call_class_index]
    
    def detect_srkw_from_file(
        self,
        wav_file_path: str,
        config: Optional[Union[Dict, DetectorInferenceConfig]] = None
    ) -> DetectionResult:
        """
        Detect SRKW calls from a WAV file.

        This method:
        1. Segments the audio file using configured window and hop parameters
        2. Generates mel spectrograms for each segment
        3. Runs inference using predict_call() on each segment
        4. Aggregates results into local and global predictions

        Args:
            wav_file_path: Path to the WAV file
            config: Optional configuration - values override self.config defaults.
                    Can be a dict or DetectorInferenceConfig object.
                    If None, uses self.config from model initialization.

        Returns:
            DetectionResult with local predictions, confidences, and global prediction
        """
        # Parse overrides and merge with stored config defaults
        if config is None:
            overrides = self.config
        elif isinstance(config, DetectorInferenceConfig):
            overrides = config
        else:
            # Dict -- backward compatible path
            overrides = DetectorInferenceConfig.from_dict(config)

        inf = overrides.inference
        max_batch_size = inf.max_batch_size

        # Generate segments and process into spectrograms using AudioPreprocessor
        start_time = time.perf_counter()
        preprocessor = AudioPreprocessor(overrides)
        spectrograms = []
        segment_info = []  # Track start times and durations

        for mel_spec, start_s, duration_s in preprocessor.process_segments(wav_file_path):
            spectrograms.append(mel_spec)
            segment_info.append({
                'start_time_s': start_s,
                'duration_s': duration_s
            })

        if len(spectrograms) == 0:
            # No segments generated - return empty result
            processing_time = time.perf_counter() - start_time
            return DetectionResult(
                local_predictions=[],
                local_confidences=[],
                global_prediction=0,
                global_confidence=0.0,
                segment_predictions=[],
                metadata=DetectionMetadata(
                    wav_file_path=wav_file_path,
                    file_duration_s=0.0,
                    processing_time_s=processing_time
                )
            )

        # Process spectrograms in batches to control memory usage
        all_confidences = []

        for batch_start in range(0, len(spectrograms), max_batch_size):
            batch_end = min(batch_start + max_batch_size, len(spectrograms))
            batch = torch.stack(spectrograms[batch_start:batch_end])
            # Move batch to model's device and cast to model dtype (e.g., fp16)
            batch = batch.to(device=self._device, dtype=self._dtype)
            batch_confidences = self.predict_call(batch)
            all_confidences.append(batch_confidences.cpu().float())

        # Concatenate all batch results
        confidences = torch.cat(all_confidences)

        # Build segment predictions (typed objects with start_time, duration, confidence)
        segment_preds = [
            SegmentPrediction(
                start_time_s=seg_info['start_time_s'],
                duration_s=seg_info['duration_s'],
                confidence=conf.item()
            )
            for seg_info, conf in zip(segment_info, confidences)
        ]

        # Aggregate predictions using configured method
        local_predictions, global_prediction, global_confidence = aggregate_predictions(
            segment_preds, overrides.global_prediction
        )
        local_confidences = [seg.confidence for seg in segment_preds]

        # Calculate file duration from last segment end time
        last_seg = segment_info[-1]
        file_duration_s = last_seg['start_time_s'] + last_seg['duration_s']
        processing_time_s = time.perf_counter() - start_time

        return DetectionResult(
            local_predictions=local_predictions,
            local_confidences=local_confidences,
            global_prediction=global_prediction,
            global_confidence=global_confidence,
            segment_predictions=segment_preds,
            metadata=DetectionMetadata(
                wav_file_path=wav_file_path,
                file_duration_s=file_duration_s,
                processing_time_s=processing_time_s
            )
        )

    @classmethod
    def from_checkpoint(cls, checkpoint_path: str, config: Dict) -> "OrcaHelloSRKWDetectorV1":
        """
        Load model from PyTorch checkpoint.

        Device and precision are taken from config.model.device and config.model.dtype.
        Defaults: device="auto" (mps > cuda > cpu), dtype="fp16".

        Args:
            checkpoint_path: Path to .pt checkpoint file
            config: Config dict (validated via DetectorInferenceConfig)

        Returns:
            OrcaHelloSRKWDetectorV1 with loaded weights in eval mode on configured device/dtype
        """
        model = cls(config)
        state_dict = torch.load(checkpoint_path, map_location=model._device, weights_only=True)
        model.load_state_dict(state_dict)
        model.to(device=model._device, dtype=model._dtype)
        model.eval()
        return model

