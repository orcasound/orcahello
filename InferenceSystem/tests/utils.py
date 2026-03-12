import json
import matplotlib
import torch
from model.types import DetectionResult

matplotlib.use("Agg")
from dataclasses import dataclass, field
from pathlib import Path
from typing import List, Tuple, Iterator, Any, Dict

import matplotlib.pyplot as plt
import numpy as np


# =============================================================================
# SpecDiff - Modular Spectrogram Comparison
# =============================================================================


@dataclass
class SpecDiff:
    """
    Encapsulates spectrogram comparison metrics and provides assertion/debug utilities.
    
    Use `diff_specs(spec_1, spec_2)` factory function to create instances.
    """
    spec_1: torch.Tensor
    spec_2: torch.Tensor
    shape_1: Tuple[int, ...]
    shape_2: Tuple[int, ...]
    shape_match: bool
    percentile_90_diff: float
    max_diff: float
    mean_diff: float
    top_mel_bins: List[Tuple[int, float]] = field(default_factory=list)
    top_time_frames: List[Tuple[int, float]] = field(default_factory=list)
    _diff_tensor: torch.Tensor = field(repr=False, default=None)

    def assert_close(self, abs_tolerance: float = 2e-2, name: str = "") -> None:
        """
        Assert spectrograms are close within tolerance.
        
        Collects all failures and raises a single AssertionError with full summary.
        
        Args:
            abs_tolerance: Absolute tolerance
            name: Optional name for error messages (e.g., "segment_0")
        """
        failures = []
        
        # Check shape
        if not self.shape_match:
            failures.append(f"Shape mismatch: {self.shape_1} vs {self.shape_2}")
        
        # Check max diff against atol
        if self.percentile_90_diff > abs_tolerance:
            failures.append(f"Percentile 90 diff {self.percentile_90_diff:.6f} exceeds abs_tolerance {abs_tolerance}")
            failures.append(f"Max diff: {self.max_diff:.6f}")
            failures.append(f"Mean diff: {self.mean_diff:.6f}")
            bins_str = ", ".join(f"{idx}: {val:.4f}" for idx, val in self.top_mel_bins)
            failures.append(f"Top mel bins: [{bins_str}]")
            frames_str = ", ".join(f"{idx}: {val:.4f}" for idx, val in self.top_time_frames)
            failures.append(f"Top time frames: [{frames_str}]")

        if failures:
            prefix = f"SpecDiff assertion failed for {name}:" if name else "SpecDiff assertion failed:"
            msg = prefix + "\n  - " + "\n  - ".join(failures)
            raise AssertionError(msg)

    def save_debug(self, debug_dir: Path, name: str) -> None:
        """
        Save debug output: comparison image, tensors, and stats JSON.
        
        Args:
            debug_dir: Directory to save debug files
            name: Name for this comparison (used in filenames and titles)
        """
        debug_dir = Path(debug_dir)
        debug_dir.mkdir(parents=True, exist_ok=True)
        
        # Save comparison image
        plot_spec_comparison(
            self.spec_1, self.spec_2, 
            debug_dir / "comparison.png", 
            name
        )
        
        # Save tensors for offline analysis
        torch.save(self.spec_1, debug_dir / "spec_1.pt")
        torch.save(self.spec_2, debug_dir / "spec_2.pt")
        if self._diff_tensor is not None:
            torch.save(self._diff_tensor, debug_dir / "diff.pt")
        
        # Save stats as JSON
        stats = {
            "name": name,
            "shape_1": list(self.shape_1),
            "shape_2": list(self.shape_2),
            "shape_match": self.shape_match,
            "percentile_90_diff": self.percentile_90_diff,
            "max_diff": self.max_diff,
            "mean_diff": self.mean_diff,
            "top_mel_bins": [{"bin": idx, "max_diff": val} for idx, val in self.top_mel_bins],
            "top_time_frames": [{"frame": idx, "max_diff": val} for idx, val in self.top_time_frames],
        }
        with open(debug_dir / "stats.json", "w") as f:
            json.dump(stats, f, indent=2)

    def to_dict(self) -> dict:
        """Convert metrics to dictionary (for summary reports)."""
        return {
            "shape_1": list(self.shape_1),
            "shape_2": list(self.shape_2),
            "shape_match": self.shape_match,
            "percentile_90_diff": self.percentile_90_diff,
            "max_diff": self.max_diff,
            "mean_diff": self.mean_diff,
            "top_mel_bins": self.top_mel_bins,
            "top_time_frames": self.top_time_frames,
        }


