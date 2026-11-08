# OrcaHello Audio Analysis API

The API analyzes an uploaded WAV or FLAC file without connecting to the live S3/CosmosDB pipeline. It loads one CPU inference engine at startup. The default model is downloaded from Hugging Face on first startup and cached under `HF_HOME`.

## Local Run

From `InferenceSystem/`, install the API dependencies and start one worker:

```bash
uv sync --no-dev --group api
MODEL_BACKEND=pytorch uv run --no-sync uvicorn api.app:create_app --factory --app-dir src --host 0.0.0.0 --port 8000 --workers 1
```

On PowerShell, set variables with `$env:MODEL_BACKEND="pytorch"` before running Uvicorn. `MODEL_BACKEND=auto` is also supported; it uses ONNX when an ONNX path and runtime are available, otherwise it falls back to PyTorch.

## Environment

| Variable | Default | Purpose |
| --- | --- | --- |
| `MODEL_BACKEND` | `auto` | Select `auto`, `onnx`, or `pytorch`. Explicit `onnx` fails startup if initialization fails. |
| `ONNX_MODEL_PATH` | unset | ONNX artifact path inside the container. Mount the artifact read-only when using Docker. |
| `ONNX_OUTPUT_TYPE` | `logits` | ONNX graph output format: `logits` or `probabilities`. |
| `ORCAHELLO_API_KEY` | unset | Optional shared key required in `X-API-Key` on `/analyze`; `/health` remains public. |
| `MAX_CONCURRENT_INFERENCES` | `1` | Maximum simultaneous CPU inference jobs per process; excess requests receive `503`. |
| `RATE_LIMIT_PER_MINUTE` | `5` | Per-client IP request limit for `/analyze`, enforced in a process-local sliding window. |
| `MAX_AUDIO_DURATION_SECONDS` | `60` | Maximum decoded WAV/FLAC duration; longer audio receives `413`. |
| `MIN_AUDIO_DURATION_SECONDS` | `3` | Minimum decoded audio duration; shorter audio receives `422`. |
| `MAX_UPLOAD_BYTES` | `20971520` | Maximum uploaded file size (20 MiB). |
| `MAX_REQUEST_BODY_BYTES` | upload limit + 64 KiB | Maximum complete multipart request body, including form overhead. |
| `MODEL_HF_REPO_ID` | `orcasound/orcahello-srkw-detector-v1` | Hugging Face model repository used by the PyTorch backend. |
| `MODEL_CONFIG_PATH` | `model/config.yaml` | Optional inference YAML path; the bundled config is used by default. |
| `TRUSTED_PROXY_CIDRS` | unset | Comma-separated trusted proxy networks. Forwarded client IPs are used only when the peer is trusted. |

The rate limiter is in-memory and local to one process. Run one Uvicorn worker per container; if deploying multiple replicas, each replica has its own limit state. Configure trusted proxy CIDRs only for proxies that sanitize forwarded headers.

## HTTP Examples

Health/readiness:

```bash
curl http://localhost:8000/health
```

Analyze a file:

```bash
curl -X POST http://localhost:8000/analyze \
  -F 'file=@tests/test_data/rpi_sunset_bay_2025_09_18_01_12_06_PDT--f6b3fcd7-2036-433a-8a18-76a6b3b4f0c9.wav'
```

Request a base64-encoded spectrogram in the JSON response:

```bash
curl -X POST http://localhost:8000/analyze \
  -F 'file=@tests/test_data/rpi_sunset_bay_2025_09_18_01_12_06_PDT--f6b3fcd7-2036-433a-8a18-76a6b3b4f0c9.wav' \
  -F 'include_spectrogram=true'
```

When `ORCAHELLO_API_KEY` is configured, add `-H 'X-API-Key: your-key'` to either POST. Responses contain file duration, global detection/confidence, positive segment timestamps/confidences, and optionally `spectrogram_png_base64`.

## Docker

Build from the `InferenceSystem/` directory. ONNX Runtime is optional and can be included at build time:

```bash
docker build -f Dockerfile.api -t orcahello-api .
# Optional ONNX Runtime build:
docker build --build-arg INSTALL_ONNXRUNTIME=true -f Dockerfile.api -t orcahello-api:onnx .
```

Run the PyTorch backend locally:

```bash
docker run --rm -p 8000:8000 \
  -e MODEL_BACKEND=pytorch \
  -e ORCAHELLO_API_KEY=dev-key \
  orcahello-api
```

The image starts Uvicorn with one worker and exposes port `8000`. It includes a `/health` Docker health check. The Hugging Face model is fetched at first startup; provide outbound access and a writable/persistent `HF_HOME` cache as appropriate for the host.

To run with ONNX, mount the model artifact and set the backend and output format:

```bash
docker run --rm -p 8000:8000 \
  -v "$PWD/models:/models:ro" \
  -e MODEL_BACKEND=onnx \
  -e ONNX_MODEL_PATH=/models/detector.onnx \
  -e ONNX_OUTPUT_TYPE=logits \
  orcahello-api:onnx
```
