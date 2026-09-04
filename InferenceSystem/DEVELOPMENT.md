# Development Guide

- [Setup](#setup)
- [Model Inference](#model-inference)
  - [Module Structure](#module-structure)
  - [Key Classes](#key-classes)
  - [Configuration](#configuration)
  - [Convenience Scripts](#convenience-scripts)
  - [Extract FastAI Weights](#extract-fastai-weights)
- [Testing & CI](#testing--ci)
  - [Quick Start](#quick-start)
  - [Test Structure](#test-structure)
  - [Running Specific Tests](#running-specific-tests)
  - [CI](#ci)
- [Inference Orchestrator](#inference-orchestrator)
  - [Docker Container](#docker-container)
  - [Deployment](#deployment)
  - [Monitoring](#monitoring)
  - [Manual Tasks](#manual-tasks)

## Setup

```bash
cd InferenceSystem
uv sync --group dev
source .venv/bin/activate  # or .\.venv\Scripts\activate.bat on Windows
```

For model weights, either:
- Use HuggingFace: `OrcaHelloSRKWDetectorV1.from_pretrained("orcasound/orcahello-srkw-detector-v1")`
- Or extract from FastAI model (see [Extract FastAI Weights](#extract-fastai-weights))

## Model Inference

### Module Structure

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

### Configuration

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
| `pred_local_threshold` | Confidence threshold (0–1) for per-segment binary predictions (used to display in moderator UI). Also selects which segments contribute to global confidence under `mean_thresholded`. |

### Convenience Scripts

**Run inference:**

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

**Run audio processing** (segment audio and generate spectrograms without model inference):

```bash
python scripts/run_audio_processing.py path/to/audio.wav
python scripts/run_audio_processing.py path/to/audio.wav --segment-duration 30
python scripts/run_audio_processing.py path/to/audio.flac --output-dir /tmp/segments
```

**Upload to HuggingFace Hub**

```bash
HF_TOKEN=<your_huggingface_token> python scripts/upload_to_hf_hub.py --checkpoint model/model_v1.pt -m "Update model checkpoint"
```

### Extract FastAI Weights

<details>
<summary>Convert FastAI model.pkl to standalone PyTorch weights (legacy, shouldn't be needed anymore)</summary>

```bash
# Requires inference-venv with fastai installed (see branch: https://github.com/orcasound/orcahello/tree/2026-03-12/inference-snapshot)
source inference-venv/bin/activate
python scripts/extract_fastai_weights.py model/model.pkl model/model_v1.pt
```

</details>


## Testing & CI

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

### CI

Tests run via `.github/workflows/InferenceSystem.yaml`.

- **Component tests** (Ubuntu + Windows): `tests/test_audio_preprocessing.py` and `tests/test_model_inference.py`. These are deterministic and use committed fixtures / Hugging Face model weights only.
- **Integration tests** (Ubuntu + Windows): `tests/test_orchestrator.py` (positive, negative, fail, and Live HLS smoke).
- **Docker smoke**: builds the inference image and runs a short Live HLS container check.

## Inference Orchestrator

See [README.md](README.md#quick-start-live-inference-orchestrator) for a quick start on running the orchestrator locally. The entry point is [src/LiveInferenceOrchestrator.py](src/LiveInferenceOrchestrator.py), which streams HLS audio from Orcasound's S3 buckets via the internal `orcasound_hls` module, runs the SRKW detector model on each segment, and uploads positive detections to the OrcaHello Azure backend. 

In production:
* The orchestrator runs as a Docker container deployed to Azure Kubernetes Service (AKS). 
* A single container image is shared across all hydrophone locations — each hydrophone runs in its own Kubernetes namespace (e.g. `bush-point`, `orcasound-lab`), with a namespace-scoped ConfigMap that holds the hydrophone-specific configuration (hydrophone ID, model thresholds, etc.). 
* The ConfigMap is mounted at `/config/config.yml` in the container at runtime. See [deploy/](deploy/) for all deployment manifests and configmaps.

### Docker Container

**Build:**

```bash
# From InferenceSystem/
docker build . -t live-inference-system -f ./Dockerfile
```

> NOTE: If building locally on an M-series Mac, prefix with `docker buildx build --platform linux/amd64` so the container works on cloud VMs.

**Run locally** by mounting an orchestrator config at `/config/config.yml`:

```bash
# Linux/Mac
docker run --rm -it --env-file .env \
  -v $PWD/tests/orch_configs/LiveHLS/LiveHLS_OrcasoundLab.yml:/config/config.yml \
  live-inference-system \
  --max_live_iterations 2
```

```cmd
:: Windows
docker run --rm -it --env-file .env ^
  -v %cd%/tests/orch_configs/LiveHLS/LiveHLS_OrcasoundLab.yml:/config/config.yml ^
  live-inference-system ^
  --max_live_iterations 2
```

The GitHub repository contains a workflow [`InferenceSystem-deploy.yaml`](../.github/workflows/InferenceSystem-deploy.yaml) that automatically builds and pushes the latest image to ACR when the main branch is tagged with a tag of the form `InferenceSystem.v#.#.#`.


### Deployment

Updating containers in production follows steps below. There isn't automatic CD workflow for this yet https://github.com/orcasound/orcahello/issues/322.

1. Tag a new release off main branch:
```bash
git tag -a "InferenceSystem.v2.x.y" -m "Notes"
git push --tags
```
2. This auto-triggers the [InferenceSystem-deploy workflow](https://github.com/orcasound/orcahello/actions/workflows/InferenceSystem-deploy.yaml), which pushes a Docker container with tag `live-inference-system:[DATE].v2.x.y`
3. Update that tag in `deploy/*.yaml` files as `image: orcaconservancycr.azurecr.io/live-inference-system:[DATE].v2.x.y`
4. Deploy to Kubernetes:
```bash
NAMESPACE=andrews-bay  # or bush-point, mast-center, north-sjc, orcasound-lab, point-robinson, port-townsend, sunset-bay
kubectl apply -f deploy/$NAMESPACE-configmap.yaml
kubectl scale deployment inference-system -n $NAMESPACE --replicas=0
kubectl apply -f deploy/$NAMESPACE.yaml
```
5. Make a PR with the updated `deploy/*.yaml` config files

**Deployment Tips** (there is no staging/dev environment):
- Deploy to one node location first and check status on the [Orcanode monitor](https://orcanodemonitor.azurewebsites.net/OrcaHelloOverview)
- Then deploy to all node locations
- This is not a zero-downtime deployment — due to memory limits on nodes, we scale down to 0 replicas first and then scale back up to 1


### Monitoring

Check deployment status and logs at: https://orcanodemonitor.azurewebsites.net/OrcaHelloOverview

### Manual Tasks

<details>
<summary>Push to Azure Container Registry</summary>

The GitHub workflow handles this automatically on tag push. To push manually:

1. Login to the Azure CLI: `az login --tenant adminorcasound.onmicrosoft.com`
2. Login to ACR: `az acr login --name orcaconservancycr`
3. Tag and push:
```bash
docker tag live-inference-system orcaconservancycr.azurecr.io/live-inference-system:<date>.<version>
docker push orcaconservancycr.azurecr.io/live-inference-system:<date>.<version>
```

</details>

<details>
<summary>Deploy to Azure Kubernetes Service</summary>

Prerequisites:
- Container image pushed to ACR
- [az cli](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) installed
- [kubectl](https://kubernetes.io/docs/tasks/tools/) installed (or run `az aks install-cli`)

```bash
# 1. Login
az login
az aks get-credentials -g LiveSRKWNotificationSystem -n inference-system-AKS
kubectl get nodes  # verify connection

# 2. Apply configmap and deployment
kubectl apply -f deploy/$NAMESPACE-configmap.yaml
kubectl scale deployment inference-system -n $NAMESPACE --replicas=0
kubectl apply -f deploy/$NAMESPACE.yaml

# 3. Verify
kubectl get pods -n $NAMESPACE
kubectl logs -n $NAMESPACE -l app=inference-system
```

</details>

<details>
<summary>Adding a new hydrophone</summary>

1. Create `deploy/{namespace}-configmap.yaml` — use an existing one as a template. The ConfigMap must be in the same namespace as the deployment.
2. Create `deploy/{namespace}.yaml` — use an existing deployment file as a template.
3. Create the namespace and secret:
    ```bash
    kubectl create namespace <namespace>
    kubectl create secret generic inference-system -n <namespace> \
        --from-literal=AZURE_COSMOSDB_PRIMARY_KEY='<key>' \
        --from-literal=AZURE_STORAGE_CONNECTION_STRING='<string>' \
        --from-literal=INFERENCESYSTEM_APPINSIGHTS_CONNECTION_STRING='<string>'
    ```
4. Apply the configmap and deployment:
    ```bash
    kubectl apply -f deploy/<namespace>-configmap.yaml
    kubectl apply -f deploy/<namespace>.yaml
    ```

The Docker container image is common across all hydrophones. Each hydrophone's configuration is stored in a namespace-scoped ConfigMap mounted at `/config/`.

</details>

<details>
<summary>Azure credentials</summary>

You will need an `InferenceSystem/.env` file with Azure credentials. Either ask an existing contributor for theirs, or create one:

```
AZURE_COSMOSDB_PRIMARY_KEY=<key>
AZURE_STORAGE_CONNECTION_STRING=<string>
INFERENCESYSTEM_APPINSIGHTS_CONNECTION_STRING=<string>
```

All credentials are found in the [Azure portal](https://portal.azure.com/) under the `"LiveSRKWNotificationSystem"` resource group:

- **`AZURE_STORAGE_CONNECTION_STRING`** — `"livemlaudiospecstorage"` storage account. See [these instructions](https://docs.microsoft.com/en-us/azure/storage/blobs/storage-quickstart-blobs-python#copy-your-credentials-from-the-azure-portal).
- **`AZURE_COSMOSDB_PRIMARY_KEY`** — `"aifororcasmetadatastore"` CosmosDB account → "Keys" → primary key.
- **`INFERENCESYSTEM_APPINSIGHTS_CONNECTION_STRING`** — `"InferenceSystemInsights"` App Insights → "Essentials" → connection string.

Set as environment variables or add to `.env`:

```bash
# Mac/Linux
export AZURE_STORAGE_CONNECTION_STRING="<value>"
export AZURE_COSMOSDB_PRIMARY_KEY="<value>"
export INFERENCESYSTEM_APPINSIGHTS_CONNECTION_STRING="<value>"
```

```cmd
:: Windows
setx AZURE_STORAGE_CONNECTION_STRING "<value>"
setx AZURE_COSMOSDB_PRIMARY_KEY "<value>"
setx INFERENCESYSTEM_APPINSIGHTS_CONNECTION_STRING "<value>"
```

To test Azure upload locally, use [DateRangeHLS_SunsetBay_AzureUploadTest.yml](tests/orch_configs/LocalDebug/DateRangeHLS_SunsetBay_AzureUploadTest.yml) as a reference. Both `upload_to_azure` and `cleanup_azure_uploads` should be set to avoid permanent changes to the backend.

</details>

<details>
<summary>Deployment to Azure Container Instances (deprecated)</summary>

Ask an existing maintainer for the file `deploy-aci-with-creds.yaml` or change strings in `deploy-aci.yaml`. There are three sensitive strings that must be filled in before deployment can happen.

**NOTE** - Make sure you change these back after running the build - don't commit them to the repository!

1. `<cosmos_primary_key>` - Replace with AZURE_COSMOSDB_PRIMARY_KEY
2. `<storage_connection_string>` - Replace with AZURE_STORAGE_CONNECTION_STRING
3. `<appinsights_connection_string>` - Replace with INFERENCESYSTEM_APPINSIGHTS_CONNECTION_STRING
4. `<image_registry_password>` - Found in the [Azure portal](https://portal.azure.com/#@OrcaConservancy778.onmicrosoft.com/resource/subscriptions/9ffa543e-3596-43aa-b82c-8f41dfbf03cc/resourcegroups/LiveSRKWNotificationSystem/providers/Microsoft.ContainerRegistry/registries/orcaconservancycr/accessKey) under `password`.

```bash
az container create -g LiveSRKWNotificationSystem -f ./deploy-aci.yaml
```

Verify:
```bash
az container attach --resource-group LiveSRKWNotificationSystem --name live-inference-system-aci-3gb-new
```

</details>
