# OrcaHello SRKW Inference System

Southern Resident Killer Whale call detection model and live inference orchestrator (together called the "Inference System").

## Quick Start: Standalone SRKW Detector Model

**Model on HuggingFace**: [orcasound/orcahello-srkw-detector-v1](https://huggingface.co/orcasound/orcahello-srkw-detector-v1)

Prerequisites:
- python 3.11 with [uv installed](https://docs.astral.sh/uv/getting-started/installation/#installation-methods)
- appropriate `uv venv` [environment created](https://docs.astral.sh/uv/pip/environments/) and activated

```bash
cd InferenceSystem
uv sync
```

```python
from src.model import OrcaHelloSRKWDetectorV1

model = OrcaHelloSRKWDetectorV1.from_pretrained("orcasound/orcahello-srkw-detector-v1")
result = model.detect_srkw_from_file("audio.wav")

print(f"Orca detected: {result.global_prediction}")
print(f"Confidence: {result.global_confidence:.2f}")
```

For more details see [MODEL_CARD.md](model/MODEL_CARD.md) for usage, and [DEVELOPMENT.md](DEVELOPMENT.md#model-inference) for development & convenience scripts.

## Quick Start: Live Inference Orchestrator

The inference orchestrator streams audio from Orcasound's S3 buckets, runs AI inference on audio segments, and uploads positive detections to the OrcaHello Azure backend (Blob Storage, CosmosDB). Entry point: [src/LiveInferenceOrchestrator.py](src/LiveInferenceOrchestrator.py).

```bash
cd InferenceSystem
uv sync --group prod
```

The model is downloaded automatically from HuggingFace Hub on first use.

```bash
uv run python src/LiveInferenceOrchestrator.py \
  --orch_config tests/orch_configs/LiveHLS/LiveHLS_OrcasoundLab.yml \
  --max_live_iterations 2
```

Since this is a test meant to run locally, `upload_to_azure` within `orch_config` is set to `false` and no updates are made to the backend (Azure Blob Storage, CosmosDB). See [tests/orch_configs](tests/orch_configs) for more config examples.

```
2026-03-20 15:38:00,012 INFO --- [iter 1] LiveHLS poll: fetching segments in [1774046160, 1774046220] (now=1774046280, delay=60.0s)
2026-03-20 15:38:00,319 INFO Found 1 folders in date range
2026-03-20 15:38:00,576 INFO Dropping 10.0s tail audio (1 ts_segments)
2026-03-20 15:38:00,577 INFO [iter 1] LiveHLS poll: got 1 segments
2026-03-20 15:38:00,577 INFO Segment: folder=1773990017, indices=[5614:5620), start=2026-03-20T22:35:59Z, duration=60.0s
2026-03-20 15:38:02,144 INFO Processing clip: rpi-north-sjc_2026_03_20_15_35_59_PDT.wav, start_timestamp=2026-03-20T22:35:59Z
2026-03-20 15:38:02,662 DEBUG Generated spectrogram: wav_dir/rpi-north-sjc_2026_03_20_15_35_59_PDT.png
.../.venv/lib/python3.11/site-packages/torchaudio/functional/functional.py:582: UserWarning: At least one mel filterbank has all zero values. The value for `n_mels` (256) may be set too high. Or, the value for `n_freqs` (1281) may be set too low.
  warnings.warn(

=== Performance ===
File duration:   59.00s
Processing time: 0.30s
Realtime factor: 196.25x

=== Summary ===
1/29 segments predicted positive
Detected at times: [14.0]
global_confidence: 0.491
global_prediction: 0
2026-03-20 15:38:02,964 INFO Inference: prediction=0, confidence=0.491, positive_segments=1/29
2026-03-20 15:38:02,964 DEBUG Deleted local files: wav_dir/rpi-north-sjc_2026_03_20_15_35_59_PDT.wav, wav_dir/rpi-north-sjc_2026_03_20_15_35_59_PDT.png
2026-03-20 15:38:02,964 DEBUG Sleeping for 57.0s until 1774046340
```

## Development

For local scripts, testing, Docker, deployment, and contributing: [DEVELOPMENT.md](DEVELOPMENT.md)