def diff_specs(spec_1: torch.Tensor, spec_2: torch.Tensor) -> SpecDiff:
    """
    Compare two spectrograms and compute difference metrics.
    
    Args:
        spec_1: First spectrogram tensor (e.g., model output)
        spec_2: Second spectrogram tensor (e.g., reference)
        
    Returns:
        SpecDiff object with computed metrics
    """
    shape_1 = tuple(spec_1.shape)
    shape_2 = tuple(spec_2.shape)
    shape_match = shape_1 == shape_2
    
    # Compute diff on overlapping region if shapes differ
    if shape_match:
        diff_tensor = (spec_1 - spec_2).abs()
    else:
        # Compare overlapping frames only
        n_frames = min(spec_1.shape[-1], spec_2.shape[-1])
        n_mels = min(spec_1.shape[-2], spec_2.shape[-2])
        spec_1_overlap = spec_1[..., :n_mels, :n_frames]
        spec_2_overlap = spec_2[..., :n_mels, :n_frames]
        diff_tensor = (spec_1_overlap - spec_2_overlap).abs()
    
    percentile_90_diff = np.percentile(diff_tensor.flatten(), 90).item()
    max_diff = diff_tensor.max().item()
    mean_diff = diff_tensor.mean().item()
    
    # Compute top mel bins (max diff per mel bin)
    # diff_tensor shape: (1, n_mels, n_frames) or (n_mels, n_frames)
    if diff_tensor.ndim == 3:
        diff_2d = diff_tensor[0]
    else:
        diff_2d = diff_tensor
    
    # Max diff per mel bin (axis 1 = time)
    per_mel_max = diff_2d.max(dim=1).values  # shape: (n_mels,)
    top_mel_indices = per_mel_max.argsort(descending=True)[:5]
    top_mel_bins = [(idx.item(), per_mel_max[idx].item()) for idx in top_mel_indices]
    
    # Max diff per time frame (axis 0 = mel bins)
    per_frame_max = diff_2d.max(dim=0).values  # shape: (n_frames,)
    top_frame_indices = per_frame_max.argsort(descending=True)[:5]
    top_time_frames = [(idx.item(), per_frame_max[idx].item()) for idx in top_frame_indices]
    
    return SpecDiff(
        spec_1=spec_1,
        spec_2=spec_2,
        shape_1=shape_1,
        shape_2=shape_2,
        shape_match=shape_match,
        percentile_90_diff=percentile_90_diff,
        max_diff=max_diff,
        mean_diff=mean_diff,
        top_mel_bins=top_mel_bins,
        top_time_frames=top_time_frames,
        _diff_tensor=diff_tensor,
    )


def plot_spec_comparison(spec_1, spec_2, output_path, name):
    """
    Create side-by-side spectrogram comparison with difference heatmap.

    Args:
        spec_1: First spectrogram tensor (1, n_mels, n_frames)
        spec_2: Second spectrogram tensor (1, n_mels, n_frames)
        output_path: Path to save the comparison image
        name: Name for the title
    """
    arr_1 = spec_1[0].numpy() if spec_1.ndim == 3 else spec_1.numpy()
    arr_2 = spec_2[0].numpy() if spec_2.ndim == 3 else spec_2.numpy()

    # Handle shape mismatch by comparing overlapping region
    if arr_1.shape != arr_2.shape:
        n_mels = min(arr_1.shape[0], arr_2.shape[0])
        n_frames = min(arr_1.shape[1], arr_2.shape[1])
        arr_1 = arr_1[:n_mels, :n_frames]
        arr_2 = arr_2[:n_mels, :n_frames]

    diff = arr_1 - arr_2

    fig, axes = plt.subplots(1, 3, figsize=(18, 5))

    # Common colormap limits for spectrograms
    vmin = min(arr_1.min(), arr_2.min())
    vmax = max(arr_1.max(), arr_2.max())

    # spec_1 spectrogram
    im0 = axes[0].imshow(arr_1, aspect="auto", origin="lower", cmap="viridis", vmin=vmin, vmax=vmax)
    axes[0].set_title(f"spec_1 - {name}")
    axes[0].set_xlabel("Frame")
    axes[0].set_ylabel("Mel bin")
    fig.colorbar(im0, ax=axes[0], format="%.0f", label="dB")

    # spec_2 spectrogram
    im1 = axes[1].imshow(arr_2, aspect="auto", origin="lower", cmap="viridis", vmin=vmin, vmax=vmax)
    axes[1].set_title(f"spec_2 - {name}")
    axes[1].set_xlabel("Frame")
    axes[1].set_ylabel("Mel bin")
    fig.colorbar(im1, ax=axes[1], format="%.0f", label="dB")

    # Difference heatmap
    diff_abs_max = max(abs(diff.min()), abs(diff.max()), 0.001)  # Avoid zero range
    im2 = axes[2].imshow(diff, aspect="auto", origin="lower", cmap="RdBu_r",
                         vmin=-diff_abs_max, vmax=diff_abs_max)
    axes[2].set_title(f"Difference (spec_1 - spec_2)\npercentile 90={np.percentile(diff.flatten(), 90):.4f}, max={diff_abs_max:.4f}")
    axes[2].set_xlabel("Frame")
    axes[2].set_ylabel("Mel bin")
    fig.colorbar(im2, ax=axes[2], format="%.3f", label="dB diff")

    plt.tight_layout()
    plt.savefig(output_path, dpi=150, bbox_inches="tight")
    plt.close(fig)


# =============================================================================
# DetectionResultDiff - Detection Result Comparison
# =============================================================================


