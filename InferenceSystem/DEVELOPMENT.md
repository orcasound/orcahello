# Development Guide

Development setup, local scripts, and testing for model_v1.

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

### Extract FastAI Weights

Convert FastAI model.pkl to standalone PyTorch weights:

```bash
# Requires inference-venv with fastai installed
source inference-venv/bin/activate
python scripts/extract_fastai_weights.py model/model.pkl model/model_v1.pt
```

### Upload to HuggingFace Hub

```bash
HF_TOKEN=<your_huggingface_token> python scripts/upload_to_hf_hub.py --checkpoint model/model_v1.pt -m "Update model checkpoint"
```

## Testing

### Quick Start

```bash
cd InferenceSystem
source model-v1-venv/bin/activate

# Run all model inference tests
python -m pytest tests/test_model_inference.py -v

# Run all audio preprocessing tests
python -m pytest tests/test_audio_preprocessing.py -v
```

### Test Structure

Tests are organized into three categories:

| Category | Description | Environment |
|----------|-------------|-------------|
| Unit tests | Model/audio class instantiation, shapes, ranges | model-v1-venv |
| Reference generation | Generate FastAI baseline outputs | inference-venv (fastai) |
| Parity checks | Compare model_v1 against FastAI references | model-v1-venv |

### Running Specific Tests

```bash
# Unit tests only (no reference files needed)
python -m pytest tests/test_model_inference.py::TestOrcaHelloSRKWDetectorUnit -v
python -m pytest tests/test_audio_preprocessing.py::TestAudioPreprocessingUnit -v

# Parity tests (require reference files in tests/reference_outputs/)
python -m pytest tests/test_model_inference.py::TestParityChecks -v
```

### Regenerating Reference Files

Reference files are pre-committed for CI. To regenerate (requires fastai):

```bash
source inference-venv/bin/activate

# Audio preprocessing references
python -m pytest tests/test_audio_preprocessing.py::TestAudioPreprocessingParity::test_generate_reference_outputs -v

# Model inference references
python -m pytest tests/test_model_inference.py::TestReferenceGeneration -v
```

## Module Structure

```
src/model_v1/
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
  pred_local_threshold: 0.5
  pred_global_threshold: 0.6
```

## CI/CD

Tests run via `.github/workflows/InferenceSystem.yaml`. Rewritten `model_v1` tests don't yet run in CI.
