# Development Guide

Development setup, local scripts, and testing for model inference.

## Setup

```bash
cd InferenceSystem
uv sync --group dev
source .venv/bin/activate  # or .\.venv\Scripts\activate.bat on Windows
```

For model weights, either:
- Use HuggingFace: `OrcaHelloSRKWDetectorV1.from_pretrained("orcasound/orcahello-srkw-detector-v1")`
- Or extract from FastAI model (see [Extract FastAI Weights](#extract-fastai-weights))

## Scripts

### Run Inference

```bash
# Single file
python scripts/run_inference.py path/to/audio.wav

# Directory (batch)
python scripts/run_inference.py path/to/audio_dir/
python scripts/run_inference.py path/to/audio_dir/ --output results_dir/

# Re-aggregate existing results with different config
python scripts/run_inference.py results.json --reaggregate
python scripts/run_inference.py results_dir/ --reaggregate
```

### Run Audio Processing

Segment audio and generate spectrograms (without model inference):

```bash
python scripts/run_audio_processing.py path/to/audio.wav
python scripts/run_audio_processing.py path/to/audio.wav --segment-duration 30
python scripts/run_audio_processing.py path/to/audio.flac --output-dir /tmp/segments
```

### Upload to HuggingFace Hub

```bash
HF_TOKEN=<your_huggingface_token> python scripts/upload_to_hf_hub.py --checkpoint model/model_v1.pt -m "Update model checkpoint"
```

### Extract FastAI Weights

Convert FastAI model.pkl to standalone PyTorch weights (shouldn't need to do this anymore): 

```bash
# Requires inference-venv with fastai installed (see branch: https://github.com/orcasound/orcahello/tree/2026-03-12/inference-snapshot)
source inference-venv/bin/activate
python scripts/extract_fastai_weights.py model/model.pkl model/model_v1.pt
```

## Testing

### Quick Start

```bash
cd InferenceSystem
uv sync --group dev

# Run all model inference tests
uv run pytest tests/test_model_inference.py -v

# Run all audio preprocessing tests
uv run pytest tests/test_audio_preprocessing.py -v

# Run all orchestrator integration tests
uv run pytest tests/test_orchestrator.py -v
```

### Test Structure

Tests are organized into three categories:

| Category | Description |
|----------|-------------|
| Unit tests | Model/audio class instantiation, shapes, ranges |
| Parity checks | Compare model outputs against pre-committed FastAI references |
| Integration tests | Orchestrator end-to-end tests against test config files |

### Running Specific Tests

```bash
# Unit tests only (no reference files needed)
uv run pytest tests/test_model_inference.py::TestOrcaHelloSRKWDetectorUnit -v
uv run pytest tests/test_audio_preprocessing.py::TestAudioPreprocessingUnit -v

# Parity tests (use pre-committed reference files in tests/reference_outputs/)
uv run pytest tests/test_model_inference.py::TestParityChecks -v
```

## Module Structure

```
src/model/
├── __init__.py           # Public exports
├── inference.py          # OrcaHelloSRKWDetectorV1 model class
├── audio_frontend.py     # AudioPreprocessor for mel spectrogram generation
└── types.py              # Dataclasses (DetectorInferenceConfig, DetectionResult, etc.)
```

### Key Classes

**OrcaHelloSRKWDetectorV1** - Main model class
- `from_pretrained(repo_id)` - Load from HuggingFace
- `from_checkpoint(path, config)` - Load from local .pt file
- `detect_srkw_from_file(wav_path)` - Full pipeline inference
- `predict_call(mel_batch)` - Raw spectrogram inference

**AudioPreprocessor** - Audio to mel spectrogram
- `process_segments(wav_path)` - Generator yielding (mel_spec, start_s, duration_s)

**DetectorInferenceConfig** - Configuration
- `from_yaml(path)` / `from_dict(d)` - Load config
- Sections: `audio`, `spectrogram`, `model`, `inference`, `global_prediction`

## Configuration

Default config in `model/config.yaml`. Key parameters:

```yaml
inference:
  window_s: 3.0          # segment length
  window_hop_s: 2.0      # hop between segments

global_prediction:
  aggregation_strategy: "mean_top_k"  # or "mean_thresholded"
  mean_top_k: 2
  pred_global_threshold: 0.6
  pred_local_threshold: 0.5
```

| Parameter | Description |
|-----------|-------------|
| `aggregation_strategy` | How segment confidences are combined into a file-level `global_confidence` score. `"mean_top_k"` averages the top K most confident segments; `"mean_thresholded"` averages only segments exceeding `pred_local_threshold`. |
| `mean_top_k` | Number of top segments to average when using `mean_top_k` strategy. |
| `pred_global_threshold` | Threshold (0–1) applied to the aggregated global confidence to produce the final binary file-level prediction. |
| `pred_local_threshold` | Confidence threshold (0–1) for per-segment binary predictions (used to diplay in moderator UI). Also selects which segments contribute to global confidence under `mean_thresholded`. |

## CI/CD

Tests run via `.github/workflows/InferenceSystem.yaml`.
