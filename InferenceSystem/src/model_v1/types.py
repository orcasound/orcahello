"""
Type definitions for model_v1 inference pipeline.

Contains all dataclasses used by the detector and audio frontend.
"""

import dataclasses
from dataclasses import asdict, dataclass
from typing import Dict, List


def _from_dict(cls, d: Dict):
    """Helper to create dataclass from dict, ignoring unknown keys."""
    field_names = {f.name for f in dataclasses.fields(cls)}
    return cls(**{k: v for k, v in d.items() if k in field_names})


@dataclass
class AudioConfig:
    downmix_mono: bool = True
    resample_rate: int = 20000
    normalize: bool = False

@dataclass
class SpectrogramConfig:
    sample_rate: int = 16000
    n_fft: int = 2560
    hop_length: int = 256
    mel_n_filters: int = 256
    mel_f_min: float = 0.0
    mel_f_max: float = 10000.0
    mel_f_pad: int = 0
    convert_to_db: bool = True
    top_db: int = 100

@dataclass
class ModelConfig:
    name: str = "orcahello-srkw-detector-v1"
    input_pad_s: float = 4.0
    num_classes: int = 2
    call_class_index: int = 1
    device: str = "auto"   # "auto" selects mps > cuda > cpu; or explicit "cpu"/"cuda"/"mps"
    precision: str = "auto"    # "auto" (float16 on cuda/mps, float32 on cpu), "float16", or "float32"


@dataclass
class InferenceConfig:
    window_s: float = 2.0
    window_hop_s: float = 1.0
    max_batch_size: int = 8
    strict_segments: bool = True


@dataclass
class GlobalPredictionConfig:
    """Configuration for aggregating segment predictions into global prediction.

    Attributes:
        aggregation_strategy: "mean_thresholded" or "mean_top_k"
        mean_top_k: top segments to average for global_confidence (mean_top_k)
        pred_local_threshold: for local binary predictions (0-1), and selecting segments to average for global_confidence (mean_thresholded)
        pred_global_threshold: applied to global_confidence for binary global_prediction (0-1)
    """
    aggregation_strategy: str = "mean_top_k"
    mean_top_k: int = 2
    pred_local_threshold: float = 0.5
    pred_global_threshold: float = 0.6


@dataclass
class DetectorInferenceConfig:
    """Full config for detector inference, validated from YAML dict."""
    audio: AudioConfig = None
    spectrogram: SpectrogramConfig = None
    model: ModelConfig = None
    inference: InferenceConfig = None
    global_prediction: GlobalPredictionConfig = None

    def __post_init__(self):
        self.audio = self.audio or AudioConfig()
        self.spectrogram = self.spectrogram or SpectrogramConfig()
        self.model = self.model or ModelConfig()
        self.inference = self.inference or InferenceConfig()
        self.global_prediction = self.global_prediction or GlobalPredictionConfig()

    @classmethod
    def from_dict(cls, d: Dict) -> "DetectorInferenceConfig":
        """Create config from nested YAML dict."""
        return cls(
            audio=_from_dict(AudioConfig, d.get("audio", {})),
            spectrogram=_from_dict(SpectrogramConfig, d.get("spectrogram", {})),
            model=_from_dict(ModelConfig, d.get("model", {})),
            inference=_from_dict(InferenceConfig, d.get("inference", {})),
            global_prediction=_from_dict(GlobalPredictionConfig, d.get("global_prediction", {})),
        )

    @classmethod
    def from_yaml(cls, yaml_path: str) -> "DetectorInferenceConfig":
        """Load config from a YAML file."""
        import yaml
        with open(yaml_path) as f:
            d = yaml.safe_load(f)
        return cls.from_dict(d)

    def as_dict(self) -> Dict:
        """Convert to nested dict (suitable for prepare_audio, JSON serialization, etc.)."""
        return asdict(self)


@dataclass
class SegmentPrediction:
    start_time_s: float
    duration_s: float
    confidence: float


@dataclass
class DetectionMetadata:
    """Metadata for a detection result including source file info and performance metrics."""
    wav_file_path: str
    file_duration_s: float
    processing_time_s: float

    @property
    def realtime_factor(self) -> float:
        """Ratio of file duration to processing time. >1 means faster than realtime."""
        if self.processing_time_s > 0:
            return self.file_duration_s / self.processing_time_s
        return float('inf')


@dataclass
class DetectionResult:
    """
    Detection result from SRKW detector.

    Attributes:
        local_predictions: List of binary predictions (0/1) for each time segment
        local_confidences: List of confidence scores (0-1) for each time segment
        global_prediction: Binary prediction (0/1) indicating if orca calls were detected
        global_confidence: Aggregated confidence score (0-1)
        metadata: Source file info and performance metrics
    """
    local_predictions: List[int]
    local_confidences: List[float]
    segment_predictions: List[SegmentPrediction]
    global_prediction: int
    global_confidence: float
    metadata: DetectionMetadata

    def print_summary(self, verbose: bool = False) -> None:
        """Print detection result summary.

        Args:
            verbose: If True, also prints per-segment local predictions in addition to overall
                     performance and summary metrics.
                     If False (default, used by orchestrator), prints overall performance
                     and summary metrics without per-segment details.
        """
        if verbose:
            print("\n=== Local predictions ===")
            print(f"{'Segment':<10} {'Start (s)':<12} {'Duration (s)':<14} {'Prediction':<12} {'Confidence':<12}")
            print("-" * 60)
            for i, (pred, conf, seg) in enumerate(zip(self.local_predictions, self.local_confidences, self.segment_predictions)):
                print(f"{i+1:<10} {seg.start_time_s:<12.1f} {seg.duration_s:<14.1f} {pred:<12} {conf:<12.3f}")
            print("-" * 60)

        meta = self.metadata
        print("\n=== Performance ===")
        print(f"File duration:   {meta.file_duration_s:.2f}s")
        print(f"Processing time: {meta.processing_time_s:.2f}s")
        print(f"Realtime factor: {meta.realtime_factor:.2f}x")

        num_positive = sum(self.local_predictions)
        print("\n=== Summary ===")
        print(f"{num_positive}/{len(self.local_predictions)} segments predicted positive")
        if num_positive > 0:
            positive_segment_times = [seg.start_time_s for seg, pred in zip(self.segment_predictions, self.local_predictions) if pred == 1]
            print(f"Detected at times: {positive_segment_times!s}")
        print(f"global_confidence: {self.global_confidence:.3f}")
        print(f"global_prediction: {self.global_prediction}")
