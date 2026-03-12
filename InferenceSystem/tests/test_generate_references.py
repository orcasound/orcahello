"""
Reference Output Generation Tests

Generate FastAI reference outputs used by parity tests in test_audio_preprocessing.py
and test_model_inference.py.

Run these tests in the legacy FastAI environment (inference-venv) BEFORE running parity
tests in the new model environment:

    uv run pytest tests/test_generate_references.py -v -s

Classes:
- TestGenerateAudioReferences:  Generate mel spectrogram references (fastai_audio pipeline)
- TestGenerateModelReferences:  Generate inference prediction references (FastAI model)

All fastai imports are deferred to inside test functions so this file can be collected
by pytest even when fastai is not installed (tests will be skipped instead of erroring).
"""

import json
import random
from dataclasses import asdict
from pathlib import Path

import pytest
import torch

from model.audio_frontend import (
    audio_segment_generator,
    featurize_waveform,
    load_processed_waveform,
)
from tests.utils import plot_spec_comparison


def _make_segments(sample_1min_wav, v1_config, max_segments, segments_start_s):
    """Create the standard audio segment generator for reference generation."""
    return audio_segment_generator(
        sample_1min_wav,
        segment_duration_s=v1_config["inference"]["window_s"],
        segment_hop_s=v1_config["inference"]["window_hop_s"],
        max_segments=max_segments,
        start_time_s=segments_start_s or 0.0,
    )


class TestGenerateAudioReferences:
    """
    Generate mel spectrogram reference outputs from fastai_audio.

    These references are saved to tests/reference_outputs/ and consumed by
    TestAudioPreprocessingParity in test_audio_preprocessing.py.
    """

    def test_generate_audio_reference_outputs(
        self, sample_1min_wav, reference_dir, fastai_available, v1_config, max_segments, segments_start_s
    ):
        """
        Generate mel spectrogram references from the fastai_audio pipeline.

        Saves mel_raw (pure mel, no padding) and mel_standardized (full pipeline)
        for each segment to tests/reference_outputs/<wav_stem>_audio_reference.pt.

        Run in the legacy FastAI environment (inference-venv).
        """
        if not fastai_available:
            pytest.skip(
                "fastai_audio not available - run in inference-venv to generate references"
            )

        # Import fastai_audio modules explicitly here (not at module level)
        from legacy.fastai_frontend import prepare_audio as fastai_prepare_audio
        from legacy.fastai_frontend import prepare_audio_mel_raw

        random.seed(42)  # Fix random seed for reproducible padding

        wav_name = Path(sample_1min_wav).stem
        references = {}

        for window_idx, (segment_path, _, _) in enumerate(
            _make_segments(sample_1min_wav, v1_config, max_segments, segments_start_s)
        ):
            mel_raw_spec = prepare_audio_mel_raw(segment_path, v1_config)
            mel_standardized_spec = fastai_prepare_audio(segment_path, v1_config)

            references[f"segment_{window_idx}"] = {
                "mel_raw": mel_raw_spec,
                "mel_standardized": mel_standardized_spec,
            }

        # Save reference spectrograms
        reference_file = reference_dir / f"{wav_name}_audio_reference.pt"
        torch.save(references, reference_file)
        print(f"Saved reference outputs to {reference_file}")

        for seg_name, specs in references.items():
            print(
                f"  {seg_name}: mel_raw={specs['mel_raw'].shape}, mel_standardized={specs['mel_standardized'].shape}"
            )

        # Save spectrogram comparison images
        images_dir = reference_dir / f"{wav_name}_spectrograms"
        images_dir.mkdir(exist_ok=True)

        for seg_name, specs in references.items():
            img_path = images_dir / f"{seg_name}_comparison.png"
            plot_spec_comparison(
                specs["mel_raw"],
                specs["mel_standardized"],
                img_path,
                f"{wav_name} {seg_name}",
            )

        print(f"Saved spectrogram images to {images_dir}")


