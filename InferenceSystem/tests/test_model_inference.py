"""
Model Inference Tests - Verify OrcaHelloSRKWDetectorV1 matches fastai

Test structure:
- TestOrcaHelloSRKWDetectorUnit: Unit tests for model class
- TestReferenceGeneration: Generate FastAI reference outputs (run in inference-venv)
- TestParityChecks: Compare model_v1 against FastAI references (run in model-v1-venv)
"""

import sys
from pathlib import Path

import numpy as np
import pytest
import torch
import torch.nn.functional as F

from model_v1.inference import OrcaHelloSRKWDetectorV1

# Add src to path (for when tests are run directly)
sys.path.insert(0, str(Path(__file__).parent.parent / "src"))


class TestOrcaHelloSRKWDetectorUnit:
    """Unit tests for OrcaHelloSRKWDetectorV1 model class"""

    def test_model_creation(self, model_v1):
        """Test model instantiation"""
        assert model_v1 is not None
        assert model_v1.num_classes == 2
        # FastAI classes are ['negative', 'positive'], so class 1 is "positive"
        assert model_v1.call_class_index == 1

    def test_model_creation_defaults(self):
        """Test model with empty config uses defaults"""
        model = OrcaHelloSRKWDetectorV1({})
        assert model.num_classes == 2
        assert model.call_class_index == 1

    def test_forward_pass_shape(self, model_v1):
        """Test forward() returns (batch, num_classes) logits"""
        # Standard input shape: (batch, 1, n_mels=256, time_frames=312)
        x = torch.randn(1, 1, 256, 312)

        with torch.no_grad():
            output = model_v1(x)

        assert output.shape == (1, 2), f"Expected (1, 2), got {output.shape}"

    def test_forward_pass_batch(self, model_v1):
        """Test forward() with batch > 1"""
        x = torch.randn(4, 1, 256, 312)

        with torch.no_grad():
            output = model_v1(x)

        assert output.shape == (4, 2), f"Expected (4, 2), got {output.shape}"

    def test_predict_call_shape(self, model_v1):
        """Test predict_call() returns (batch,) probabilities"""
        x = torch.randn(1, 1, 256, 312)
        prob = model_v1.predict_call(x)

        # predict_call returns scalar for batch=1 due to squeeze()
        assert prob.dim() == 0 or prob.shape == (1,), f"Expected scalar or (1,), got {prob.shape}"

    def test_predict_call_batch(self, model_v1):
        """Test predict_call() with batch > 1"""
        x = torch.randn(4, 1, 256, 312)
        probs = model_v1.predict_call(x)

        assert probs.shape == (4,), f"Expected (4,), got {probs.shape}"

    def test_predict_call_range(self, model_v1):
        """Test predict_call() returns values in [0, 1]"""
        # Run multiple random inputs
        for _ in range(5):
            x = torch.randn(4, 1, 256, 312)
            probs = model_v1.predict_call(x)

            assert (probs >= 0).all(), f"Found negative probability: {probs}"
            assert (probs <= 1).all(), f"Found probability > 1: {probs}"

    def test_from_checkpoint_missing_file(self, v1_config):
        """Test from_checkpoint() raises error for missing file"""
        with pytest.raises(FileNotFoundError):
            OrcaHelloSRKWDetectorV1.from_checkpoint("/nonexistent/path/model.pt", v1_config)

    def test_from_checkpoint(self, model_dir, v1_config):
        """Test loading from checkpoint file"""
        model_path = model_dir / "model_v1.pt"
        if not model_path.exists():
            pytest.skip(f"Converted PyTorch checkpoint not found: {model_path}. Run extract_fastai_weights.py script first.")

        model = OrcaHelloSRKWDetectorV1.from_checkpoint(str(model_path), v1_config)

        assert model is not None
        # Verify model is in eval mode
        assert not model.training

        # Verify forward pass works
        x = torch.randn(1, 1, 256, 312)
        with torch.no_grad():
            output = model(x)
        assert output.shape == (1, 2)