@dataclass
class DetectionResultDiff:
    """
    Encapsulates detection result comparison metrics.

    Use `diff_detection_results(result_v1, result_ref)` factory function to create instances.
    Then call assert_segment_preds() and assert_global_preds() separately.
    """
    # Segment-level metrics
    num_segments_v1: int
    num_segments_ref: int
    local_confidence_max_diff: float
    local_confidence_mean_diff: float
    local_predictions_mismatches: List[int] = field(default_factory=list)
    confidence_mismatches: List[Tuple[int, float, float, float]] = field(default_factory=list)

    # Global-level metrics
    num_positive_v1: int = 0
    num_positive_ref: int = 0
    global_pred_v1: int = 0
    global_pred_ref: int = 0
    global_confidence_diff: float = 0.0

    def assert_segment_preds(self, abs_tolerance: float = 1e-5, name: str = "") -> None:
        """Assert segment-level parity: num segments, local confidences, local predictions."""
        failures = []

        if self.num_segments_v1 != self.num_segments_ref:
            failures.append(f"Num segments: {self.num_segments_v1} vs {self.num_segments_ref}")

        if self.local_predictions_mismatches:
            failures.append(f"Local predictions differ at {len(self.local_predictions_mismatches)} segments: {self.local_predictions_mismatches[:10]}")

        if self.local_confidence_max_diff > abs_tolerance:
            failures.append(f"Local confidence max diff {self.local_confidence_max_diff:.6f} > {abs_tolerance}")
            if self.confidence_mismatches:
                top = ", ".join(f"seg{i}: {c1:.4f} vs {c2:.4f}" for i, c1, c2, _ in self.confidence_mismatches[:5])
                failures.append(f"Top mismatches: {top}")

        if failures:
            prefix = f"Segment parity failed for {name}:" if name else "Segment parity failed:"
            raise AssertionError(prefix + "\n  - " + "\n  - ".join(failures))

    def assert_global_preds(self, abs_tolerance: float = 1e-5, name: str = "") -> None:
        """Assert global-level parity: num positive segments, global confidence, global prediction."""
        failures = []

        if self.num_positive_v1 != self.num_positive_ref:
            failures.append(f"Num positive segments: {self.num_positive_v1} vs {self.num_positive_ref}")

        if self.global_pred_v1 != self.global_pred_ref:
            failures.append(f"Global prediction: {self.global_pred_v1} vs {self.global_pred_ref}")

        if self.global_confidence_diff > abs_tolerance:
            failures.append(f"Global confidence diff {self.global_confidence_diff:.6f} > {abs_tolerance}")

        if failures:
            prefix = f"Global parity failed for {name}:" if name else "Global parity failed:"
            raise AssertionError(prefix + "\n  - " + "\n  - ".join(failures))


def diff_detection_results(result_v1: DetectionResult, result_ref: Dict, abs_tolerance: float = 1e-3) -> DetectionResultDiff:
    """
    Compare two DetectionResult objects and compute difference metrics.

    Args:
        result_v1: DetectionResult from model (dataclass or dict)
        result_ref: DetectionResult reference (dataclass or dict)
        abs_tolerance: Tolerance for identifying confidence mismatches

    Returns:
        DetectionResultDiff object with computed metrics
    """
    # Handle both dataclass and dict inputs
    def _get(obj, key):
        return getattr(obj, key) if hasattr(obj, key) else obj[key]

    local_confs_v1, local_confs_ref = _get(result_v1, "local_confidences"), _get(result_ref, "local_confidences")
    local_preds_v1, local_preds_ref = _get(result_v1, "local_predictions"), _get(result_ref, "local_predictions")
    global_pred_v1, global_pred_ref = _get(result_v1, "global_prediction"), _get(result_ref, "global_prediction")
    global_conf_v1, global_conf_ref = _get(result_v1, "global_confidence"), _get(result_ref, "global_confidence")

    n_v1, n_ref = len(local_confs_v1), len(local_confs_ref)
    min_n = min(n_v1, n_ref)

    # Local prediction mismatches
    pred_mismatches = [i for i in range(min_n) if local_preds_v1[i] != local_preds_ref[i]]

    # Confidence diffs
    conf_diffs = [abs(local_confs_v1[i] - local_confs_ref[i]) for i in range(min_n)]
    max_diff = max(conf_diffs) if conf_diffs else 0.0
    mean_diff = sum(conf_diffs) / len(conf_diffs) if conf_diffs else 0.0

    # Top confidence mismatches
    conf_mismatches = sorted(
        [(i, local_confs_v1[i], local_confs_ref[i], conf_diffs[i]) for i in range(min_n) if conf_diffs[i] > abs_tolerance],
        key=lambda x: x[3], reverse=True
    )

    return DetectionResultDiff(
        num_segments_v1=n_v1,
        num_segments_ref=n_ref,
        local_confidence_max_diff=max_diff,
        local_confidence_mean_diff=mean_diff,
        local_predictions_mismatches=pred_mismatches,
        confidence_mismatches=conf_mismatches,
        num_positive_v1=sum(local_preds_v1),
        num_positive_ref=sum(local_preds_ref),
        global_pred_v1=global_pred_v1,
        global_pred_ref=global_pred_ref,
        global_confidence_diff=abs(global_conf_v1 - global_conf_ref),
    )
