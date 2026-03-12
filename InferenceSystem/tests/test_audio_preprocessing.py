"""
Audio Preprocessing Tests
"""

from pathlib import Path

import pytest
import torch
from model.audio_frontend import (
    audio_segment_generator,
    featurize_waveform,
    load_processed_waveform,
    prepare_audio,
    standardize,
)
from tests.utils import diff_specs, plot_spec_comparison


def _make_segments(sample_1min_wav, v1_config, max_segments, segments_start_s):
    """Create the standard audio segment generator for parity tests."""
    return audio_segment_generator(
        sample_1min_wav,
        segment_duration_s=v1_config["inference"]["window_s"],
        segment_hop_s=v1_config["inference"]["window_hop_s"],
        max_segments=max_segments,
        start_time_s=segments_start_s or 0.0,
    )


class TestAudioPreprocessingUnit:
    """Unit tests for audio preprocessing components"""

    def test_load_audio(self, sample_1min_wav, v1_config):
        """Test that audio loads correctly with config"""
        waveform, sr = load_processed_waveform(sample_1min_wav, v1_config["audio"])

        assert isinstance(waveform, torch.Tensor)
        assert waveform.ndim == 2  # (channels, samples)
        assert waveform.shape[0] == 1  # mono after downmix
        assert sr == v1_config["audio"]["resample_rate"]

    def test_featurize_waveform(self, sample_1min_wav, v1_config):
        """Test mel spectrogram feature extraction"""
        waveform, sr = load_processed_waveform(sample_1min_wav, v1_config["audio"])
        features, times, freqs = featurize_waveform(waveform, sr, v1_config["spectrogram"])

        # Check feature shape
        assert features.ndim == 3  # (channels, n_mels, n_frames)
        assert features.shape[1] == v1_config["spectrogram"]["mel_n_filters"]

        # Check times axis
        assert times.ndim == 1
        assert len(times) == features.shape[2]

        # Check freqs axis
        assert freqs.ndim == 1
        assert len(freqs) == v1_config["spectrogram"]["mel_n_filters"]

    def test_standardize(self, sample_1min_wav, v1_config):
        """Test spectrogram standardization (pad/crop)"""
        waveform, sr = load_processed_waveform(sample_1min_wav, v1_config["audio"])
        features, _, _ = featurize_waveform(waveform, sr, v1_config["spectrogram"])

        # Add resample_rate to model_config
        model_config = {**v1_config["model"], "resample_rate": v1_config["audio"]["resample_rate"]}
        standardized = standardize(features, model_config, v1_config["spectrogram"])

        # Check output has expected frame count
        expected_frames = int(
            v1_config["model"]["input_pad_s"]
            * v1_config["audio"]["resample_rate"]
            / v1_config["spectrogram"]["hop_length"]
        )
        assert standardized.shape[2] == expected_frames

    def test_prepare_audio_full_pipeline(self, sample_1min_wav, v1_config):
        """Test complete audio preparation pipeline"""
        mel_spec = prepare_audio(sample_1min_wav, v1_config)

        assert isinstance(mel_spec, torch.Tensor)
        assert mel_spec.ndim == 3
        assert mel_spec.shape[1] == v1_config["spectrogram"]["mel_n_filters"]

        # Check standardized frame count
        expected_frames = int(
            v1_config["model"]["input_pad_s"]
            * v1_config["audio"]["resample_rate"]
            / v1_config["spectrogram"]["hop_length"]
        )
        assert mel_spec.shape[2] == expected_frames


class TestAudioPreprocessingParity:
    """
    Parity tests comparing model audio preprocessing against pre-committed FastAI references.
    """

    def test_mel_raw_parity(self, sample_1min_wav, audio_references, v1_config, max_segments, segments_start_s):
        """
        MEL RAW PARITY: Test pure mel spectrogram computation from raw audio clips.

        Tests mel spectrogram generation from raw 2-second clips WITHOUT padding/standardization.
        This isolates the core mel computation (downmix -> resample -> mel spec).
        """
        # Check if references have mel_raw key
        first_ref = list(audio_references.values())[0]
        if not isinstance(first_ref, dict) or "mel_raw" not in first_ref:
            pytest.skip(
                "Reference file is in old format. Regenerate with test_generate_reference_outputs."
            )

        mismatches = []
        for window_idx, (segment_path, _, _) in enumerate(
            _make_segments(sample_1min_wav, v1_config, max_segments, segments_start_s)
        ):
            # Process with model_v1 - raw mel only (no standardization)
            waveform, sr = load_processed_waveform(segment_path, v1_config["audio"])
            model_v1_spec, _, _ = featurize_waveform(waveform, sr, v1_config["spectrogram"])

            ref_key = f"segment_{window_idx}"
            if ref_key not in audio_references:
                continue

            fastai_spec = audio_references[ref_key]["mel_raw"]

            # Compare using SpecDiff (handles overlapping frames automatically)
            diff = diff_specs(model_v1_spec, fastai_spec)
            try:
                diff.assert_close(name=ref_key)
            except AssertionError as e:
                mismatches.append(str(e))

        assert len(mismatches) == 0, "mel_raw parity failures:\n" + "\n".join(mismatches)

    def test_mel_standardized_parity(self, sample_1min_wav, audio_references, v1_config, debug_dir, max_segments, segments_start_s):
        """
        MEL STANDARDIZED PARITY: Compare full fastai_audio pipeline including input standardization (padding/cropping).

        Run with --save-debug to generate debug output to tests/tmp/mel_standardized_debug/
        for detailed analysis of differences.
        """
        # Set up debug output directory if enabled
        debug_output_dir = None
        if debug_dir is not None:
            debug_output_dir = debug_dir / "mel_standardized_debug"
            debug_output_dir.mkdir(parents=True, exist_ok=True)

        mismatches = []

        for window_idx, (segment_path, _, _) in enumerate(
            _make_segments(sample_1min_wav, v1_config, max_segments, segments_start_s)
        ):
            # Process with model_v1 - full pipeline including standardization
            model_v1_spec = prepare_audio(segment_path, v1_config)

            ref_key = f"segment_{window_idx}"
            if ref_key not in audio_references:
                continue

            fastai_spec = audio_references[ref_key]["mel_standardized"]

            # Compare using SpecDiff
            diff = diff_specs(model_v1_spec, fastai_spec)

            # Save debug output for this segment (only if --save-debug flag is set)
            if debug_output_dir is not None:
                seg_dir = debug_output_dir / ref_key
                diff.save_debug(seg_dir, ref_key)

            # Check tolerance and collect failures
            try:
                diff.assert_close(name=ref_key)
            except AssertionError as e:
                mismatches.append(str(e))

        if debug_output_dir is not None:
            print(f"\nDebug output saved to: {debug_output_dir}")

        assert len(mismatches) == 0, "mel_standardized parity failures:\n" + "\n".join(mismatches)