class TestReferenceGeneration:
    """
    Generate FastAI reference outputs for parity testing.

    Run these tests in inference-venv (which has fastai) to create reference files.
    These references are then used by TestParityChecks (run in model-v1-venv).
    """

    def test_generate_segment_predictions_reference(
        self, model_dir, reference_dir, sample_1min_wav, fastai_available
    ):
        """
        Generate fastai reference predictions for per-segment inference.

        Uses audio preprocessing reference outputs (mel spectrograms) as input.

        The reference file contains:
        - segment_predictions: dict mapping segment_N to fastai confidence score
        - source_wav: name of WAV file used for audio reference
        """
        if not fastai_available:
            pytest.skip("fastai not available - run in inference-venv to generate references")

        # Late import for fastai-specific test
        import json
        from model.fastai_inference import FastAIModel
        from audio.data import AudioItem

        # Load audio preprocessing reference
        wav_name = Path(sample_1min_wav).stem
        audio_ref_file = reference_dir / f"{wav_name}_audio_reference.pt"
        if not audio_ref_file.exists():
            pytest.skip(
                f"Audio reference not found: {audio_ref_file}. "
                "Run test_generate_reference_outputs in test_audio_preprocessing.py first."
            )

        audio_ref = torch.load(audio_ref_file, weights_only=False)

        # Load fastai model
        fastai_model = FastAIModel(
            model_path=str(model_dir),
            model_name="model.pkl"
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
            # Get standardized mel spectrogram (1, 256, 312)
            mel_spectro = audio_ref[seg_key]["mel_standardized"]

            # Create AudioItem for fastai prediction
            # AudioItem wraps the spectrogram tensor
            audio_item = AudioItem(mel_spectro, None)

            # Get fastai prediction
            # fastai_model.model.predict() returns (category, tensor_index, probabilities)
            # We want probabilities[1] which is the "positive" class
            pred_result = fastai_model.model.predict(audio_item)
            call_prob = pred_result[2][1].item()

            references["segment_predictions"][seg_key] = call_prob
            print(f"  {seg_key}: {call_prob:.6f}")

        # Store class order for verification
        if hasattr(fastai_model.model.data, 'classes'):
            references["classes"] = list(fastai_model.model.data.classes)
            print(f"\nFastAI classes: {fastai_model.model.data.classes}")

        # Save as JSON
        reference_file = reference_dir / f"{wav_name}_segment_preds_reference.json"
        with open(reference_file, 'w') as f:
            json.dump(references, f, indent=2)
        print(f"\nSaved reference to: {reference_file}")

    def test_generate_file_predictions_reference(
        self, model_dir, reference_dir, sample_1min_wav, fastai_available
    ):
        """
        Generate fastai reference predictions for full-file inference.

        Saves as JSON-serialized DetectionResult for easy comparison.
        """
        if not fastai_available:
            pytest.skip("fastai not available - run in inference-venv to generate references")

        # Late import for fastai-specific test
        import json
        from dataclasses import asdict
        from model.fastai_inference import FastAIModel
        from model_v1.types import DetectionResult, DetectionMetadata, SegmentPrediction

        # Run fastai inference on the WAV file (no smoothing for parity testing)
        model = FastAIModel(
            model_path=str(model_dir),
            model_name="model.pkl",
            threshold=0.5,
            min_num_positive_calls_threshold=3,
            smooth_predictions=False
        )

        result = model.predict(sample_1min_wav)

        wav_name = Path(sample_1min_wav).stem

        # Build segment predictions from submission DataFrame
        segment_predictions = []
        submission = result["submission"]
        for _, row in submission.iterrows():
            segment_predictions.append(
                SegmentPrediction(
                    start_time_s=float(row["start_time_s"]),
                    duration_s=float(row["duration_s"]),
                    confidence=float(row["confidence"])
                )
            )

        # Create DetectionResult with dummy metadata
        detection_result = DetectionResult(
            local_predictions=result["local_predictions"],
            local_confidences=result["local_confidences"],
            segment_predictions=segment_predictions,
            global_prediction=result["global_prediction"],
            global_confidence=result["global_confidence"] / 100.0,  # FastAI returns percentage
            metadata=DetectionMetadata(
                wav_file_path=wav_name,
                file_duration_s=0.0,  # Dummy value
                processing_time_s=0.0  # Dummy value
            )
        )

        print("\nGenerating fastai file prediction reference:")
        print(f"Source: {wav_name}")
        print(f"Segments: {len(detection_result.local_predictions)}")
        print(f"Global prediction: {detection_result.global_prediction}")
        print(f"Global confidence: {detection_result.global_confidence:.4f}")

        # Save as JSON using asdict
        reference_file = reference_dir / f"{wav_name}_file_preds_reference.json"

        with open(reference_file, 'w') as f:
            json.dump(asdict(detection_result), f, indent=2)

        print(f"\nSaved reference to: {reference_file}")


class TestParityChecks:
    """
    Compare model_v1 outputs against FastAI references.

    Run these tests in model-v1-venv after generating references in inference-venv.
    """

    def test_segment_predictions_match_fastai(
        self, model_v1, audio_references, segment_prediction_references, numerical_tolerance
    ):
        """
        Compare model_v1 segment predictions against saved fastai reference.

        Uses audio preprocessing reference spectrograms as input to test
        per-segment inference parity.
        """
        mismatches = []

        print("\nComparing model_v1 segment predictions to fastai reference:")
        print(f"Source: {segment_prediction_references['source_wav']}")
        print()

        for seg_key in sorted(audio_references.keys()):
            mel_spectro = audio_references[seg_key]["mel_standardized"]  # (1, 256, 312)

            # Add batch dimension: (1, 256, 312) -> (1, 1, 256, 312)
            mel_batch = mel_spectro.unsqueeze(0)
            model_v1_prob = model_v1.predict_call(mel_batch).item()

            # Get fastai reference
            fastai_prob = segment_prediction_references["segment_predictions"][seg_key]

            diff = abs(model_v1_prob - fastai_prob)
            status = "PASS" if diff <= numerical_tolerance["atol"] else "FAIL"

            print(f"  {seg_key}: model_v1={model_v1_prob:.6f}, fastai={fastai_prob:.6f}, diff={diff:.2e} [{status}]")

            if diff > numerical_tolerance["atol"]:
                mismatches.append(
                    f"{seg_key}: model_v1={model_v1_prob:.6f}, fastai={fastai_prob:.6f}, diff={diff:.2e}"
                )

        if "classes" in segment_prediction_references:
            print(f"\nNote: FastAI classes were {segment_prediction_references['classes']}")

        assert len(mismatches) == 0, f"Parity failures:\n" + "\n".join(mismatches)

    def test_file_predictions_match_fastai(
        self, model_v1, file_prediction_references, sample_1min_wav, numerical_tolerance
    ):
        """
        Compare model_v1 full-file detection against saved fastai reference.

        Tests the complete pipeline end-to-end.
        """
        from tests.utils import diff_detection_results

        result_v1 = model_v1.detect_srkw_from_file(sample_1min_wav)
        result_ref = file_prediction_references
        atol = numerical_tolerance["atol"]

        print(f"\nComparing model_v1 vs fastai for: {result_ref['metadata']['wav_file_path']}")

        diff = diff_detection_results(result_v1, result_ref, abs_tolerance=atol)
        diff.assert_segment_preds(abs_tolerance=atol, name="file_preds")
        diff.assert_global_preds(abs_tolerance=atol, name="file_preds")