class TestGenerateModelReferences:
    """
    Generate FastAI inference prediction references.

    These references are saved to tests/reference_outputs/ and consumed by
    TestParityChecks in test_model_inference.py.

    Requires:
    - Audio reference outputs already generated (run TestGenerateAudioReferences first)
    - FastAI model checkpoint at model/model.pkl
    """

    def test_generate_segment_predictions_reference(
        self, model_dir, reference_dir, sample_1min_wav, fastai_available
    ):
        """
        Generate per-segment FastAI confidence scores for parity testing.

        Uses audio preprocessing reference outputs (mel spectrograms) as input.
        Saves to tests/reference_outputs/<wav_stem>_segment_preds_reference.json.
        """
        if not fastai_available:
            pytest.skip("fastai not available - run in inference-venv to generate references")

        # Import FastAI modules explicitly here (not at module level)
        from audio.data import AudioItem
        from legacy.fastai_inference import FastAIModel

        wav_name = Path(sample_1min_wav).stem
        audio_ref_file = reference_dir / f"{wav_name}_audio_reference.pt"
        if not audio_ref_file.exists():
            pytest.skip(
                f"Audio reference not found: {audio_ref_file}. "
                "Run test_generate_audio_reference_outputs first."
            )

        audio_ref = torch.load(audio_ref_file, weights_only=False)

        fastai_model = FastAIModel(
            model_path=str(model_dir),
            model_name="model.pkl",
        )

        references = {
            "segment_predictions": {},
            "source_wav": wav_name,
        }

        print("\nGenerating fastai segment predictions:")
        print(f"Source: {wav_name}")
        print(f"Segments: {len(audio_ref)}")
        print()

        for seg_key in sorted(audio_ref.keys()):
            mel_spectro = audio_ref[seg_key]["mel_standardized"]  # (1, 256, 312)
            audio_item = AudioItem(mel_spectro, None)
            pred_result = fastai_model.model.predict(audio_item)
            call_prob = pred_result[2][1].item()

            references["segment_predictions"][seg_key] = call_prob
            print(f"  {seg_key}: {call_prob:.6f}")

        if hasattr(fastai_model.model.data, "classes"):
            references["classes"] = list(fastai_model.model.data.classes)
            print(f"\nFastAI classes: {fastai_model.model.data.classes}")

        reference_file = reference_dir / f"{wav_name}_segment_preds_reference.json"
        with open(reference_file, "w") as f:
            json.dump(references, f, indent=2)

        print(f"\nSaved reference to: {reference_file}")

    def test_generate_file_predictions_reference(
        self, model_dir, reference_dir, sample_1min_wav, fastai_available
    ):
        """
        Generate full-file FastAI inference reference for end-to-end parity testing.

        Saves to tests/reference_outputs/<wav_stem>_file_preds_reference.json.
        """
        if not fastai_available:
            pytest.skip("fastai not available - run in inference-venv to generate references")

        # Import FastAI modules explicitly here (not at module level)
        from legacy.fastai_inference import FastAIModel
        from model.types import DetectionMetadata, DetectionResult, SegmentPrediction

        model = FastAIModel(
            model_path=str(model_dir),
            model_name="model.pkl",
            threshold=0.5,
            min_num_positive_calls_threshold=3,
            smooth_predictions=False,
        )

        result = model.predict(sample_1min_wav)

        wav_name = Path(sample_1min_wav).stem

        segment_predictions = []
        submission = result["submission"]
        for _, row in submission.iterrows():
            segment_predictions.append(
                SegmentPrediction(
                    start_time_s=float(row["start_time_s"]),
                    duration_s=float(row["duration_s"]),
                    confidence=float(row["confidence"]),
                )
            )

        detection_result = DetectionResult(
            local_predictions=result["local_predictions"],
            local_confidences=result["local_confidences"],
            segment_predictions=segment_predictions,
            global_prediction=result["global_prediction"],
            global_confidence=result["global_confidence"] / 100.0,  # FastAI returns percentage
            metadata=DetectionMetadata(
                wav_file_path=wav_name,
                file_duration_s=0.0,
                processing_time_s=0.0,
            ),
        )

        print("\nGenerating fastai file prediction reference:")
        print(f"Source: {wav_name}")
        print(f"Segments: {len(detection_result.local_predictions)}")
        print(f"Global prediction: {detection_result.global_prediction}")
        print(f"Global confidence: {detection_result.global_confidence:.4f}")

        reference_file = reference_dir / f"{wav_name}_file_preds_reference.json"
        with open(reference_file, "w") as f:
            json.dump(asdict(detection_result), f, indent=2)

        print(f"\nSaved reference to: {reference_file}")
